using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Decoding
{
    public class AudioMedia : Media
    {
        public int? AudioRate { get; set; }
        public List<(float start, float end)> Silences { get; set; }
        public bool Normalize { get; private set; } = true;
        public bool FindSilences { get; private set; }
        public AudioPostProcess AudioProcess { get; private set; }
        public bool VoiceRemoval { get; private set; }
        public float SilenceLength { get; private set; }
        public int SilenceThreshold { get; private set; }
        public bool DownMixStereo { get; private set; }
        public float NormalizeGain { get; set; }

        public AudioMedia(Mux mux, MediaStreamInfo info) : base(mux, info)
        {
        }
        public Task ShiftAudioAsync(List<IShiftBlock> blocks, string temppath, float minmalms)
        {
            return Demuxer.ShiftAudioAsync(this, OutputPath, blocks, temppath, minmalms);
        }
        public void SetAudioProcessing(int? sample_rate, bool normalize, AudioPostProcess process=AudioPostProcess.SubtitleSearch, bool voiceRemoval=false, bool downmix_stereo=false)
        {
            AudioRate = sample_rate;
            AudioProcess = process;
            ShouldProcess = true;
            Normalize = normalize;
            VoiceRemoval = voiceRemoval;
            DownMixStereo = downmix_stereo;
        }

        public void SetSilenceSearch(float silence_length, int silence_threshold)
        {
            FindSilences = true;
            SilenceLength = silence_length;
            SilenceThreshold = silence_threshold;
        }

        internal static Dictionary<MediaStreamInfo, string> GenerateLanguages(IEnumerable<MediaStreamInfo> media)
        {
            string? frinv = null, eninv = null, esinv = null, ptinv = null;
            Dictionary<MediaStreamInfo, string> ret = media.ToDictionary(a => a, a => a.Language);
            foreach (MediaStreamInfo info in ret.Keys)
            {
                string title = info.Title?.Trim();
                string language = ret[info];
                if (string.IsNullOrEmpty(title))
                    continue;
                if (title.Contains("Latin American", StringComparison.InvariantCultureIgnoreCase) &&
                    (language == "spa" || language == "es"))
                {
                    language = "es-419";
                    esinv = "es-ES";
                }

                if ((title.Contains("European", StringComparison.InvariantCultureIgnoreCase) ||
                     title.Contains("Castilian", StringComparison.InvariantCultureIgnoreCase)) &&
                    (language == "spa" || language == "es"))
                {
                    language = "es-ES";
                    esinv = "es-419";
                }

                if (title.Contains("European", StringComparison.InvariantCultureIgnoreCase) &&
                    (language == "pt" || language == "por"))
                {
                    language = "pt-PT";
                    ptinv = "pt-BR";
                }

                if (title.Contains("Brazilian", StringComparison.InvariantCultureIgnoreCase) &&
                    (language == "pt" || language == "por"))
                {
                    language = "pt-BR";
                    ptinv = "pt-PT";
                }

                if (title.Contains("American", StringComparison.InvariantCultureIgnoreCase) &&
                    (language == "fr" || language == "fra"))
                {
                    language = "fr-CA";
                    frinv = "fr-FR";
                }

                if (title.Contains("European", StringComparison.InvariantCultureIgnoreCase) &&
                    (language == "fr" || language == "fra"))
                {
                    language = "fr-FR";
                    frinv = "fr-CA";
                }

                if ((title.Contains("United Kingdom", StringComparison.InvariantCultureIgnoreCase) ||
                     title.Contains("England", StringComparison.InvariantCultureIgnoreCase)) &&
                    (language == "en" || language == "eng"))
                {
                    language = "en-UK";
                    eninv = "en-US";
                }

                ret[info] = language;
            }

            foreach (MediaStreamInfo info in ret.Keys)
            {
                string title = info.Title?.Trim();
                string language = ret[info];
                if (string.IsNullOrEmpty(title))
                    continue;
                if ((language == "es" || language == "spa") && esinv != null)
                    ret[info] = esinv;
                if ((language == "pt" || language == "por") && ptinv != null)
                    ret[info] = ptinv;
                if ((language == "fr" || language == "fra") && frinv != null)
                    ret[info] = frinv;
                if ((language == "en" || language == "eng") && eninv != null)
                    ret[info] = eninv;
            }

            return ret;
        }


        private string GeneratePostfix(bool with_out = false)
        {
            string extension = Info.Extension;
            if (!string.IsNullOrEmpty(Mux.AudioOutputCodec))
            {
                extension = "." + Mux.AudioOutputCodec;
                OutputCodec = Mux.AudioOutputCodec;
            }
            if (!string.IsNullOrEmpty(Mux.AudioOutputCodecParameters))
            {
                OutputParameters = Mux.AudioOutputCodecParameters;
            }
            if (!extension.StartsWith("."))
                extension = "." + extension;
            Dictionary<int, StringBuilder> outs = new Dictionary<int, StringBuilder>();
            for (int x = 0; x < Mux.Audios.Count; x++)
                outs.Add(Mux.Audios[x].Info.Id, new StringBuilder());
            Dictionary<MediaStreamInfo, string> langs = GenerateLanguages(Mux.Audios.Where(a => outs.Keys.Contains(a.Info.Id)).Select(a => a.Info));

            foreach (MediaStreamInfo info in langs.Keys)
            {
 
                if (!string.IsNullOrWhiteSpace(info.Title))
                {
                    outs[info.Id].Append("_");
                    outs[info.Id].Append(info.Title.Trim());
                }
                if (info.Default)
                {
                    outs[info.Id].Append(".default");
                }
                if (info.Forced)
                {
                    outs[info.Id].Append(".forced");
                }

                if (info.Comment)
                {
                    outs[info.Id].Append(".commentary");
                }
                if (info.HearingImpaired)
                {
                    outs[info.Id].Append(".sdh");
                }
                if (!string.IsNullOrEmpty(info.Language))
                {
                    outs[info.Id].Append(".");
                    outs[info.Id].Append(langs[info]);
                }


            }

            int tcnt = outs.Select(a => a.Value.ToString().ToLowerInvariant()).Distinct().Count();
            if (outs.Count != tcnt)
            {
                int cnt = (int)Math.Log10(Mux.Audios.Count) + 1;
                foreach (int x in outs.Keys)
                {
                    outs[x].Insert(0, string.Format("_{0:D" + cnt + "}", x));
                }
            }

            if (with_out)
                outs[Info.Id].Insert(0, "_out");
            return outs[Info.Id].ToString() + extension;
        }
        public override void SetPaths(string tempPath, string outputpath)
        {
            ProcessPath = Mux.Path.FormatFullPath($"_{Info.Id}.wav", tempPath);
            OutputCodec = Info.CodecName;
            if (outputpath != null && outputpath.ToLowerInvariant() != System.IO.Path.GetDirectoryName(Mux.Path))
                OutputPath = Mux.Path.FormatFullPath(GeneratePostfix(), outputpath);
            else
                OutputPath = Mux.Path.FormatFullPath(GeneratePostfix(true));
        }
    }
}