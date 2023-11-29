using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using OpenCvSharp;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Events.Audio;
using Sushi.Net.Library.Media;
using Thinktecture.Extensions.Configuration;
using static Sushi.Net.Library.Events.Shifter;

namespace Sushi.Net.Library.Tools
{
    public class FFMpeg : Tool
    {
        public const string ffmpeg = "ffmpeg";
        //private static readonly Regex AudioRegex = new Regex(@"Stream\s\#0:(\d+)(?:\((.*?)\))?.*?Audio:\s*(.*?(?:\((default)\))?)\s*?(?:\(forced\))?\r?\n(?:\s*Metadata:\s*\r?\n\s*title\s*:\s*?(.*?)\r?\n)?", RegexOptions.Compiled);
        //private static readonly Regex VideoRegex = new Regex(@"Stream\s\#0:(\d+).*?Video:\s*(.*?(?:,\s(\d+)x(\d+)).*?(?:\((default)\))?)\s*?(?:\(forced\))?\r?\n(?:\s*Metadata:\s*\r?\n\s*title\s*:\s*?(.*?)\r?\n)?", RegexOptions.Compiled);
        //private static readonly Regex SubtitleRegex = new Regex(@"Stream\s\#0:(\d+)(?:\((.*?)\))?.*?Subtitle:\s*((\w*)\s*?(?:\((default)\))?\s*?(?:\(forced\))?)\r?\n(?:\s*Metadata:\s*\r?\n\s*title\s*:\s*?(.*?)\r?\n)?", RegexOptions.Compiled);
        //private static readonly Regex ChapterRegex = new Regex(@"Chapter #0.\d+: start (\d+\.\d+)", RegexOptions.Compiled);


        private static readonly Regex DurationRegex = new Regex(@"^\s+?DURATION.*?:(.*?)(\n|,|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CurrentRegex = new Regex(@"size.*?\stime=(.*?)\s",RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static readonly Regex BaseDurationRegex = new Regex(@"Duration:\s?(.*?),",RegexOptions.Compiled);
        public FFMpeg(ILogger<FFMpeg> logger, IProgressLoggerFactory fact, IGlobalCancellation cancel, ILoggingConfiguration cfg) : base(logger, fact, cancel, cfg, ffmpeg)
        {
        }


        public Task<string> AnalyzeAsync(string file)
        {
            CheckIfRequired(false);
            Command cmd = Command.WithArguments("-hide_banner -i " + file.Quote()).WithValidation(CommandResultValidation.None);
            return ExecuteAsync(cmd, true,null, Encoding.UTF8);
        }

        private static readonly Regex VolumeRegex = new Regex(@"\[Parsed_volumedetect.*?max_volume:(.*?)dB",RegexOptions.Compiled);
        private static readonly Regex SilenceRegex = new Regex("\\[silencedetect.*?silence_start:(.*?)\n.*?silence_end:(.*?)\\|",RegexOptions.Compiled|RegexOptions.Singleline);

        private static string filtervoice = "bandreject=\"f=900:width_type=h:w=600\"";

        private static string downmux = "pan=\"1c|c0=0.5*FL+0.5*FR\"";
        //private static string invertright = "pan=\"stereo|c0=c0|c1=-1*c1\"";
      /*
        public async Task<(List<(float start, float end)>,float vol)> FindSilencesAsync(string file, int? index, float silence_length, int silence_threshold)
        {
            CheckIfRequired(false);
            _logger.LogInformation("Finding maximum volume...");
            Command cmd = Command.WithArguments("-hide_banner -i " + file.Quote()+" -vn -sn -dn -af volumedetect -f null -").WithValidation(CommandResultValidation.None);
            string res=await ExecuteAsync(cmd,true, new FFMpegPercentageProcessor()).ConfigureAwait(false);
            Match vol = VolumeRegex.Match(res);
            if (!vol.Success)
                throw new SushiException($"Unable to find {file} volume");
            float val = -float.Parse(vol.Groups[1].Value.Trim());
            _logger.LogInformation($"Volume {-val}db. Finding silences...");
            cmd = Command.WithArguments($"-hide_banner -i " + file.Quote() + $" -vn -sn -dn -af volume=volume={val.ToString(CultureInfo.InvariantCulture)}dB,silencedetect=noise={silence_threshold}dB:d={silence_length.ToString(CultureInfo.InvariantCulture)} -f null -").WithValidation(CommandResultValidation.None);
            res=await ExecuteAsync(cmd, true, new FFMpegPercentageProcessor()).ConfigureAwait(false);
            MatchCollection coll = SilenceRegex.Matches(res);
            return (coll.Select(a => (float.Parse(a.Groups[1].Value), float.Parse(a.Groups[2].Value))).ToList(), val);
        }
        */

      public async Task ShiftAudioAsync(Decoding.AudioMedia stream, string path, List<IShiftBlock> blocks, string temppath)
      {
            try
            {
                NumPercentageProcessor proc = new NumPercentageProcessor();
                string p = temppath;
                if (string.IsNullOrEmpty(p))
                {
                    p = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString().Replace("-", ""));
                    Directory.CreateDirectory(p);
                }
                CheckIfRequired(false);
                List<string> dellist=new List<string>();
                string joint = Path.Combine(p, Path.GetFileNameWithoutExtension(path) + ".script");
                dellist.Add(joint);
                int cnt = 0;
                StringBuilder parts = new StringBuilder();
                double acc = 100d / ((double)blocks.Count+1);
                double ct = 0;
                foreach (IShiftBlock b in blocks)
                {
                    string filename = Path.GetFileNameWithoutExtension(path);
                    string ext = Path.GetExtension(path);
                    string current = Path.Combine(p, filename + $"_{cnt:000}" + ext);
                    StringBuilder bld = new StringBuilder();
                    SilenceBlock s = b as SilenceBlock;
                    Block bb = b as Block;
                    if (s != null)
                    {
                        bld.Append("-y -f lavfi -i anullsrc");
                        long duration = (long)(s.Duration * 1000000);
                        if (stream.Info.SampleRate.HasValue || !string.IsNullOrEmpty(stream.Info.ChannelLayout))
                        {
                            bld.Append("=");
                        }
                        if (!string.IsNullOrEmpty(stream.Info.ChannelLayout))
                        {
                            bld.Append("channel_layout=" + stream.Info.ChannelLayout);
                            if (stream.Info.SampleRate.HasValue)
                                bld.Append(":");
                        }
                        if (stream.Info.SampleRate.HasValue)
                        {
                            bld.Append("sample_rate=" + stream.Info.SampleRate.Value);
                        }
                        bld.Append($" -c:a {stream.Info.CodecName}");
                        if (stream.Info.BitRate != 0)
                            bld.Append($" -b:a {stream.Info.BitRate}");
                        bld.Append($" -t {duration}us ");
                        bld.Append(current.Quote());
                    }
                    else if (bb != null && bb.Start<bb.End)
                    {
                        bld.Append("-hide_banner -i " + stream.Mux.Path.Quote() + " -y ");
                        bld.Append($"-map 0:a:{stream.Mux.Audios.IndexOf(stream)} ");
                        long ustart = (long)(bb.Start * 1000000);
                        long uend = (long)(bb.End * 1000000);
                        bld.Append("-ss " + ustart + "us -to " + uend + "us -c:a copy ");
                        bld.Append(current.Quote());
                    }
                    ct += acc;
                    proc.SetValue((int)ct);
                    if (bld.Length > 0)
                    {
                        cnt++;
                        _logger.LogDebug("ffmpeg args: " + bld.ToString());
                        Command cmd = Command.WithArguments(bld.ToString());
                        await ExecuteAsync(cmd, true, proc).ConfigureAwait(false);
                        parts.AppendLine("file " + current.Quote('\''));
                        dellist.Add(current);
                    }
                }
                File.WriteAllText(joint, parts.ToString());
                string concat = "-hide_banner -y -safe 0 -f concat -i " + joint.Quote() + " -c copy " + path.Quote();
                _logger.LogDebug("ffmpeg args: " + concat);
                Command cmd2 = Command.WithArguments(concat);
                proc.SetValue(100);
                await ExecuteAsync(cmd2, true, proc).ConfigureAwait(false);
                foreach(string s in dellist)
                {
                    try
                    {
                        File.Delete(s);
                    }
                    catch(Exception e)
                    {

                    }
                }
                if (string.IsNullOrEmpty(temppath))
                {
                    try
                    {
                        Directory.Delete(p, true);
                    }
                    catch (Exception e)
                    {

                    }

                }

          }
          catch(Exception e)

          {
                _logger.LogError("Exception Error: "+e.ToString(),e);
                throw new SushiException("Couldn't invoke ffmpeg, check that it's installed");
          }
      }



        public async Task ShiftAudioAsync(AudioMedia stream, string path, List<Split> splits)
        {
            try
            {
                CheckIfRequired(false);
                List<string> args = new List<string>();
                args.Add("-hide_banner -i " + stream.ProcessPath.Quote() + " -y");
                args.Add($"-map 0:{stream.Info.Id}");
                StringBuilder bld = new StringBuilder();
                bld.Append("-acodec " + stream.OutputCodec+" -filter_complex \"");
                int scnt = 0;
                foreach (Split spl in splits.Where(a => a.IsSilence))
                {
                    long ustart = (long) (spl.DstStart * 1000000);
                    long uend = (long) (spl.DstEnd * 1000000);
                    bld.Append("anullsrc");
                    if (stream.Info.SampleRate.HasValue || !string.IsNullOrEmpty(stream.Info.ChannelLayout))
                    {
                        bld.Append("=");
                    }
                    if (!string.IsNullOrEmpty(stream.Info.ChannelLayout))
                    {
                        bld.Append("channel_layout=" + stream.Info.ChannelLayout+":");
                    }
                    if (stream.Info.SampleRate.HasValue)
                    {
                        bld.Append("sample_rate="+stream.Info.SampleRate.Value);
                    }
                    bld.Append($",atrim=0:{uend - ustart}us,asetpts=PTS-STARTPTS[s{scnt}];");
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
                //bld.Append(" -acodec copy");
                args.Add(bld.ToString());
                args.Add(path.Quote());
                string arguments = string.Join(" ", args);
                Command cmd = Command.WithArguments(arguments);
                await ExecuteAsync(cmd,true,new FFMpegPercentageProcessor()).ConfigureAwait(false);
            }
            catch(Exception e)

            {
                _logger.LogError("Exception Error: " + e.ToString(), e);
                throw new SushiException("Couldn't invoke ffmpeg, check that it's installed");
            }
        }
        internal class StartEnd
        {
            public float Start { get; set; }
            public float End { get; set; }
        }
        private async Task<List<(float start, float end)>> FindSilences(string file, float db, float silence_length, int silence_threshold)
        {
            _logger.LogInformation($"Volume {db}db. Finding silences...");
            Command cmd = Command.WithArguments($"-hide_banner -i " + file.Quote() + $" -vn -sn -dn -af volume={db.ToString(CultureInfo.InvariantCulture)}dB,silencedetect=noise={silence_threshold}dB:d={silence_length.ToString(CultureInfo.InvariantCulture)} -f null -").WithValidation(CommandResultValidation.None);
            string res=await ExecuteAsync(cmd, true, new FFMpegPercentageProcessor()).ConfigureAwait(false);
            MatchCollection coll = SilenceRegex.Matches(res);
            List<StartEnd> silences=coll.Select(a => new StartEnd { Start = float.Parse(a.Groups[1].Value), End = float.Parse(a.Groups[2].Value) }).ToList();
            bool restart = false;
            do
            {
                restart = false;
                for (int x = 1; x < silences.Count; x++)
                {
                    if ((silences[x].Start - silences[x - 1].End) <= 0.00015d)
                    {
                        silences[x - 1].End = silences[x].End;
                        silences.Remove(silences[x]);
                        restart = true;
                        break;
                    }
                }
            } while(restart);
            return silences.Select(a=>(a.Start, a.End)).ToList();
        }


        private async Task<float> FindMaxVolume(string path)
        {
            _logger.LogInformation("Finding maximum volume...");
            Command cmd = Command.WithArguments("-hide_banner -i " + path.Quote()+" -vn -sn -dn -af volumedetect -f null -").WithValidation(CommandResultValidation.None);
            string res=await ExecuteAsync(cmd,true, new FFMpegPercentageProcessor()).ConfigureAwait(false);
            Match vol = VolumeRegex.Match(res);
            if (!vol.Success)
                throw new SushiException($"Unable to find {path} volume");
            return -float.Parse(vol.Groups[1].Value.Trim());
        }

        private async Task NormalizeAudio(string infile, string outfile, int? rate, float db)
        {
            _logger.LogInformation("Normalizing Audio...");
            List<string> args = new List<string>();
            args.Add("-hide_banner -i " + infile.Quote() + " -y");
            args.Add($"-vn -sn -dn -af volume=volume={db.ToString(CultureInfo.InvariantCulture)}dB");
            if (rate.HasValue)
                args.Add($"-ar {rate.Value}");
            args.Add("-acodec pcm_s16le " + outfile.Quote());
            string arguments = string.Join(" ", args);
            Command cmd = Command.WithArguments(arguments);
            await ExecuteAsync(cmd, true, new FFMpegPercentageProcessor()).ConfigureAwait(false);
        }
        public async Task DeMux(Mux mux)
        {
            try
            {

                List<string> args = new List<string>();
                args.Add("-hide_banner -i " + mux.Path.Quote() + " -y");
                CheckIfRequired(false);
                Dictionary<string, AudioMedia> audios = new Dictionary<string, AudioMedia>();
                foreach (AudioMedia s in mux.Audios.Where(a => a.ShouldProcess))
                {
                    int? rate = s.AudioRate;
                    string outpath = s.ProcessPath;
                    if (s.Normalize)
                    {
                        rate = 44000;
                        outpath = (s.ProcessPath + "_temp.wav");
                    }

                    args.Add($"-map 0:{s.Info.Id}");
                    List<string> filters = new List<string>();
                    if (s.VoiceRemoval)
                        filters.Add(filtervoice);
                    if (s.DownMixStereo)
                        filters.Add(downmux);
                    if (filters.Count > 0)
                        args.Add("-af " + string.Join(",", filters));
                    if (rate.HasValue)
                        args.Add($"-ar {rate.Value}");
                    args.Add("-ac 1 -acodec pcm_s16le " + outpath.Quote());
                    audios.Add(outpath, s);
                }
                foreach (SubtitleMedia s in mux.Subtitles.Where(a => a.ShouldProcess))
                {
                    args.Add($"-map 0:{s.Info.Id} " + s.ProcessPath.Quote());
                    s.Processed = true;
                }
                if (!string.IsNullOrEmpty(mux.TimeCodesPath))
                    args.Add($"-map 0:{mux.Videos.First().Info.Id} -f mkvtimestamp_v2 " + mux.TimeCodesPath.Quote());
                string arguments = string.Join(" ", args);
                //_logger.LogInformation("Args: "+arguments);
                Command cmd = Command.WithArguments(arguments);
                await ExecuteAsync(cmd,true,new FFMpegPercentageProcessor()).ConfigureAwait(false);
                foreach (string outpath in audios.Keys)
                {
                    AudioMedia s = audios[outpath];
                    if (s.Normalize || s.FindSilences)
                        s.NormalizeGain = await FindMaxVolume(outpath).ConfigureAwait(false);
                    if (s.FindSilences)
                        s.Silences = await FindSilences(outpath, s.NormalizeGain, s.SilenceLength, s.SilenceThreshold).ConfigureAwait(false);
                    if (s.Normalize)
                    {
                        await NormalizeAudio(outpath, s.ProcessPath, s.AudioRate, s.NormalizeGain).ConfigureAwait(false);
                        File.Delete(outpath.Strip());
                    }
                    s.Processed = true;
                }
              
            }
            catch(Exception e)
            {
                _logger.LogError("Exception Error: " + e.ToString(), e);
                throw new SushiException("Couldn't invoke ffmpeg, check that it's installed");
            }
        }



        public class NumPercentageProcessor : IPercentageProcessor
        {
            int val = 0;
            public NumPercentageProcessor()
            {

            }
            public int PercentageFromLine(string line)
            {
                return val;
            }

            public void Init()
            {
            }
            public void SetValue(int value)
            {
                val = value;
            }
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