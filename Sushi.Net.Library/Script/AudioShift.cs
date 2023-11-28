using System.Collections.Generic;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;

namespace Sushi.Net.Library.Script
{
    public class AudioShift
    {
        public List<IShiftBlock> Blocks { get; set;}
        public AudioMedia Media { get; set;}
        public float Duration { get; set; }
    }
}