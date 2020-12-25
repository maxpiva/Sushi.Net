using System;

namespace Sushi.Net.Library.Timecoding
{
    public class CFR : ITimeCodes
    {
        private readonly float _frame_duration;

        public CFR(float fps)
        {
            _frame_duration = 1.0F / fps;
        }


        public float GetFrameTime(long number)
        {
            return number * _frame_duration;
        }

        public int GetFrameNumber(float timestamp)
        {
            return Convert.ToInt32(timestamp / _frame_duration);
        }

        public float? GetFrameSize(float timestamp)
        {
            return _frame_duration;
        }
    }
}