using System.Globalization;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events
{
    public class Split
    {
        public float SrcStart { get; set; }
        public float SrcEnd { get; set; }
        public bool IsSilence { get; set; }
        public float DstStart { get; set; }
        public float DstEnd { get; set; }

    }

    public interface IShiftBlock
    {
    }

    public class WarnBlock : Block
    {
        public int Warning { get; set;}
    }
    public class Block : IShiftBlock
    {
        public float Start { get; set; }
        public float End { get; set; }
         
        public bool IsMove { get; set; }
        public float Shift { get; set; }
        public override string ToString()
        {
            return Start.FormatTime2() + " " + End.FormatTime2() + " " + Shift.ToString(CultureInfo.InvariantCulture);
        }
    }

    public class SilenceBlock : IShiftBlock
    {
        public float Duration { get; set; }
    }
}