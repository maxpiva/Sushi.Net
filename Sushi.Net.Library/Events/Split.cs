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
}