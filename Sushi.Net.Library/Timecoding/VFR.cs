using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Timecoding
{
    public class VFR : ITimeCodes
    {
        private List<float> _times;
        private float? _default_frame_duration;

        public VFR(List<float> times, float? default_fps)
        {
            _times = times;
            _default_frame_duration = default_fps.HasValue ? (1.0f / default_fps.Value) : (float?)null;
        }

        public static VFR CreateFromText(string text)
        {
            List<string> lines = text.SplitLines().ToList();
            if (lines.Count == 0)
                return new VFR(new List<float>(),null);
            string first = lines[0].ToLowerInvariant().LeftStrip();
            lines.RemoveAt(0);
            if (first.StartsWith("# timecode format v2") || first.StartsWith("# timestamp format v2"))
            {
                return new VFR((lines.Select(a=>Convert.ToSingle(a)/1000.0f)).ToList(), null);
            }

            if (first.StartsWith("# timecode format v1"))
            {
                float def = Convert.ToSingle(lines[0].ToLowerInvariant().Replace("assume", string.Empty));
                lines.RemoveAt(0);
                List<string[]> overrides = lines.Select(a => a.Split(',').ToArray()).ToList();
                return new VFR(ConvertToV2(def, overrides), def);
            }

            throw new SushiException("This timecodes format is not supported");
        }

        public static async Task<VFR> CreateFromFileAsync(string file)
        {
            return CreateFromText(await file.ReadAllTextAsync().ConfigureAwait(false));
        }
        public float GetFrameTime(long number)
        {
            if (number >= 0 && number < _times.Count)
                return _times[(int)number];
            if (!_default_frame_duration.HasValue)
                return _times[^1];
            if (_times != null && _times.Count > 0)
                return _times[^1] + _default_frame_duration.Value * (number - _times.Count + 1);
            return number * _default_frame_duration.Value;
        }

        public int GetFrameNumber(float timestamp)
        {
            if (_times == null || _times.Count == 0 || _times[^1] < timestamp)
            {
                if (_default_frame_duration.HasValue)
                {
                    float calc = timestamp;
                    if (_times != null && _times.Count > 0)
                        calc -= _times.Sum();
                    calc /= _default_frame_duration.Value;
                    return Convert.ToInt32(calc);
                }

                return -1;
            }

            return _times.BisectLeft(timestamp);
        }
        public float? GetFrameSize(float timestamp)
        {
            if (_times == null || _times.Count == 0 || _times[^1] < timestamp)
                return _default_frame_duration;
            int number = _times.BisectLeft(timestamp);
            float t1 = GetFrameTime(number);
            float t2;
            if (number == _times.Count)
            {
                t2 = GetFrameTime(number - 1);
                return t1 - t2;
            }

            t2 = GetFrameTime(number + 1);
            return t2 - t1;
        }

        private static List<float> ConvertToV2(float default_fps, List<string[]> overrides)
        {
            List<(int, int, float)> overs = overrides.Select(a => (Convert.ToInt32(a[0]), Convert.ToInt32(a[1]), Convert.ToSingle(a[2]))).ToList();
            if (overs.Count == 0)
                return new List<float>();
            float[] fps = Enumerable.Repeat(default_fps, overs[^1].Item2 + 1).ToArray();
            foreach ((int, int, float) o in overs)
            {
                for(int x=o.Item1;x<=o.Item2+1;x++)
                    fps[x] = o.Item3;
            }

            List<float> ret = new List<float>() {0f};
            foreach(float d in fps)
                ret.Add(ret[^1]+(1.0f/d));
            return ret;
        }




    }
}
