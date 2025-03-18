using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Providers
{
    public class ChapterProvider : Provider<List<float>, DummyMedia>
    {
        public ChapterProvider(Mux original, string path)
        {
            Media = new DummyMedia(original);
            Media.ProcessPath = path;
            if (string.IsNullOrEmpty(Media.ProcessPath))
                Media.ShouldProcess = true;
        }

        public override async Task<List<float>> ObtainAsync()
        {
            List<float> results;

            if (string.IsNullOrEmpty(Media.ProcessPath))
                return Media.Mux.Chapters?.Times ?? new List<float>();
            await CheckExistance().ConfigureAwait(false);
            if ((System.IO.Path.GetExtension(Media.ProcessPath) ?? "").ToLowerInvariant() == ".xml")
                results = (await Chapters.CreateFromFileXMLAsync(Media.ProcessPath).ConfigureAwait(false)).Times;
            else
                results = (await Chapters.CreateFromFileOGMAsync(Media.ProcessPath).ConfigureAwait(false)).Times;
            if (Media.Mux.ReScale != 0)
            {
                double scale = 1 / Media.Mux.ReScale;
                for (int i = 0; i < results.Count; i++)
                    results[i] *= (float)scale;

            }
            return results;
        }
    }
}