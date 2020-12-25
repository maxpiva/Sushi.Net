using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Media;
using Thinktecture.Extensions.Configuration;

namespace Sushi.Net.Library.Tools
{
    

    public class FFMpeg : Tool
    {
        public const string ffmpeg = "ffmpeg";
        private static readonly Regex AudioRegex = new Regex(@"Stream\s\#0:(\d+)(?:\((.*?)\))?.*?Audio:\s*(.*?(?:\((default)\))?)\s*?(?:\(forced\))?\r?\n(?:\s*Metadata:\s*\r?\n\s*title\s*:\s*(.*?)\r?\n)?", RegexOptions.Compiled);
        private static readonly Regex VideoRegex = new Regex(@"Stream\s\#0:(\d+).*?Video:\s*(.*?(?:,\s(\d+)x(\d+)).*?(?:\((default)\))?)\s*?(?:\(forced\))?\r?\n(?:\s*Metadata:\s*\r?\n\s*title\s*:\s*(.*?)\r?\n)?", RegexOptions.Compiled);
        private static readonly Regex SubtitleRegex = new Regex(@"Stream\s\#0:(\d+)(?:\((.*?)\))?.*?Subtitle:\s*((\w*)\s*?(?:\((default)\))?\s*?(?:\(forced\))?)\r?\n(?:\s*Metadata:\s*\r?\n\s*title\s*:\s*(.*?)\r?\n)?", RegexOptions.Compiled);
        private static readonly Regex ChapterRegex = new Regex(@"Chapter #0.\d+: start (\d+\.\d+)", RegexOptions.Compiled);


        private static readonly Regex DurationRegex = new Regex(@"^\s+?DURATION.*?:(.*?)(\n|,|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentRegex = new Regex(@"size.*?\stime=(.*?)\s",RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public FFMpeg(ILogger<FFMpeg> logger, IProgressLoggerFactory fact, IGlobalCancellation cancel, ILoggingConfiguration cfg) : base(logger, fact, cancel, cfg, ffmpeg)
        {
        }

        public Task<string> AnalyzeAsync(string file)
        {
            CheckIfRequired(false);
            Command cmd = Command.WithArguments("-hide_banner -i " + file.Quote()).WithValidation(CommandResultValidation.None);
            return ExecuteAsync(cmd);
        }

        private static readonly Regex VolumeRegex = new Regex(@"\[Parsed_volumedetect.*?max_volume:(.*?)dB",RegexOptions.Compiled);
        private static readonly Regex SilenceRegex = new Regex("\\[silencedetect.*?silence_start:(.*?)\n.*?silence_end:(.*?)\\|",RegexOptions.Compiled|RegexOptions.Singleline);

        private static string filtervoice = "bandreject=\"f=900:width_type=h:w=600\"";
        //private static string invertright = "pan=\"stereo|c0=c0|c1=-1*c1\"";
      
        public async Task<(List<(float start, float end)>,float vol)> FindSilencesAsync(string file, int? index, float silence_length, int silence_threshold)
        {
            CheckIfRequired(false);
            _logger.LogInformation("Finding maximum volume...");
            Command cmd = Command.WithArguments("-hide_banner -i " + file.Quote()+" -af volumedetect -f null -").WithValidation(CommandResultValidation.None);
            string res=await ExecuteAsync(cmd,true, new FFMpegPercentageProcessor()).ConfigureAwait(false);
            Match vol = VolumeRegex.Match(res);
            if (!vol.Success)
                throw new SushiException($"Unable to find {file} volume");
            float val = -float.Parse(vol.Groups[1].Value.Trim());
            _logger.LogInformation($"Volume {-val}db. Finding silences...");
            cmd = Command.WithArguments($"-hide_banner -i " + file.Quote() + $" -af volume=volume={val.ToString(CultureInfo.InvariantCulture)}dB,silencedetect=noise={silence_threshold}dB:d={silence_length.ToString(CultureInfo.InvariantCulture)} -f null -").WithValidation(CommandResultValidation.None);
            res=await ExecuteAsync(cmd, true, new FFMpegPercentageProcessor()).ConfigureAwait(false);
            MatchCollection coll = SilenceRegex.Matches(res);
            return (coll.Select(a => (float.Parse(a.Groups[1].Value), float.Parse(a.Groups[2].Value))).ToList(), val);
        }

        public async Task ShiftAudioAsync(Mux mux, string path, List<Split> splits)
        {
            try
            {
                CheckIfRequired(false);
                List<string> args = new List<string>();
                args.Add("-hide_banner -i " + mux.Path.Quote() + " -y");
                args.Add($"-map 0:{mux.AudioStream.Id}");
                StringBuilder bld = new StringBuilder();
                bld.Append("-filter_complex \"");
                int scnt = 0;
                foreach (Split spl in splits.Where(a => a.IsSilence))
                {
                    long ustart = (long) (spl.DstStart * 1000000);
                    long uend = (long) (spl.DstEnd * 1000000);
                    bld.Append($"anullsrc,atrim=0:{uend - ustart}us,asetpts=PTS-STARTPTS[s{scnt}];");
                    scnt++;
                }

                int acnt = 0;
                foreach (Split spl in splits.Where(a => !a.IsSilence))
                {
                    long ustart = (long) (spl.SrcStart * 1000000);
                    long uend = (long) (spl.SrcEnd * 1000000);
                    bld.Append($"[0]atrim={ustart}us:{uend}us,asetpts=PTS-STARTPTS[a{acnt}];");
                    acnt++;
                }

                acnt = 0;
                scnt = 0;
                foreach (Split spl in splits)
                {
                    if (spl.IsSilence)
                    {
                        bld.Append($"[s{scnt}]");
                        scnt++;
                    }
                    else
                    {
                        bld.Append($"[a{acnt}]");
                        acnt++;
                    }
                }

                bld.Append("concat=n=" + (scnt + acnt) + ":v=0:a=1\"");
                args.Add(bld.ToString());
                args.Add(path.Quote());
                string arguments = string.Join(" ", args);
                Command cmd = Command.WithArguments(arguments);
                await ExecuteAsync(cmd,true,new FFMpegPercentageProcessor()).ConfigureAwait(false);
            }
            catch

            {
                throw new SushiException("Couldn't invoke ffmpeg, check that it's installed");
            }
        }


        public async Task DeMux(Mux mux)
        {
            try
            {
                CheckIfRequired(false);
                List<string> args = new List<string>();
                args.Add("-hide_banner -i " + mux.Path.Quote() + " -y");
                if (mux.AudioStream != null)
                {
                    args.Add($"-map 0:{mux.AudioStream.Id}");

                    List<string> filters = new List<string>();
                    if (mux.AudioProcess==AudioPostProcess.AudioSearch)
                        filters.Add(filtervoice);
                    if (filters.Count>0)
                        args.Add("-af "+string.Join(",",filters));
                    if (mux.AudioRate.HasValue)
                        args.Add($"-ar {mux.AudioRate.Value}");
                    args.Add("-ac 1 -acodec pcm_s16le " + mux.AudioPath.Quote());
                }

                if (mux.ScriptStreams != null)
                {
                    for(int x=0;x<mux.ScriptStreams.Count;x++)
                    {
                        args.Add($"-map 0:{mux.ScriptStreams[x].Id} " + mux.ScriptPaths[x].Quote());

                    }
                }

                if (!string.IsNullOrEmpty(mux.TimeCodesPath))
                    args.Add($"-map 0:{mux.VideoStream.Id} -f mkvtimestamp_v2 " + mux.TimeCodesPath.Quote());
                string arguments = string.Join(" ", args);
                Command cmd = Command.WithArguments(arguments);
                await ExecuteAsync(cmd,true,new FFMpegPercentageProcessor()).ConfigureAwait(false);
            }
            catch
            {
                throw new SushiException("Couldn't invoke ffmpeg, check that it's installed");
            }
        }

        private static List<MediaStreamInfo> GetAudioStreams(string info)
        {
            return AudioRegex.Matches(info).Where(a => a.Success).Select(MediaStreamInfo.FromAudio).ToList();
        }

        private static List<MediaStreamInfo> GetVideoStreams(string info)
        {
            return VideoRegex.Matches(info).Where(a => a.Success).Select(MediaStreamInfo.FromVideo).ToList();
        }

        private static List<SubtitleStreamInfo> GetSubtitlesStreams(string info)
        {
            return SubtitleRegex.Matches(info).Where(a => a.Success).Select(a => new SubtitleStreamInfo(a)).ToList();
        }

        private static Chapters GetChapters(string info)
        {
            return new Chapters(ChapterRegex.Matches(info).Where(a => a.Success));
        }

        public async Task<MediaInfo> GetMediaInfoAsync(string file)
        {
            string info = await AnalyzeAsync(file).ConfigureAwait(false);
            MediaInfo m = new MediaInfo();
            m.Videos = GetVideoStreams(info);
            m.Audios = GetAudioStreams(info);
            m.Subtitles = GetSubtitlesStreams(info);
            m.Chapters = GetChapters(info);
            return m;
        }

        public class FFMpegPercentageProcessor : IPercentageProcessor
        {
            private float _duration;
            private int _lastval = 0;
            public int PercentageFromLine(string line)
            {
                Match duration = DurationRegex.Match(line);
                if (duration.Success)
                {
                    _duration = duration.Groups[1].Value.Trim().ParseAssTime();
                    _lastval = 0;
                }
                else
                {
                    Match time = CurrentRegex.Match(line);
                    if (time.Success && _duration>0)
                    {
                        float r=time.Groups[1].Value.Trim().ParseAssTime();
                        r = r * 100 / _duration;
                        _lastval = (int) Math.Round(r);
                    }
                }
                return Math.Min(_lastval,100);
            }

            public void Init()
            {
                _lastval = 0;
            }
        }
        

    }
}