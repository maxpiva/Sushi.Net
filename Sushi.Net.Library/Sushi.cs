using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FuzzySharp;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Events.Audio;
using Sushi.Net.Library.Events.Subtitles.Aegis;
using Sushi.Net.Library.Media;
using Sushi.Net.Library.Providers;
using Sushi.Net.Library.Script;
using Sushi.Net.Library.Settings;
using Sushi.Net.Library.Timecoding;
using Sushi.Net.Library.Tools;
using Thinktecture.Extensions.Configuration;
using static System.Net.Mime.MediaTypeNames;


namespace Sushi.Net.Library
{
    public enum AlgoType
    {
        Subtitle,
        Audio
    }
    [Flags]
    public enum Types
    {
        Subtitles = 0x1,
        Audios = 0x2,
        All = 0x3
    }
    public enum ActionType
    {
        Shift,
        Script,
        Export
    }

    public class Sushi
    {
        private static readonly string[] SupportedSubtitles = { ".ass", ".ssa", ".srt" };
        private readonly Demuxer _demuxer;
        private readonly FFMpeg _ffmpeg;
        private readonly Grouping _grouping;
        private readonly ILogger _logger;
        private readonly AudioReader _reader;
        private readonly Shifter _shifter;
        private readonly BlockManipulation _manipulation;
        private readonly ILoggingConfiguration _logConfig;
        private readonly FFProbe _probe;

        public Sushi(ILogger<Sushi> logger, Demuxer demuxer, FFMpeg ffmpeg, FFProbe probe, AudioReader reader, Grouping grouping, Shifter shifter, BlockManipulation manipulation, ILoggingConfiguration logConfig)
        {
            _logger = logger;
            _demuxer = demuxer;
            _ffmpeg = ffmpeg;
            _reader = reader;
            _grouping = grouping;
            _shifter = shifter;
            _manipulation = manipulation;
            _logConfig = logConfig;
            _probe = probe;
        }

        private (int width, int height) ParseDimensions(string dimensions)
        {
            string[] dims = dimensions.Split(new char[] { 'x', 'X' }, StringSplitOptions.None);
            if (dims.Length == 2)
            {
                if (int.TryParse(dims[0], out int width) && int.TryParse(dims[1], out int height))
                    return (width, height);
            }

            throw new SushiException("Invalid dimensions provided");
        }
        private List<string> GetAllFiles(string path)
        {
            List<string> files = new List<string>();
            if (path.Contains("*"))
            {
                string dir = Path.GetDirectoryName(path);
                string file = Path.GetFileName(path);
                if (string.IsNullOrEmpty(dir))
                    dir = Environment.CurrentDirectory;
                files.AddRange(Directory.GetFiles(dir, file));
            }
            else
                files.Add(path);
            return files;
        }
      
        private Dictionary<string, string> GenerateMatchers(string original, List<string> matched)
        {
            string path = Path.GetDirectoryName(original);
            Dictionary<string, string> ret = new Dictionary<string, string>();
            original = Path.GetFileName(original);
            matched = matched.Select(Path.GetFileName).ToList();
            string regex = string.Join("(.*?)", original.Split("*").Select(Regex.Escape));
            Regex r= new Regex(regex, RegexOptions.Compiled|RegexOptions.Singleline);
            foreach (string m in matched)
            {
                Match q = r.Match(m);
                if (q.Success)
                {
                    string z = string.Join("_", q.Groups.Cast<Group>().Skip(1).Select(a => a.Value));
                    ret.Add(Path.Combine(path, m), z);
                }
            }
            return ret;
        }


        private string GetBestMatch(KeyValuePair<string,string> from, Dictionary<string, string> to)
        {
            int max = int.MinValue;
            string best = null;
            foreach (string s in to.Values)
            {
                int d = Fuzz.Ratio(from.Value, s);
                if (d > max)
                {
                    max = d;
                    best = s;
                }
            }

            return to.First(a => a.Value == best).Key;
        }

        public async Task ValidateAndProcess(SushiSettings args)
        {
            try
            {
                List<(string source, string destination)> pairs = new List<(string source, string destination)>();


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
                switch (args.Algo)
                {
                    case AlgoType.Audio:
                        args.Mode ??= Mode.CCoeffNormed;
                        break;
                    default:
                        args.Mode ??= Mode.SqDiffNormed;
                        break;

                }

                if (args.OnlyExtract && string.IsNullOrEmpty(args.SubtitleDimensions))
                {
                    throw new SushiException($"When using OnlyExtract, SubtitleDimensions are mandatory");
                }

                args.Output = args.Output.Strip();
                Dictionary<string, string> src_matches = new Dictionary<string, string>();
                if (args.Action != ActionType.Script)
                {

                    if (args.Src==null)
                        throw new SushiException($"Option '--src' is missing");
                    List<string> src_files = GetAllFiles(args.Src).OrderBy(a=>a).ToList();
                    List<string> dst_files = new List<string>();
                    if (src_files.Count==1)
                        args.Src.CheckFileExists("Source");
                    if (!args.OnlyExtract)
                    {
                        if (args.Dst == null)
                            throw new SushiException($"Option '--dst' is missing");
                        dst_files = GetAllFiles(args.Dst);
                        if (dst_files.Count == 1)
                        {
                            args.Dst.CheckFileExists("Destination");
                            pairs.Add((src_files[0], dst_files[0]));
                            dst_files.Remove(dst_files[0]);
                        }
                        else
                        {
                            src_matches = GenerateMatchers(args.Src, src_files);
                            Dictionary<string, string> dst_matches = GenerateMatchers(args.Dst, dst_files);
                            if (src_matches.Count > dst_matches.Count)
                                throw new SushiException($"Too many source files, Source Count: {src_matches.Count} Destination Count: {dst_matches.Count}");
                            foreach (var kk in src_matches)
                            {
                                string n = GetBestMatch(kk, dst_matches);
                                if (n != null)
                                {
                                    pairs.Add((kk.Key, n));
                                    dst_matches.Remove(n);
                                }
                                else
                                    throw new SushiException($"Cannot find destination file for {kk.Key}");
                            }

                            if (dst_matches.Count > 0)
                                throw new SushiException($"Too many destination files, these remains without match:" +
                                                         string.Join(",", dst_matches.Select(a=>a.Key)));
                        }
                    }
                    else
                        pairs = src_files.Select(a => (a, (string)null)).ToList();
                }
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

                if (args.Output == null)
                    args.Output = Environment.CurrentDirectory;
                else
                    args.Output.NormalizePath().CreateDirectoryIfNotExists();
                if (args.Action == ActionType.Script || args.Action == ActionType.Export)
                {
                    if (string.IsNullOrEmpty(args.ScriptFile))
                        throw new SushiException("Script File is missing.");
                    if (args.Action == ActionType.Script)
                        args.ScriptFile.CheckFileExists("Script File");
                }
                if (args.Action == ActionType.Script)
                    await ShiftScript(args).ConfigureAwait(false);
                else
                {
                    string script = args.ScriptFile;
                    foreach ((string source, string destination) in pairs)
                    {
                        
                        args.Src = source;
                        args.Dst = destination;
                        if (!string.IsNullOrEmpty(script) && script.Contains("*") && src_matches.Count>0)
                        {
                            args.ScriptFile = script.Replace("*", src_matches[source]);
                        }
                        if (args.Algo == AlgoType.Subtitle)
                        {
                            if (args.Action == ActionType.Export)
                                await ShiftSubsExport(args).ConfigureAwait(false);
                            else if (!args.OnlyExtract)
                                await ShiftSubs(args).ConfigureAwait(false);
                            else
                                await OnlyExportSubs(args).ConfigureAwait(false);
                        }
                        else
                        {
                            if (args.Action == ActionType.Export)
                                await ShiftAudioExport(args).ConfigureAwait(false);
                            else
                            {

                                Dictionary<int, float> syncs = new Dictionary<int, float>();
                                if (args.SrcMultiSync > 0)
                                {
                                    Mux src_mux = new Mux(_demuxer, args.Src, _logger);
                                    AudioMedia src_audio = await src_mux.ObtainMediaInfoForProcessAsync(args.SrcAudio, args).ConfigureAwait(false);
                                    if ((args.Type & Types.Audios) == Types.Audios)
                                    {
                                        foreach (AudioMedia m in src_mux.Audios)
                                        {
                                            m.ShouldProcess = true;
                                            m.SetAudioProcessing(args.SampleRate, args.Normalize, AudioPostProcess.None, args.VoiceRemoval, args.DownMixStereo);
                                            m.SetPaths(args.TempDir, args.Output);
                                            m.SetSilenceSearch(args.SilenceMinLength, args.SilenceThreshold);
                                        }

                                    }

                                    src_mux.LimitSeconds = args.SrcMultiSync;
                                    int src_index = src_mux.GetAudioIndex(args.SrcAudio);
                                    if (src_mux.Audios.Count>1)
                                    {
                                        int? old_audio = args.DstAudio;
                                        foreach (AudioMedia m in src_mux.Audios)
                                        {
                                            if (src_mux.GetAudioIndex(m.Info.Id) != src_index)
                                            {
                                                float val = await GetLimitSync(src_mux, args, m.Info.Id);
                                                syncs.Add(m.Info.Id, val);
                                            }
                                        }
                                        args.DstAudio = old_audio;
                                    }
                                }
                                await ShiftAudio(args,syncs).ConfigureAwait(false);
                            }

                        }
                    }
                }
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



        private async Task<AudioEvents> CalculateShiftsAsync(SushiSettings args, AudioMedia src_mux, AudioMedia dst_mux, AudioPostProcess process)
        {
            try
            {
                string temp_path = args.TempDir;
                bool ignore_chapters = args.NoChapters;
                temp_path.NormalizePath().CreateDirectoryIfNotExists();
                ChapterProvider chapters_dst = null;


                AudioProvider src_audio = new AudioProvider(_reader, src_mux, args.SampleRate, args.SampleType, 0, args.Normalize, args.VoiceRemoval, args.DownMixStereo, process, temp_path, args.Output);
                AudioProvider dst_audio = new AudioProvider(_reader, dst_mux, args.SampleRate, args.SampleType, 0, args.Normalize, args.VoiceRemoval, args.DownMixStereo, process, temp_path);
                if (!args.NoGrouping && !ignore_chapters)
                {
                    chapters_dst = new ChapterProvider(dst_mux.Mux, args.Chapters);
                }
                _logger.LogInformation($"Loading Destination Audio {dst_audio.Media.ProcessPath}...");

                using (AudioStream dst_stream = await dst_audio.ObtainAsync().ConfigureAwait(false))
                {
                    List<float> chapter_times = new List<float>();
                    if (chapters_dst != null)
                    {
                        chapter_times = await chapters_dst.ObtainAsync().ConfigureAwait(false);
                    }
                    List<(float start, float end)> silences = dst_mux.Silences;
                    _logger.LogInformation($"Loading Source Audio {src_audio.Media.ProcessPath}...");
                    AudioEvents events;
                    using (AudioStream src_stream = await src_audio.ObtainAsync().ConfigureAwait(false))
                    {


                        events = new AudioEvents(silences, dst_stream.DurationInSeconds);
                        events.ChapterTimes = chapter_times;
                        List<List<Event>> search_groups = _grouping.PrepareSearchGroups(events.Events, src_stream.DurationInSeconds, chapter_times, args.MaxTsDuration, args.MaxTsDistance);
                        _logger.LogInformation($"Calculating Audio Shifts [with {args.Mode.Value.ToString()}] for {src_audio.Media.ProcessPath}...");
                        await _shifter.CalculateShiftsAsync(dst_stream, src_stream, search_groups, args.Window, args.MaxWindow, 1, args.AudioAllowedDifference, args.Mode.Value).ConfigureAwait(false);
                    }
                    /*
                    _logger.LogInformation($"Reloading Source Audio {src_audio.Media.ProcessPath}...");
                    using (AudioStream src_stream = await src_audio.ObtainWithoutProcess().ConfigureAwait(false))
                    {
                        _manipulation.ExpandBorders(events.Events, src_stream, .1F, args.SilenceThreshold);
                        return events;
                    }*/
                    List<List<Event>> groups = _grouping.GroupWithChapters(events.Events, chapter_times, ignore_chapters,  !args.NoGrouping, args.SmoothRadius, args.AllowedDifference, args.MaxGroupStd,  "blocks");
                    List<Event> final=new List<Event>();
                    int s=0;
                    foreach(List<Event> ls in groups)
                    {
                        Event ev=ls[0];
                        ev.End=ls[^1].End;
                        ev.SourceIndex = s++;
                        final.Add(ev);
                    }

                    events.ChapterTimes = chapter_times;
                    events.Events=final;
                    return events;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
        }

        private async Task ResolveImportAsync(SushiSettings args, Script.Script script)
        {
            args.TempDir.NormalizePath().CreateDirectoryIfNotExists();
            List<AudioMedia> outsteams = new List<AudioMedia>();
            foreach (StreamCommands s in script.Streams)
            {
                foreach (Decoding.Media m in s.Medias)
                {
                    //m.ShouldProcess=true;
                    m.SetPaths(args.TempDir, args.Output);
                }
            }
            bool demux=script.Mux.Subtitles.Any(a => a.ShouldProcess) || script.Mux.Audios.Any(a => a.ShouldProcess);
            if (demux)
                await script.Mux.ProcessAsync().ConfigureAwait(false);
        }

        private (int width, int height) GetWidthAndHeight(SushiSettings args, Mux mux)
        {
            int width = 0;
            int height = 0;
            if (!string.IsNullOrEmpty(args.SubtitleDimensions))
                (width, height) = ParseDimensions(args.SubtitleDimensions);
            else if (mux!=null)
            {
                if (mux.Videos.Count>0)
                {
                    width = mux.Videos[0].Info.Width;
                    height = mux.Videos[0].Info.Height;
                }
            }

            return (width, height);
        }

        private void SetRescaleIfNeeded(Mux src_mux, Mux dst_mux, SushiSettings args)
        {
            AudioMedia src_audio = src_mux.GetAudioStream(args.SrcAudio);
            AudioMedia dst_audio = src_mux.GetAudioStream(args.SrcAudio);

            if (src_audio.Info.FrameRateValue != 0 && dst_audio.Info.FrameRateValue != 0)
            {
                if (Math.Abs(src_audio.Info.FrameRateValue - dst_audio.Info.FrameRateValue) > 0.0001)
                {
                    src_mux.ReScale = dst_audio.Info.FrameRateValue / src_audio.Info.FrameRateValue;
                    _logger.LogInformation(
                        $"Rescaling required ({src_audio.Info.FrameRateValue:N0} -> {dst_audio.Info.FrameRateValue:N0}) fps.");
                }
                return;
            }
            if (src_mux.HasVideos && dst_mux.HasVideos)
            {
                MediaStreamInfo sm = src_mux.Videos.First().Info;
                MediaStreamInfo dm = dst_mux.Videos.First().Info;
                if (sm.FrameRateValue != 0 && dm.FrameRateValue != null &&
                    Math.Abs(sm.FrameRateValue - dm.FrameRateValue) > 0.0001)
                {
                    src_mux.ReScale = dm.FrameRateValue / sm.FrameRateValue;
                    _logger.LogInformation($"Rescaling required ({sm.FrameRateValue:N0} -> {dm.FrameRateValue:N0}) fps.");
                }
            }
        }

        private async Task<Script.Script> ResolveShiftsAsync(SushiSettings args, Mux src_mux, Mux dst_mux, List<SubtitleMedia> externalsubs, Dictionary<int, float> syncs, bool dump=true)
        {
            try
            {

                List<AudioMedia> outsteams = new List<AudioMedia>();
                AudioMedia src_audio = await src_mux.ObtainMediaInfoForProcessAsync(args.SrcAudio, args).ConfigureAwait(false);
                AudioMedia dst_audio = await dst_mux.ObtainMediaInfoForProcessAsync(args.DstAudio, args).ConfigureAwait(false);
                SetRescaleIfNeeded(src_mux, dst_mux, args);
                outsteams.Add(src_audio);
                if ((args.Type&Types.Audios)==Types.Audios)
                {
                    foreach (AudioMedia m in src_mux.Audios)
                    {
                        if (!m.ShouldProcess)
                        {
                            m.ShouldProcess = true;
                            m.SetPaths(args.TempDir, args.Output);
                            m.SetSilenceSearch(args.SilenceMinLength,args.SilenceThreshold);
                            outsteams.Add(m);
                        }
                    }

                }
                if ((args.Type&Types.Subtitles)==Types.Subtitles)
                {
                    foreach (SubtitleMedia m in src_mux.Subtitles)
                    {
                        if (!m.ShouldProcess)
                        {
                            m.ShouldProcess = true;
                            m.SetPaths(args.TempDir, args.Output);
                        }
                    }
                }

                AudioEvents events = await CalculateShiftsAsync(args, src_audio, dst_audio, AudioPostProcess.None);
                for(int x=0;x<events.Events.Count;x++)
                {
                    Event ev = events.Events[x];
                    string orig = $"Chunk ({ev.ShiftedStart.FormatTime()}=>{ev.ShiftedEnd.FormatTime()} to {ev.Start.FormatTime()}=>{ev.End.FormatTime()}), shift : {-ev.Shift,15: 0.0000000000;-0.0000000000}, diff: {Math.Abs(ev.Diff),15: 0.0000000000;-0.0000000000}";
                    if (x > 0)
                    {
                        foreach (float f in events.ChapterTimes)
                        {
                            if (ev.Start < f && ev.End > f)
                            {
                                orig += $" [Info: {f.FormatTime()} chapter is inside]";
                                break;
                            }
                        }
                        Event prev = events.Events.Take(x).FirstOrDefault(a => a.ShiftedEnd > ev.ShiftedStart);
                        if (prev!=null)
                            orig+=$" [Warn: Section of this block already used at {ev.ShiftedStart.FormatTime()}=>{prev.ShiftedEnd.FormatTime()}]";
                    }
                    _logger.LogInformation(orig);
                }
                PrecomputedAudioEvents preco = new PrecomputedAudioEvents(events);
                foreach (AudioMedia m in outsteams)
                {
                    List<Event> temp = preco.AudioEvents.Events.Select(a => a.Clone()).ToList();
                    foreach (Event ev in temp)
                    {
                        if (ev.Shift<0.001 && ev.Shift>-0.001)
                            ev.AdjustShift(0);
                    }

                    if (m.Info.StartTime != 0)
                    {
                        if (syncs.ContainsKey(m.Info.Id))
                        {
                            syncs[m.Info.Id] += -m.Info.StartTime;
                        }
                        else
                        {
                            syncs[m.Info.Id] = -m.Info.StartTime;
                        }
                    }

                    if (syncs.ContainsKey(m.Info.Id))
                    {
                        float val = syncs[m.Info.Id];
                        if (val != 0)
                        {
                           _logger.LogInformation($"Applying sync of {val} to {m.ProcessPath}");
                           foreach (Event ev in temp)
                           {
                               ev.AdjustShift(val);
                               if (ev.Start + ev.Shift < 0)
                                   ev.Start = -ev.Shift;
                           }
                        }
                    }
                    List<ComputedMovement> ls=preco.AddStream(m, temp, args.SilenceAssignThreshold);
                    _logger.LogInformation($"ReAssigning Blocks for {m.ProcessPath}...");
                    foreach(ComputedMovement l in ls)
                    {
                        if (l.Warning!=0)
                        {
                            string error=preco.GetErrorWarning(l,args.SilenceAssignThreshold);
                            string  msg=$"[{l.AbsolutePosition.FormatTime()}] - "+error;
                            if (l.Warning==1)
                                _logger.LogWarning(msg);
                            else if (l.Warning==2)
                                _logger.LogError(msg);
                        }
                    }
                }

                float duration = dst_audio.Info.Duration;
                List<(List<ComputedMovement>, List<Decoding.Media>)> decs = new List<(List<ComputedMovement>, List<Decoding.Media>)>();
                float delay = args.SubTime;
                List<Decoding.Media> unsort = new List<Decoding.Media>();
                if (((args.Type & Types.Subtitles) == Types.Subtitles))
                {
                    unsort = src_mux.Subtitles.Where(a => a.ShouldProcess).Cast<Decoding.Media>().ToList();
                    if (externalsubs != null && externalsubs.Count > 0)
                        unsort.AddRange(externalsubs);
                    if (unsort.Count > 0)
                    {
                        if (((args.Type & Types.Subtitles) == Types.Subtitles))
                        {
                            float srcms = await _probe.GetAudioDelayAsync(args.Src, src_mux.GetAudioIndex(args.SrcAudio)).ConfigureAwait(false);
                            float dstms = await _probe.GetAudioDelayAsync(args.Dst, dst_mux.GetAudioIndex(args.DstAudio)).ConfigureAwait(false);
                            delay = delay - srcms + dstms;
                        }
                    }
                }
                foreach (AudioMedia m in preco.Streams.Keys.Reverse())
                {
                    List<Decoding.Media> cur = new List<Decoding.Media>();
                    cur.Add(m);
                    if (syncs.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(m.Info.Language))
                        {
                            List<Decoding.Media> meds = unsort.Where(a => a.Info.Language == m.Info.Language).ToList();
                            if (meds.Count > 0)
                            {
                                cur.AddRange(meds);
                                meds.ForEach(a => unsort.Remove(a));
                            }
                        }
                    }

                    decs.Add((preco.Streams[m], cur));
                }

                if (unsort.Count > 0)
                {
                    decs[^1].Item2.AddRange(unsort);
                }

                decs.ForEach(a => a.Item2 = a.Item2.OrderBy(a => a.Info.Id).ToList());
                decs.Reverse();
                (int width, int height) = GetWidthAndHeight(args, dst_mux);
                Script.Script script = await Script.Script.CreateScriptAsync(_demuxer, _logger, src_mux, decs.ToDictionary(a => a.Item1, a => a.Item2), delay, duration, width, height).ConfigureAwait(false);
                if (!dump)
                    return script;
                _logger.LogInformation($"Exporting script file to {args.ScriptFile}...");
                await File.WriteAllLinesAsync(args.ScriptFile, script.Serialize(args.AbsoluteTimes)).ConfigureAwait(false);
                return script;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }



        }

        private async Task<Script.Script> ResolveOwnAsync(SushiSettings args, Mux src, List<SubtitleMedia> externalsubs, bool dump = true)
        {
            try
            {

                List<AudioMedia> outsteams = new List<AudioMedia>();
                AudioMedia src_audio = src.GetAudioStream(args.SrcAudio);
                AudioMedia dst_audio = src.GetAudioStream(args.DstAudio);
                outsteams.Add(src_audio);
                AudioEvents events = await CalculateShiftsAsync(args, src_audio, dst_audio, AudioPostProcess.None);
                for (int x = 0; x < events.Events.Count; x++)
                {
                    Event ev = events.Events[x];
                    string orig = $"Chunk ({ev.ShiftedStart.FormatTime()}=>{ev.ShiftedEnd.FormatTime()} to {ev.Start.FormatTime()}=>{ev.End.FormatTime()}), shift : {-ev.Shift,15: 0.0000000000;-0.0000000000}, diff: {Math.Abs(ev.Diff),15: 0.0000000000;-0.0000000000}";
                    if (x > 0)
                    {
                        foreach (float f in events.ChapterTimes)
                        {
                            if (ev.Start < f && ev.End > f)
                            {
                                orig += $" [Info: {f.FormatTime()} chapter is inside]";
                                break;
                            }
                        }
                        Event prev = events.Events.Take(x).FirstOrDefault(a => a.ShiftedEnd > ev.ShiftedStart);
                        if (prev != null)
                            orig += $" [Warn: Section of this block already used at {ev.ShiftedStart.FormatTime()}=>{prev.ShiftedEnd.FormatTime()}]";
                    }
                    _logger.LogInformation(orig);
                }
                PrecomputedAudioEvents preco = new PrecomputedAudioEvents(events);
                foreach (AudioMedia m in outsteams)
                {
                    List<ComputedMovement> ls = preco.AddStream(m, preco.AudioEvents.Events, args.SilenceAssignThreshold);
                    _logger.LogInformation($"ReAssigning Blocks for {m.ProcessPath}...");
                    foreach (ComputedMovement l in ls)
                    {
                        if (l.Warning != 0)
                        {
                            string error = preco.GetErrorWarning(l, args.SilenceAssignThreshold);
                            string msg = $"[{l.AbsolutePosition.FormatTime()}] - " + error;
                            if (l.Warning == 1)
                                _logger.LogWarning(msg);
                            else if (l.Warning == 2)
                                _logger.LogError(msg);
                        }
                    }
                }

                float duration = dst_audio.Info.Duration;
                List<(List<ComputedMovement>, List<Decoding.Media>)> decs = new List<(List<ComputedMovement>, List<Decoding.Media>)>();
                float delay = args.SubTime;
                List<Decoding.Media> unsort = new List<Decoding.Media>();

                foreach (AudioMedia m in preco.Streams.Keys.Reverse())
                {
                    List<Decoding.Media> cur = new List<Decoding.Media>();
                    cur.Add(m);
                    if (!string.IsNullOrEmpty(m.Info.Language))
                    {
                        List<Decoding.Media> meds = unsort.Where(a => a.Info.Language == m.Info.Language).ToList();
                        if (meds.Count > 0)
                        {
                            cur.AddRange(meds);
                            meds.ForEach(a => unsort.Remove(a));
                        }
                    }
                    decs.Add((preco.Streams[m], cur));
                }

                if (unsort.Count > 0)
                {
                    decs[^1].Item2.AddRange(unsort);
                }

                decs.ForEach(a => a.Item2 = a.Item2.OrderBy(a => a.Info.Id).ToList());
                decs.Reverse();
                (int width, int height) = GetWidthAndHeight(args, src);
                Script.Script script = await Script.Script.CreateScriptAsync(_demuxer, _logger, src, decs.ToDictionary(a => a.Item1, a => a.Item2), delay, duration, width, height).ConfigureAwait(false);
                if (!dump)
                    return script;
                _logger.LogInformation($"Exporting script file to {args.ScriptFile}...");
                await File.WriteAllLinesAsync(args.ScriptFile, script.Serialize(args.AbsoluteTimes)).ConfigureAwait(false);
                return script;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
            finally
            {

            }


        }

        public async Task ShiftAudioExport(SushiSettings args)
        {
            Mux src_mux = new Mux(_demuxer, args.Src, _logger);
            Mux dst_mux = new Mux(_demuxer, args.Dst, _logger);
            src_mux.AudioOutputCodec = args.OutputAudioCodec;
            src_mux.AudioOutputCodecParameters = args.OutputAudioParams;
            try
            {
                List<SubtitleMedia> medias = new List<SubtitleMedia>();
                if (args.Subtitle != null && args.Subtitle.Length > 0)
                    medias = args.Subtitle.Select(a => new SubtitleMedia(a)).ToList();
                await ResolveShiftsAsync(args, src_mux, dst_mux, medias, new Dictionary<int, float>(),true).ConfigureAwait(false);

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
        public async Task ShiftSubsExport(SushiSettings args)
        {
            Mux src_mux = new Mux(_demuxer, args.Src, _logger);
            Mux dst_mux = new Mux(_demuxer, args.Dst, _logger);
            src_mux.AudioOutputCodec = args.OutputAudioCodec;
            src_mux.AudioOutputCodecParameters = args.OutputAudioParams;
            try
            {
                await ResolveShiftSubtitlesAsync(args, src_mux, dst_mux, true).ConfigureAwait(false);
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
        public async Task ShiftScript(SushiSettings args)
        {

            Script.Script s = await Script.Script.ParseScriptAsync(_demuxer, _logger, (await File.ReadAllLinesAsync(args.ScriptFile).ConfigureAwait(false)).ToList()).ConfigureAwait(false);
            s.Mux.ReScale = s.ReScale;
            s.Mux.AudioOutputCodec = args.OutputAudioCodec;
            s.Mux.AudioOutputCodecParameters = args.OutputAudioParams;
            try
            {
                await ResolveImportAsync(args, s).ConfigureAwait(false);
                await ProcessFiles(args, s).ConfigureAwait(false);

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
                    s.Mux?.CleanUp();
                }
            }
        }

        private List<SubtitleProvider> GetSubtitles(SushiSettings args, Mux src_mux)
        {
            bool external_subtitles = args.Subtitle != null && args.Subtitle.Length > 0;
            string temp_path = args.TempDir;
            List<SubtitleProvider> src_subtitles = new List<SubtitleProvider>();
            if ((args.SrcSubtitle==null && src_mux.Subtitles.Count>0 && !external_subtitles) || (args.SrcSubtitle.HasValue && args.SrcSubtitle == -1 && src_mux.Subtitles.Count>0))
                src_subtitles = src_mux.Subtitles.Select(a => 
                {
                    a.ShouldProcess=true;
                    return new SubtitleProvider(a);
                }).ToList();
            else if (args.SrcSubtitle.HasValue && src_mux.Subtitles.Count>0)
            {
                var s=src_mux.GetSubtitleStream(args.SrcSubtitle.Value);
                s.ShouldProcess=true;
                src_subtitles.Add(new SubtitleProvider(s));
            }
            if (external_subtitles)
                src_subtitles.AddRange(args.Subtitle.Select(a => new SubtitleProvider(new SubtitleMedia(a))));
            src_subtitles.ForEach(a=>a.Media.SetPaths(args.TempDir, args.Output));
            return src_subtitles;
        }

        private Task<IEvents> LoadSubtitlesAsync(SubtitleProvider provider)
        {
            _logger.LogInformation($"Loading Subtitle {provider.Media.ProcessPath}...");
            return provider.ObtainAsync();
        }

        public async Task ProcessFiles(SushiSettings args, Script.Script script)
        {
            if ((args.Type&Types.Audios)==Types.Audios)
            {
                List<AudioShift> shifts = script.GetAudioShifts();
                foreach (AudioShift shift in shifts)
                {
                    shift.Media.SetPaths(args.TempDir, args.Output);
                    _logger.LogInformation($"Rejoining shifted audio into {shift.Media.OutputPath}...");
                    await shift.Media.ShiftAudioAsync(shift.Blocks, args.TempDir, args.MinimalAudioShift).ConfigureAwait(false);
                }
            }

            if (((args.Type & Types.Subtitles) == Types.Subtitles))
            {
                List<SubtitleShift> r=await script.GetSubtitleShiftAsync().ConfigureAwait(false);
                foreach (SubtitleShift m in r)
                {
                    await ApplyShiftAsync(args, m).ConfigureAwait(false);

                }
            }
            
        }

        private async Task ApplyShiftAsync(SushiSettings args, SubtitleShift ev)
        {
            ev.Events.Events.ForEach(a => a.ApplyShift());
            if (args.ResizeSubtitles && ev.Events is AegisSubtitles aegis)
            {
                if (ev.Width != 0 && ev.Height != 0)
                {
                    _logger.LogInformation($"Resizing {ev.SubtitleMedia.ProcessPath} to {ev.Width}x{ev.Height}...");
                    aegis.Resize(ev.Width, ev.Height, args.ResizeBorders);
                }
            }

            if (ev.SubDelay != 0)
            {
                _logger.LogInformation($"Applying subtitle time shift {args.SubTime}ms.");
                ev.Events.Events.ForEach(a => a.ApplyTime(ev.SubDelay/1000f));
            }
            if (!args.DryRun)
            {
                _logger.LogInformation($"Saving result to {ev.SubtitleMedia.OutputPath}...");
                await ev.Events.SaveAsync(ev.SubtitleMedia.OutputPath).ConfigureAwait(false);
            }
        }

        private void ShiftEventsWithAudio(IEvents events, AudioEvents audios)
        {
            if (audios.Events.Count == 0)
                return;
            List<Event> matches=new List<Event>();
            foreach (Event ev in audios.Events)
            {
                
                foreach (Event e in events.Events.Where(a => a.Start >= ev.Start && a.Start < ev.End))
                {
                    e.SetShift(-ev.Shift, 0);
                    matches.Add(e);
                }
            }

            List<Event> inter = events.Events.Except(matches).ToList();
            foreach (Event e in inter)
            {
                if (e.Start < audios.Events[0].Start)
                {
                    e.SetShift(-audios.Events[0].Shift,0);
                }
                else if (e.Start > audios.Events[^1].End)
                {
                    e.SetShift(-audios.Events[^1].Shift, 0);
                }
                else
                {
                    Event f = audios.Events.FirstOrDefault(a => a.End < e.Start);
                    if (f!=null)
                        e.SetShift(-f.Shift,0);
                }
            }
        }

        public async Task ShiftSubs(SushiSettings args)
        {
            Mux src_mux = new Mux(_demuxer, args.Src, _logger);
            Mux dst_mux = new Mux(_demuxer, args.Dst, _logger);
            src_mux.AudioOutputCodec = args.OutputAudioCodec;
            src_mux.AudioOutputCodecParameters = args.OutputAudioParams;
            try
            {
                Script.Script script = await ResolveShiftSubtitlesAsync(args, src_mux, dst_mux, !string.IsNullOrEmpty(args.ScriptFile)).ConfigureAwait(false);
                await ProcessFiles(args, script).ConfigureAwait(false);

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
        public async Task OnlyExportSubs(SushiSettings args)
        {
            Mux src_mux = new Mux(_demuxer, args.Src, _logger);
            src_mux.AudioOutputCodec = args.OutputAudioCodec;
            src_mux.AudioOutputCodecParameters = args.OutputAudioParams;
            try
            {
                Script.Script script = await CreateEmptyShiftSubtitlesAsync(args, src_mux, !string.IsNullOrEmpty(args.ScriptFile)).ConfigureAwait(false);
                script.Mux.Audios.ForEach(a => a.ShouldProcess = false);
                await ProcessFiles(args, script).ConfigureAwait(false);

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
                }
            }

        }

        public async Task<float> GetLimitSync(Mux mux, SushiSettings args, int destination_audio)
        {
            mux.AudioOutputCodec = args.OutputAudioCodec;
            mux.AudioOutputCodecParameters = args.OutputAudioParams;
            args.DstAudio = destination_audio;
            Script.Script script = await ResolveOwnAsync(args, mux, [],false).ConfigureAwait(false);
            BaseCommand  bs = script.Streams.FirstOrDefault()?.Commands.FirstOrDefault(a=>a is FillCommand || a is CutCommand);
            if (bs is FillCommand)
                return bs.Duration;
            if (bs is CutCommand)
                return -bs.Duration;
            return 0;
        }
        public async Task ShiftAudio(SushiSettings args, Dictionary<int, float> syncs)
        {
            Mux src_mux = new Mux(_demuxer, args.Src, _logger);
            Mux dst_mux = new Mux(_demuxer, args.Dst, _logger);
            src_mux.AudioOutputCodec = args.OutputAudioCodec;
            src_mux.AudioOutputCodecParameters = args.OutputAudioParams;
            try
            {
                List<SubtitleMedia> ext_subs = new List<SubtitleMedia>();
                if (args.Subtitle != null && args.Subtitle.Length > 0)
                    ext_subs.AddRange(args.Subtitle.Select(a =>new SubtitleMedia(a)));
                Script.Script script = await ResolveShiftsAsync(args, src_mux, dst_mux, ext_subs, syncs, !string.IsNullOrEmpty(args.ScriptFile)).ConfigureAwait(false);
                await ProcessFiles(args, script).ConfigureAwait(false);

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

        private void OverlayEvent(List<PeriodEvent> orig, PeriodEvent nw)
        {
            foreach (PeriodEvent ev2 in orig)
            {
                if (nw.Start <= ev2.End && ev2.Start <= nw.End)
                {
                    orig.Remove(ev2);
                    ev2.Start = Math.Min(nw.Start, ev2.Start);
                    ev2.End = Math.Max(nw.End, ev2.End);
                    OverlayEvent(orig, ev2);
                    return;

                }
            }
            orig.Add(nw);
        }


        public async Task<Script.Script> CreateEmptyShiftSubtitlesAsync(SushiSettings args, Mux src_mux, bool dump)
        {
            bool external_subtitles = args.Subtitle != null && args.Subtitle.Length > 0;
            string temp_path = args.TempDir;
            await src_mux.GetMediaInfoAsync().ConfigureAwait(false);
            temp_path.NormalizePath().CreateDirectoryIfNotExists();
            if (!src_mux.HasSubtitles && !external_subtitles)
                throw new SushiException("Subtitles aren't specified");
            if (args.Output == null)
                args.Output = AppContext.BaseDirectory;
            else
                args.Output.NormalizePath().CreateDirectoryIfNotExists();
            AudioMedia srcm = src_mux.GetAudioStream(args.SrcAudio);
            srcm.SetPaths(args.TempDir, args.Output);

            AudioProvider src_audio = new AudioProvider(_reader, srcm, args.SampleRate, args.SampleType, args.Padding, args.Normalize, args.VoiceRemoval, args.DownMixStereo, AudioPostProcess.SubtitleSearch, temp_path);
            float duration = src_audio.Media.Info.Duration;

            List<SubtitleProvider> src_subtitles = GetSubtitles(args, src_mux);
            long srcms = 0;
            if (args.SrcKeyframes == null && !args.MakeSrcKeyframes)
            {
                srcms = await _probe.GetAudioDelayAsync(args.Src, src_mux.GetAudioIndex(args.SrcAudio)).ConfigureAwait(false);
            }

            try
            {
                List<(List<ComputedMovement>, List<Decoding.Media>)> decs = new List<(List<ComputedMovement>, List<Decoding.Media>)>();
                float delay = args.SubTime;
                List<Decoding.Media> unsort = src_subtitles.Select(a => a.Media).Cast<Decoding.Media>().ToList();
                if (unsort.Count > 0)
                    delay = delay - srcms;
                decs.Add((new List<ComputedMovement>(), unsort));
                (int width, int height) = GetWidthAndHeight(args, null);
                Script.Script script = await Script.Script.CreateScriptAsync(_demuxer, _logger, src_mux, decs.ToDictionary(a => a.Item1, a => a.Item2), delay, duration, width, height).ConfigureAwait(false);
                if (!dump)
                    return script;
                _logger.LogInformation($"Exporting script file to {args.ScriptFile}...");
                await File.WriteAllLinesAsync(args.ScriptFile, script.Serialize(args.AbsoluteTimes)).ConfigureAwait(false);
                return script;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }

        }
        public async Task<Script.Script> ResolveShiftSubtitlesAsync(SushiSettings args, Mux src_mux, Mux dst_mux, bool dump)
        {
            bool ignore_chapters = args.NoChapters;
            bool external_subtitles = args.Subtitle != null && args.Subtitle.Length > 0;
            string temp_path = args.TempDir;
            await src_mux.GetMediaInfoAsync().ConfigureAwait(false);
            await dst_mux.GetMediaInfoAsync().ConfigureAwait(false);
            temp_path.NormalizePath().CreateDirectoryIfNotExists();
            if (!src_mux.HasSubtitles && !external_subtitles)
                throw new SushiException("Subtitles aren't specified");
            if (args.Output == null)
                args.Output = AppContext.BaseDirectory;
            else
                args.Output.NormalizePath().CreateDirectoryIfNotExists();
            SetRescaleIfNeeded(src_mux, dst_mux, args);
            AudioMedia srcm =src_mux.GetAudioStream(args.SrcAudio);
            AudioMedia dstm =dst_mux.GetAudioStream(args.DstAudio);
            srcm.SetPaths(args.TempDir, args.Output);
            dstm.SetPaths(args.TempDir, args.Output);

            AudioProvider src_audio = new AudioProvider(_reader, srcm, args.SampleRate, args.SampleType, args.Padding, args.Normalize, args.VoiceRemoval, args.DownMixStereo, AudioPostProcess.SubtitleSearch, temp_path);
            AudioProvider dst_audio = new AudioProvider(_reader, dstm, args.SampleRate, args.SampleType, args.Padding, args.Normalize, args.VoiceRemoval, args.DownMixStereo, AudioPostProcess.SubtitleSearch, temp_path);
            float duration = dst_audio.Media.Info.Duration;

            List<SubtitleProvider> src_subtitles = GetSubtitles(args, src_mux);
            long srcms = 0;
            long dstms = 0;
            if (args.SrcKeyframes == null && !args.MakeSrcKeyframes)
            {
                srcms=await _probe.GetAudioDelayAsync(args.Src, src_mux.GetAudioIndex(args.SrcAudio)).ConfigureAwait(false);
                dstms=await _probe.GetAudioDelayAsync(args.Dst, dst_mux.GetAudioIndex(args.DstAudio)).ConfigureAwait(false);
            }
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
                _logger.LogInformation($"Providing Source Audio {src_audio.Media.ProcessPath}...");
                using (AudioStream src_stream = await src_audio.ObtainAsync().ConfigureAwait(false))
                {
                    _logger.LogInformation($"Providing Destination Audio {dst_audio.Media.ProcessPath}...");
                    using (AudioStream dst_stream = await dst_audio.ObtainAsync().ConfigureAwait(false))
                    {
                        if (chapters_src != null)
                        {
                            chapter_times = await chapters_src.ObtainAsync().ConfigureAwait(false);
                        }

                        if (args.SrcKeyframes != null || args.MakeSrcKeyframes)
                        {
                            _logger.LogInformation($"Loading Source Timecodes {timecodes_src.Media.ProcessPath}...");
                            src_timecodes = await timecodes_src.ObtainAsync().ConfigureAwait(false);
                            _logger.LogInformation($"Loading Source Keyframes {keyframes_src.Media.ProcessPath}...");
                            src_keytimes = (await keyframes_src.ObtainAsync().ConfigureAwait(false)).Select(a => src_timecodes.GetFrameTime(a)).ToList();
                            _logger.LogInformation($"Loading Destination Timecodes {timecodes_src.Media.ProcessPath}...");
                            dst_timecodes = await timecodes_dst.ObtainAsync().ConfigureAwait(false);
                            _logger.LogInformation($"Loading Destination Keyframes {keyframes_src.Media.ProcessPath}...");
                            dst_keytimes = (await keyframes_dst.ObtainAsync().ConfigureAwait(false)).Select(a => dst_timecodes.GetFrameTime(a)).ToList();
                        }

                        List<PeriodEvent> calcevents = new List<PeriodEvent>();
                        List<SubtitleProvider> overlay_providers = src_subtitles.ToList();
                        if (!string.IsNullOrEmpty(args.SubtitleStreams))
                        {
                            List<int> vals = new List<int>();
                            string[] split= args.SubtitleStreams.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string s in split)
                            {
                                if (int.TryParse(s, out int val))
                                    vals.Add(val);
                                else
                                {

                                    SubtitleProvider prov=src_subtitles.FirstOrDefault(a => a.Media.Info.Language!=null && a.Media.Info.Language.Contains(s));
                                    if (prov==null)
                                        prov= src_subtitles.FirstOrDefault(a => a.Media.Info.Title!=null && a.Media.Info.Title.Contains(s));
                                    if (prov != null)
                                        vals.Add(prov.Media.Info.Id);
                                }
                            }
                            if (vals.Count > 0)
                            {
                                overlay_providers = overlay_providers.Where(a => vals.Contains(a.Media.Info.Id)).ToList();
                            }
                        }
                        foreach (SubtitleProvider sub_provider in overlay_providers)
                        {
                            _logger.LogInformation($"Loading Subtitle {sub_provider.Media.ProcessPath}...");
                            IEvents sub = await sub_provider.ObtainAsync().ConfigureAwait(false);
                            foreach (Event ev in sub.Events)
                            {
                                if (ev.End<ev.Start)
                                    ev.End = ev.Start;
                                OverlayEvent(calcevents, new PeriodEvent { Start = ev.Start, End = ev.End });
                            }
                        }

                        List<Event> events = calcevents.Cast<Event>().OrderBy(a => a.Start).ToList();
                        List<List<Event>> search_groups = _grouping.PrepareSearchGroups(events,
                        src_stream.DurationInSeconds, chapter_times, args.MaxTsDuration, args.MaxTsDistance);
                        _logger.LogInformation($"Calculating Subtitle Shifts [with {args.Mode.Value.ToString()}]...");
                        await _shifter.CalculateShiftsAsync(src_stream, dst_stream, search_groups, args.Window, args.MaxWindow, !args.NoGrouping ? args.RewindThresh : 0, args.AllowedDifference, args.Mode.Value).ConfigureAwait(false);
                        List<List<Event>> groups = _grouping.GroupWithChapters(events, chapter_times, ignore_chapters, !args.NoGrouping, args.SmoothRadius, args.AllowedDifference, args.MaxGroupStd);
                        if (args.SrcKeyframes != null || args.MakeSrcKeyframes)
                        {
                            foreach (Event ev in events.Where(a => a.Linked))
                                ev.ResolveLink();
                            foreach (List<Event> g in groups)
                                _grouping.SnapGroupsToKeyFrames(g, chapter_times, args.MaxTsDuration, args.MaxTsDistance, src_keytimes, dst_keytimes, src_timecodes, dst_timecodes, args.MaxKFDistance, args.KfMode);
                        }

                        List<ComputedMovement> movements = new List<ComputedMovement>();
                        float last_shift = 0;
                        float total = 0;
                        foreach (List<Event> g in groups)
                        {
                            float avg_shift = Grouping.AverageShifts(g);
                            float r = avg_shift - last_shift;
                            if (r < 0.001 && r > -0.001)
                                r = 0;
                            ComputedMovement mov = new ComputedMovement();
                            mov.AbsolutePosition = g[0].Start;
                            
                            mov.Difference = r;
                            mov.RelativePosition = mov.AbsolutePosition+total;

                            if (r < 0)
                                total += r;
                            last_shift = avg_shift;
                            movements.Add(mov);
                        }

                        List<(List<ComputedMovement>, List<Decoding.Media>)> decs = new List<(List<ComputedMovement>, List<Decoding.Media>)>();
                        float delay = args.SubTime;
                        List<Decoding.Media> unsort = src_subtitles.Select(a => a.Media).Cast<Decoding.Media>().ToList();
                        if (unsort.Count > 0)
                            delay = delay - srcms + dstms;
                        decs.Add((movements, unsort));
                        decs.ForEach(a => a.Item2 = a.Item2.OrderBy(a => a.Info.Id).ToList());
                        decs.Reverse();
                        (int width, int height) = GetWidthAndHeight(args, dst_mux);
                        Script.Script script = await Script.Script.CreateScriptAsync(_demuxer, _logger, src_mux, decs.ToDictionary(a => a.Item1, a => a.Item2), delay, duration, width, height).ConfigureAwait(false);
                        if (!dump)
                            return script;
                        _logger.LogInformation($"Exporting script file to {args.ScriptFile}...");
                        await File.WriteAllLinesAsync(args.ScriptFile, script.Serialize(args.AbsoluteTimes)).ConfigureAwait(false);
                        return script;
                    }
                }   
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }

        }
        /*
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

            

            AudioProvider src_audio = new AudioProvider(_reader, src_mux, args.SrcAudio, args.SampleRate, args.SampleType, args.Padding, AudioPostProcess.SubtitleSearch, temp_path);
            AudioProvider dst_audio = new AudioProvider(_reader, dst_mux, args.DstAudio, args.SampleRate, args.SampleType, args.Padding, AudioPostProcess.SubtitleSearch, temp_path);
            List<SubtitleProvider> src_subtitles = new List<SubtitleProvider>();
            if ((args.SrcSubtitle==null && src_mux.MediaInfo.Subtitles.Count>0 && !external_subtitles) || (args.SrcSubtitle.HasValue && args.SrcSubtitle == -1 && src_mux.MediaInfo.Subtitles.Count>0))
                src_subtitles = src_mux.MediaInfo.Subtitles.Select(a => new SubtitleProvider(src_mux, a.Id, temp_path, args.Output.Normalize())).ToList();
            else if (args.SrcSubtitle.HasValue && src_mux.MediaInfo.Subtitles.Count>0)
                src_subtitles.Add(new SubtitleProvider(src_mux, args.SrcSubtitle.Value, temp_path, args.Output.NormalizePath()));
            if (external_subtitles)
                src_subtitles.AddRange(args.Subtitle.Select(a => new SubtitleProvider(a, args.Output.NormalizePath())));
            long srcms = 0;
            long dstms = 0;
            if (args.SrcKeyframes == null && !args.MakeSrcKeyframes)
            {
                srcms=await _probe.GetAudioDelayAsync(args.Src, src_mux.GetAudioIndex(args.SrcAudio)).ConfigureAwait(false);
                dstms=await _probe.GetAudioDelayAsync(args.Dst, dst_mux.GetAudioIndex(args.DstAudio)).ConfigureAwait(false);
            }

          

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
                            _logger.LogInformation($"Calculating Subtitle Shifts [with {args.Mode.Value.ToString()}] for {sub_provider.Path}...");
                            await _shifter.CalculateShiftsAsync(src_stream, dst_stream, search_groups, args.Window, args.MaxWindow, !args.NoGrouping ? args.RewindThresh : 0, args.AllowedDifference, args.Mode.Value).ConfigureAwait(false);
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

                            args.SubTime += -srcms + dstms;
                            if (args.SubTime != 0)
                            {
                                _logger.LogInformation($"Applying subtitle time shift {args.SubTime}ms.");
                                events.ForEach(a => a.ApplyTime(args.SubTime/1000f));
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
        */

    }
}