using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;

namespace Sushi.Net.Library.Script
{
    public class SubtitleShift
    {
        public SubtitleMedia SubtitleMedia { get; set; }
        public IEvents Events { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float SubDelay { get; set;}
        public float Duration { get; set; }
    }
}