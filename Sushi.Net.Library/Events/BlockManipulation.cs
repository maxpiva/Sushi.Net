using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sushi.Net.Library.Audio;

namespace Sushi.Net.Library.Events
{
    public class BlockManipulation
    {
        private class ExpandState
        {
            public float ExpandStart { get; set; }
            public float ExpandEnd { get; set; }
            public Event Event { get; set; }
        }
        
        public List<(float start, float end)> ReduceSilences(List<(float start, float end)> silence, float duration, float time = 0.15f)
        {
            List<(float, float)> result = new List<(float, float)>();
            foreach ((float start, float end) in silence)
            {
                float s = start;
                float e = end;
                if (s > 0)
                    s += time;
                if (e < duration)
                    e -= time;
                if (s < 0)
                    s = 0;
                if (e > duration)
                    e = duration;
                result.Add((s, e));
            }

            return result;
        }

        public List<Split> CreateSplits(List<Event> events, float duration)
        {
            List<Split> splits = events.Select(a => new Split { DstStart = a.Start, DstEnd = a.End, SrcStart = a.Start + a.Shift, SrcEnd = a.End + a.Shift }).OrderBy(a => a.DstStart).ToList();
            List<Split> ret = new List<Split>();
            float previous = 0;
            foreach (Split s in splits)
            {
                if (s.DstStart > previous)
                {
                    Split spl = new Split();
                    spl.DstStart = previous;
                    spl.DstEnd = s.DstStart;
                    spl.IsSilence = true;
                    ret.Add(spl);
                }

                ret.Add(s);
                previous = s.DstEnd;
            }

            if (previous < duration)
            {
                Split spl = new Split();
                spl.DstStart = previous;
                spl.DstEnd = duration;
                spl.IsSilence = true;
                ret.Add(spl);
            }

            return ret;
        }

        public void ExpandBorders(List<Event> events, AudioStream stream, float min_length = 0.1f, int threshold = -50)
        {
            List<ExpandState> states = events.Select(a => new ExpandState { Event = a, ExpandStart = a.Start, ExpandEnd = a.End }).OrderBy(a => a.ExpandStart).ToList();
            for (int x = 0; x < states.Count; x++)
            {
                float margin_left = 0;
                if (x > 1)
                    margin_left = states[x - 1].ExpandEnd;
                float margin_right = stream.DurationInSeconds;
                if (x < states.Count - 1)
                    margin_right = states[x + 1].ExpandStart;
                if (margin_left < states[x].ExpandStart)
                    states[x].ExpandStart = stream.FindSilence(states[x].ExpandStart + states[x].Event.Shift, margin_left + states[x].Event.Shift, min_length, threshold) - states[x].Event.Shift;
                if (margin_right > states[x].ExpandEnd)
                    states[x].ExpandEnd = stream.FindSilence(states[x].ExpandEnd + states[x].Event.Shift, margin_right + states[x].Event.Shift, min_length, threshold) - states[x].Event.Shift;
            }

            for (int x = 0; x < states.Count - 1; x++)
            {
                if (states[x].ExpandEnd > states[x + 1].ExpandStart)
                {
                    double original_difference = states[x + 1].Event.Start - states[x].Event.End;
                    double end_diff = states[x].ExpandEnd - states[x].Event.End;
                    double start_diff = states[x + 1].Event.Start - states[x + 1].ExpandStart;
                    double new_difference = end_diff + start_diff;
                    double diff = original_difference / new_difference;
                    end_diff *= diff;
                    start_diff *= diff;
                    states[x].ExpandEnd = states[x].Event.End + (float)end_diff;
                    states[x].ExpandStart = states[x].ExpandStart - (float)start_diff;
                }
            }

            foreach (ExpandState exp in states)
            {
                exp.Event.End = exp.ExpandEnd;
                exp.Event.Start = exp.ExpandStart;
            }
        }

        
    }
}
