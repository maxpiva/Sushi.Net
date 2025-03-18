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
            Dictionary<MediaStreamInfo, string> langs = AudioMedia.GenerateLanguages(Mux.Subtitles.Where(a => outs.Keys.Contains(a.Info.Id)).Select(a => a.Info));

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