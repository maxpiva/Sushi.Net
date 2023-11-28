using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Decoding
{
    public class SubtitleMedia : Media
    {

        public SubtitleMedia(Mux mux, MediaStreamInfo info) : base(mux, info)
        {
        }

        public SubtitleMedia(string file) : base(null, new MediaStreamInfo())
        {
            ProcessPath = file;
            Info.Id = -1;
            Info.MediaType = MediaStreamType.Subtitle;
            Info.Extension = Path.GetExtension(file);
            Info.CodecName = Info.Extension.ToCodec();
            Processed = true;
        }

        public override void SetPaths(string tempPath, string output_path)
        {
            if (Mux != null)
            {
                ProcessPath = Mux.Path.FormatFullPath(GeneratePostfix(), tempPath);
                if (output_path != null && output_path.ToLowerInvariant() != System.IO.Path.GetDirectoryName(Mux.Path))
                    OutputPath = Mux.Path.FormatFullPath(GeneratePostfix(), output_path);
                else
                    OutputPath = Mux.Path.FormatFullPath(GeneratePostfix(true));
            }
            else
            {
                if (output_path != null && output_path.ToLowerInvariant() != System.IO.Path.GetDirectoryName(ProcessPath))
                    OutputPath = ProcessPath.FormatFullPath(ProcessPath.GetExtension(), output_path);
                else
                    OutputPath = ProcessPath.FormatFullPath("_out"+ProcessPath.GetExtension(), output_path);
            }

        }
        private string GeneratePostfix(bool with_out=false)
        {

            Dictionary<int, StringBuilder> outs = new Dictionary<int, StringBuilder>();
            for(int x=0;x<Mux.Subtitles.Count;x++)
                outs.Add(Mux.Subtitles[x].Info.Id, new StringBuilder());
            foreach (int idx in outs.Keys)
            {
                MediaStreamInfo info = (MediaStreamInfo)Mux.Subtitles.First(a => a.Info.Id == idx).Info;
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
                int cnt=(int)Math.Log10(Mux.Subtitles.Count)+1;
                foreach(int x in outs.Keys)
                {
                    outs[x].Insert(0, string.Format("_{0:D" + cnt + "}", x));
                }
            }

            if (with_out)
                outs[Info.Id].Insert(0, "_out");
            return outs[Info.Id].ToString()+Info.Extension;
        }
    }
}