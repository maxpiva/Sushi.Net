using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Providers
{
    public class KeyframesProvider : Provider<List<long>>
    {

        public  KeyframesProvider(Mux original, string path, bool make, string temp_path)
        {
            if (make && (original==null || !original.HasVideo))
                throw new SushiException($"Cannot make keyframes from {original?.Path ?? path ?? "'null'"} because it doesn't have any video!");
            if (make)
            {
                Path = original.Path.FormatFullPath("_sushi_keyframes.txt", temp_path);
                original.SetKeyframes(Path);
                RequireDemuxing = true;
            }
            else
                Path = path;
        }

        public override async Task<List<long>> ObtainAsync()
        {
            await CheckExistance().ConfigureAwait(false);
            string text = await Path.ReadAllTextAsync().ConfigureAwait(false);
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
            throw new SushiException($"Unsupported keyframes type in file {Path}.");
        }
    }
}