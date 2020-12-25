using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Providers
{
    public class SubtitleProvider : Provider<IEvents>
    {

        public string OutputPath { get; }
        
        public override async Task<IEvents> ObtainAsync()
        {
            await CheckExistance().ConfigureAwait(false);
            return await IEvents.CreateFromFileAsync(Path).ConfigureAwait(false);
        }

        public SubtitleProvider(Mux original, int index, string temp_path, string outputPath)
        {
            if (!original.MediaInfo.Subtitles.Any(a => a.Id == index))
                throw new SushiException($"Invalid subtitle index {index}.");
            Mux = original;
            Path = original.Path.FormatFullPath(GeneratePostfix(index), temp_path);
            if (outputPath != null && outputPath.ToLowerInvariant()!=System.IO.Path.GetDirectoryName(original.Path))
                OutputPath = original.Path.FormatFullPath(GeneratePostfix(index), outputPath);
            else
                OutputPath = original.Path.FormatFullPath(GeneratePostfix(index,true));
            original.SetScript(index,Path);
            RequireDemuxing = true;
        }
        public SubtitleProvider(string path, string outputPath)
        {
            if (outputPath != null && outputPath.ToLowerInvariant() != System.IO.Path.GetDirectoryName(path))
                OutputPath = path.FormatFullPath(path.GetExtension(), outputPath);
            else
                OutputPath = path.FormatFullPath("_out"+path.GetExtension(), outputPath);
            Mux = null;
            Path=path;
            RequireDemuxing = false;
        }

        private string GeneratePostfix(int index, bool with_out=false)
        {
            Dictionary<int, StringBuilder> outs = new Dictionary<int, StringBuilder>();
            for(int x=0;x<Mux.MediaInfo.Subtitles.Count;x++)
                outs.Add(Mux.MediaInfo.Subtitles[x].Id, new StringBuilder());
            Dictionary<int, string> titles = Mux.MediaInfo.Subtitles.ToDictionary(a => a.Id, a => a.Title);
            Dictionary<int, string> languages = Mux.MediaInfo.Subtitles.ToDictionary(a => a.Id, a => a.Language);
            foreach (int idx in outs.Keys)
            {
                SubtitleStreamInfo info = Mux.MediaInfo.Subtitles.First(a => a.Id == idx);
                if (!string.IsNullOrEmpty(info.Title))
                {
                    outs[idx].Append("_");
                    outs[idx].Append("title");
                    if (!string.IsNullOrEmpty(info.Language))
                    {
                        outs[idx].Append("_");
                        outs[idx].Append(info.Language);
                    }
                }
                else if (!string.IsNullOrEmpty(info.Language))
                {
                    outs[idx].Append("_");
                    outs[idx].Append(info.Language);
                }
            }

            string[] res = outs.Select(a => a.Value.ToString()).ToArray();
            if (res.Length == res.Select(a => a.ToLowerInvariant().Distinct()).Count() && res[0]!=string.Empty)
            {
                return res[index]+"."+Mux.MediaInfo.Subtitles.First(a => a.Id==index).Type;
            }
            int cnt=(int)Math.Log10(Mux.MediaInfo.Subtitles.Count)+1;
            foreach(int x in outs.Keys)
            {
                outs[x].Append(string.Format("_{0:D" + cnt + "}", x));
            }
            if (with_out)
                 return outs[index].ToString()+"_out"+Mux.MediaInfo.Subtitles.First(a => a.Id==index).Type;
            return outs[index].ToString()+Mux.MediaInfo.Subtitles.First(a => a.Id==index).Type;
        }
        
        
    }
}