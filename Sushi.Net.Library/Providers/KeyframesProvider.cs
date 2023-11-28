using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Providers
{
    public class KeyframesProvider : Provider<List<long>, DummyMedia>
    {

        public  KeyframesProvider(Mux original, string path, bool make, string temp_path)
        {
            Media = new DummyMedia(original);
            if (make && (original==null || original.Videos.Count==0))
                throw new SushiException($"Cannot make keyframes from {original?.Path ?? path ?? "'null'"} because it doesn't have any video!");
            if (make)
            {
                Media.ProcessPath = original.Path.FormatFullPath("_sushi_keyframes.txt", temp_path);
                original.SetKeyframes(Media.ProcessPath);

            }
            else
                Media.ProcessPath = path;
        }

        public override async Task<List<long>> ObtainAsync()
        {
            await CheckExistance().ConfigureAwait(false);
            string text = await Media.ProcessPath.ReadAllTextAsync().ConfigureAwait(false);
            if (text.Contains("# XviD 2pass stat file"))
            {
                List<long> frames = new List<long>();
                string[] lines = text.SplitLines().ToArray();
                for (long x = 0; x < lines.Length; x++)
                {
                    if (string.IsNullOrEmpty(lines[x]))
                        continue;
                    if (lines[x][0] == 'i')
                        frames.Add(x - 3);
                }
                if (frames[0] != 0)
                    frames.Insert(0, 0);
                return frames;
            }
            throw new SushiException($"Unsupported keyframes type in file {Media.ProcessPath}.");
        }
    }
}