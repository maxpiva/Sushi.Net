using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Events.Audio;
using Sushi.Net.Library.Events.Subtitles.Aegis;
using Sushi.Net.Library.Providers;
using Sushi.Net.Library.Settings;
using Sushi.Net.Library.Timecoding;
using Sushi.Net.Library.Tools;
using Thinktecture.Extensions.Configuration;

namespace Sushi.Net.Library
{
    public class Sushi
    {
        private static readonly string[] SupportedSubtitles = {".ass", ".ssa", ".srt"};
        private readonly Demuxer _demuxer;
        private readonly FFMpeg _ffmpeg;
        private readonly Grouping _grouping;
        private readonly ILogger _logger;
        private readonly AudioReader _reader;
        private readonly Shifter _shifter;
        private readonly BlockManipulation _manipulation;
        private readonly ILoggingConfiguration _logConfig;
        public Sushi(ILogger<Sushi> logger, Demuxer demuxer, FFMpeg ffmpeg, AudioReader reader, Grouping grouping, Shifter shifter, BlockManipulation manipulation, ILoggingConfiguration logConfig)
        {
            _logger = logger;
            _demuxer = demuxer;
            _ffmpeg = ffmpeg;
            _reader = reader;
            _grouping = grouping;
            _shifter = shifter;
            _manipulation = manipulation;
            _logConfig = logConfig;
        }

        private (int width, int height) ParseDimensions(string dimensions)
        {
            string[] dims = dimensions.Split(new char[] {'x', 'X'}, StringSplitOptions.None);
            if (dims.Length== 2)
            {
                if (int.TryParse(dims[0], out int width) && int.TryParse(dims[1], out int height))
                    return (width, height);
            }
            throw new SushiException("Invalid dimensions provided");
        }
        public async Task ValidateAndProcess(SushiSettings args)
        {
            try
            {
                if (args.VerboseVerbose)
                    _logConfig.SetLevel(LogLevel.Trace);
                else if (args.Verbose)
                    _logConfig.SetLevel(LogLevel.Debug);
                else
                    _logConfig.SetLevel(LogLevel.Information);
                if (args.Window > args.MaxWindow)
                    args.MaxWindow = args.Window;
                if (args.MaxWindow == args.Window)
                    args.MaxWindow++;
                args.Output = args.Output.Strip();
                args.Src.CheckFileExists("Source");
                args.Dst.CheckFileExists("Destination");
                args.SrcTimecodes?.CheckFileExists("Source timecodes");
                args.DstTimecodes?.CheckFileExists("Destination timecodes");
                args.Subtitle?.ToList().ForEach(a => a.CheckFileExists($"Subtitle {a}"));
                args.Chapters?.CheckFileExists("Chapters");
                args.SrcKeyframes?.CheckFileExists("Source keyframes");
                args.DstKeyframes?.CheckFileExists("Destination keyframes");
                if (args.SrcTimecodes != null && args.SrcFPS.HasValue || args.DstTimecodes != null && args.DstFPS.HasValue)
                    throw new SushiException("Both fps and timecodes file cannot be specified at the same time");
                if (args.SrcKeyframes != null && (args.DstKeyframes == null && !args.MakeDstKeyframes) || ((args.SrcKeyframes == null && !args.MakeSrcKeyframes) && args.DstKeyframes != null))
                    throw new SushiException("Either none or both of src and dst keyframes should be provided");
                if (args.Subtitle != null && args.Subtitle.Length > 0)
                {
                    foreach (string f in args.Subtitle)
                    {
                        if (!SupportedSubtitles.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            throw new SushiException("Unknown subtitle type.");
                    }
                }

                if (!string.IsNullOrEmpty(args.SubtitleDimensions))
                    ParseDimensions(args.SubtitleDimensions);
                if (args.Type?.ToLowerInvariant() == "audio")
                    await ShiftAudio(args).ConfigureAwait(false);
                else
                    await ShiftSubtitles(args).ConfigureAwait(false);
            }
            catch (SushiException e)
            {
                _logger.LogError(e.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
            _logger.LogInformation("\n");
        }

        public async Task ShiftAudio(SushiSettings args)
        {
            Mux src_mux = new Mux(_demuxer, args.Src, _logger);
            Mux dst_mux = new Mux(_demuxer, args.Dst, _logger);

            try
            {
                string temp_path = args.TempDir;

                await src_mux.GetMediaInfoAsync().ConfigureAwait(false);
                await dst_mux.GetMediaInfoAsync().ConfigureAwait(false);
                temp_path.NormalizePath().CreateDirectoryIfNotExists();
                if (args.Output == null)
                    args.Output = Environment.CurrentDirectory;
                else
                    args.Output.NormalizePath().CreateDirectoryIfNotExists();
                _logger.LogInformation("Finding Chunks in Destination...");
                List<(float start, float end)> silences = await dst_mux.FindSilencesAsync(args.DstAudioIndex, args.SilenceMinLength, args.SilenceThreshold);
                AudioProvider src_audio = new AudioProvider(_reader, src_mux, args.SrcAudioIndex, args.SampleRate, args.SampleType, 0, AudioPostProcess.AudioSearch, temp_path, args.Output);
                AudioProvider dst_audio = new AudioProvider(_reader, dst_mux, args.DstAudioIndex, args.SampleRate, args.SampleType, 0, AudioPostProcess.AudioSearch, temp_path);
                _logger.LogInformation($"Loading Destination Audio {dst_audio.Path}...");
                using (AudioStream dst_stream = await dst_audio.ObtainAsync().ConfigureAwait(false))
                {
                    _logger.LogInformation($"Loading Source Audio {src_audio.Path}...");
                    AudioEvents events;
                    using (AudioStream src_stream = await src_audio.ObtainAsync().ConfigureAwait(false))
                    {
                        events = new AudioEvents(silences, dst_stream.DurationInSeconds);
                        List<List<Event>> search_groups = _grouping.PrepareSearchGroups(events.Events, src_stream.DurationInSeconds, new List<float>(), args.MaxTsDuration, args.MaxTsDistance);
                        _logger.LogInformation($"Calculating Audio Shifts for {src_audio.Path}...");
                        await _shifter.CalculateShiftsAsync(dst_stream, src_stream, search_groups, args.Window, args.MaxWindow, 1, args.AudioAllowedDifference).ConfigureAwait(false);
                    }

                    _logger.LogInformation($"Reloading Source Audio {src_audio.Path}...");
                    using (AudioStream src_stream = await src_audio.ObtainWithoutProcess().ConfigureAwait(false))
                    {
                        _manipulation.ExpandBorders(events.Events, src_stream, .1F, args.SilenceThreshold);
                        for(int x=0;x<events.Events.Count;x++)
                        {
                            Event ev = events.Events[x];
                            string orig = $"Chunk ({ev.ShiftedStart.FormatTime()}=>{ev.ShiftedEnd.FormatTime()} to {ev.Start.FormatTime()}=>{ev.End.FormatTime()}), shift : {-ev.Shift,15: 0.0000000000;-0.0000000000}, diff: {Math.Abs(ev.Diff),15: 0.0000000000;-0.0000000000}";
                            if (x > 0)
                            {
                                Event prev = events.Events.Take(x).FirstOrDefault(a => a.ShiftedEnd > ev.ShiftedStart);
                                if (prev!=null)
                                    orig+=$" [Warn: Section of this block already used at {ev.ShiftedStart.FormatTime()}=>{prev.ShiftedEnd.FormatTime()}]";
                            }
                            _logger.LogInformation(orig);
                        }
                        List<Split> splits = _manipulation.CreateSplits(events.Events, dst_stream.DurationInSeconds);
                        if (!args.DryRun)
                        {
                            _logger.LogInformation($"Rejoining shifted audio into {src_audio.OutputPath}...");
                            await src_audio.ShiftAudioAsync(splits);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
            finally
            {
                if (!args.NoCleanup)
                {
                    src_mux?.CleanUp();
                    dst_mux?.CleanUp();
                }
            }
        }

    

        public async Task ShiftSubtitles(SushiSettings args)
        {
            bool ignore_chapters = args.NoChapters;
            bool external_subtitles = args.Subtitle != null && args.Subtitle.Length > 0;
            string temp_path = args.TempDir;
            Mux src_mux = new Mux(_demuxer, args.Src, _logger);
            Mux dst_mux = new Mux(_demuxer, args.Dst, _logger);
            await src_mux.GetMediaInfoAsync().ConfigureAwait(false);
            await dst_mux.GetMediaInfoAsync().ConfigureAwait(false);
            temp_path.NormalizePath().CreateDirectoryIfNotExists();
            if (!src_mux.HasSubtitles && !external_subtitles)
                throw new SushiException("Subtitles aren't specified");
            if (args.Output == null)
                args.Output = AppContext.BaseDirectory;
            else
                args.Output.NormalizePath().CreateDirectoryIfNotExists();
            AudioProvider src_audio = new AudioProvider(_reader, src_mux, args.SrcAudioIndex, args.SampleRate, args.SampleType, args.Padding, AudioPostProcess.SubtitleSearch, temp_path);
            AudioProvider dst_audio = new AudioProvider(_reader, dst_mux, args.DstAudioIndex, args.SampleRate, args.SampleType, args.Padding, AudioPostProcess.SubtitleSearch, temp_path);
            List<SubtitleProvider> src_subtitles = new List<SubtitleProvider>();
            if ((args.SrcSubtitleIndex==null && src_mux.MediaInfo.Subtitles.Count>0 && !external_subtitles) || (args.SrcSubtitleIndex.HasValue && args.SrcSubtitleIndex == -1 && src_mux.MediaInfo.Subtitles.Count>0))
                src_subtitles = src_mux.MediaInfo.Subtitles.Select(a => new SubtitleProvider(src_mux, a.Id, temp_path, args.Output.Normalize())).ToList();
            else if (args.SrcSubtitleIndex.HasValue && src_mux.MediaInfo.Subtitles.Count>0)
                src_subtitles.Add(new SubtitleProvider(src_mux, args.SrcSubtitleIndex.Value, temp_path, args.Output.NormalizePath()));
            if (external_subtitles)
                src_subtitles.AddRange(args.Subtitle.Select(a => new SubtitleProvider(a, args.Output.NormalizePath())));


            List<float> chapter_times = new List<float>();
            KeyframesProvider keyframes_src = null;
            KeyframesProvider keyframes_dst = null;
            TimecodeProvider timecodes_src = null;
            TimecodeProvider timecodes_dst = null;
            ChapterProvider chapters_src = null;

            if (!args.NoGrouping && !ignore_chapters)
            {
                chapters_src = new ChapterProvider(src_mux, args.Chapters);
            }

            if (args.SrcKeyframes != null || args.MakeSrcKeyframes)
            {
                keyframes_src = new KeyframesProvider(src_mux, args.SrcKeyframes, args.MakeSrcKeyframes, temp_path);
                keyframes_dst = new KeyframesProvider(dst_mux, args.DstKeyframes, args.MakeDstKeyframes, temp_path);
                timecodes_src = new TimecodeProvider(src_mux, args.SrcTimecodes, args.SrcFPS, temp_path);
                timecodes_dst = new TimecodeProvider(dst_mux, args.DstTimecodes, args.DstFPS, temp_path);
            }

            try
            {
                ITimeCodes src_timecodes = null;
                ITimeCodes dst_timecodes = null;
                List<float> src_keytimes = null;
                List<float> dst_keytimes = null;
                _logger.LogInformation($"Providing Source Audio {src_audio.Path}...");
                using (AudioStream src_stream = await src_audio.ObtainAsync().ConfigureAwait(false))
                {
                    _logger.LogInformation($"Providing Destination Audio {dst_audio.Path}...");
                    using (AudioStream dst_stream = await dst_audio.ObtainAsync().ConfigureAwait(false))
                    {
                        if (chapters_src != null)
                        {
                            chapter_times = await chapters_src.ObtainAsync().ConfigureAwait(false);
                        }

                        if (args.SrcKeyframes != null || args.MakeSrcKeyframes)
                        {
                            _logger.LogInformation($"Loading Source Timecodes {timecodes_src.Path}...");
                            src_timecodes = await timecodes_src.ObtainAsync().ConfigureAwait(false);
                            _logger.LogInformation($"Loading Source Keyframes {keyframes_src.Path}...");
                            src_keytimes = (await keyframes_src.ObtainAsync().ConfigureAwait(false)).Select(a => src_timecodes.GetFrameTime(a)).ToList();
                            _logger.LogInformation($"Loading Destination Timecodes {timecodes_src.Path}...");
                            dst_timecodes = await timecodes_dst.ObtainAsync().ConfigureAwait(false);
                            _logger.LogInformation($"Loading Destination Keyframes {keyframes_src.Path}...");
                            dst_keytimes = (await keyframes_dst.ObtainAsync().ConfigureAwait(false)).Select(a => dst_timecodes.GetFrameTime(a)).ToList();
                        }

                        foreach (SubtitleProvider sub_provider in src_subtitles)
                        {
                            _logger.LogInformation($"Loading Subtitle {sub_provider.Path}...");
                            IEvents sub = await sub_provider.ObtainAsync().ConfigureAwait(false);
                            List<List<Event>> search_groups = _grouping.PrepareSearchGroups(sub.Events, src_stream.DurationInSeconds, chapter_times, args.MaxTsDuration, args.MaxTsDistance);
                            _logger.LogInformation($"Calculating Subtitle Shifts for {sub_provider.Path}...");
                            await _shifter.CalculateShiftsAsync(src_stream, dst_stream, search_groups, args.Window, args.MaxWindow, !args.NoGrouping ? args.RewindThresh : 0, args.AllowedDifference).ConfigureAwait(false);
                            List<Event> events = sub.Events;
                            List<List<Event>> groups = _grouping.GroupWithChapters(events, chapter_times, ignore_chapters, !args.NoGrouping, args.SmoothRadius, args.AllowedDifference, args.MaxGroupStd);
                            if (args.SrcKeyframes != null || args.MakeSrcKeyframes)
                            {
                                foreach (Event ev in events.Where(a => a.Linked))
                                    ev.ResolveLink();
                                foreach (List<Event> g in groups)
                                    _grouping.SnapGroupsToKeyFrames(g, chapter_times, args.MaxTsDuration, args.MaxTsDistance, src_keytimes, dst_keytimes, src_timecodes, dst_timecodes, args.MaxKFDistance, args.KfMode);
                            }

                            events.ForEach(a => a.ApplyShift());
                            if (args.ResizeSubtitles && sub is AegisSubtitles aegis)
                            {
                                int width = 0;
                                int height = 0;
                                if (!string.IsNullOrEmpty(args.SubtitleDimensions))
                                    (width, height) = ParseDimensions(args.SubtitleDimensions);
                                else
                                {
                                    if (dst_mux.HasVideo)
                                    {
                                        width = dst_mux.MediaInfo.Videos[0].Width;
                                        height = dst_mux.MediaInfo.Videos[0].Height;
                                    }
                                }

                                if (width != 0 && height != 0)
                                {
                                    _logger.LogInformation($"Resizing {sub_provider.Path} to {width}x{height}...");
                                    aegis.Resize(width, height, args.ResizeBorders);
                                }
                            }

                            if (!args.DryRun)
                            {
                                _logger.LogInformation($"Saving result to {sub_provider.OutputPath}...");
                                await sub.SaveAsync(sub_provider.OutputPath).ConfigureAwait(false);
                            }
                        }
                    }
                }   
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
            finally
            {
                if (!args.NoCleanup)
                {
                    src_mux.CleanUp();
                    dst_mux.CleanUp();
                }
            }
        }


    }
}