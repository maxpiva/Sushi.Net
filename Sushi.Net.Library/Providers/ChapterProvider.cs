using System.Collections.Generic;
using System.Threading.Tasks;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Providers
{
    public class ChapterProvider : Provider<List<float>>
    {
        public ChapterProvider(Mux original, string path)
        {
            
            Mux = original;
            Path = path;
        }

        public override async Task<List<float>> ObtainAsync()
        {
            if (string.IsNullOrEmpty(Path))
                return Mux.MediaInfo.Chapters.Times;
            await CheckExistance().ConfigureAwait(false);
            if ((System.IO.Path.GetExtension(Path) ?? "").ToLowerInvariant() == ".xml")
                return (await Chapters.CreateFromFileXMLAsync(Path).ConfigureAwait(false)).Times;
            return (await Chapters.CreateFromFileOGMAsync(Path).ConfigureAwait(false)).Times;
        }
    }
}