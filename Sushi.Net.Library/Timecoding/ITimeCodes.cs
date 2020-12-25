namespace Sushi.Net.Library.Timecoding
{
    public interface ITimeCodes
    {
        public float GetFrameTime(long number);
        public int GetFrameNumber(float timestamp);
        public float? GetFrameSize(float timestamp);
    }
}