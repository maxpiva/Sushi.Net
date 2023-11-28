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
        public int? AudioRate { get; private set; }
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
        public Task ShiftAudioAsync(List<IShiftBlock> blocks, string temppath)
        {
            return Demuxer.ShiftAudioAsync(this, OutputPath, blocks, temppath);
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
            foreach (int idx in outs.Keys)
            {
                MediaStreamInfo info = (MediaStreamInfo)Mux.Audios.First(a => a.Info.Id == idx).Info;
                if (!string.IsNullOrWhiteSpace(info.Title))
                {
                    outs[idx].Append("_");
                    outs[idx].Append(info.Title.Trim());
                }
                if (info.Default)
                {
                    outs[idx].Append(".default");
                }
                if (info.Forced)
                {
                    outs[idx].Append(".forced");
                }

                if (info.Comment)
                {
                    outs[idx].Append(".commentary");
                }
                if (info.HearingImpaired)
                {
                    outs[idx].Append(".sdh");
                }
                if (!string.IsNullOrEmpty(info.Language))
                {
                    outs[idx].Append(".");
                    outs[idx].Append(info.Language);
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