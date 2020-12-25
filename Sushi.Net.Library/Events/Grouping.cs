using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.LibIO;
using Sushi.Net.Library.Timecoding;

namespace Sushi.Net.Library.Events
{
    public class Grouping
    {
        private readonly ILogger _logger;

        public Grouping(ILogger<Grouping> logger)
        {
            _logger = logger;
        }

        public List<List<Event>> DetectGroups(List<Event> events, float allowed_error)
        {
            List<List<Event>> groups = new List<List<Event>>();
            if (events.Count == 0)
                return groups;
            groups.Add(new List<Event> {events[0]});
            if (events.Count == 1)
                return groups;
            for (int x = 1; x < events.Count; x++)
            {
                Event ev = events[x];
                if (ev.Shift.AbsDiff(groups[^1][^1].Shift) > allowed_error)
                    groups.Add(new List<Event>());
                groups[^1].Add(ev);
            }

            return groups;
        }

        public List<List<Event>> SplitBrokenGroups(List<List<Event>> groups, float allowed_error, float max_group_std)
        {
            List<List<Event>> correct_groups = new List<List<Event>>();
            bool broken_found = false;
            foreach (List<Event> g in groups)
            {
                float std = g.Select(a => a.Shift).ToArray().Std();
                if (std > max_group_std)
                {
                    _logger.LogWarning($"Shift is not consistent between {g[0].Start.FormatTime()} and {g[^1].End.FormatTime()}, most likely chapters are wrong (std: {std}).\nSwitching to automatic grouping.");
                    correct_groups = correct_groups.Union(DetectGroups(g, allowed_error)).ToList();
                    broken_found = true;
                }
                else
                    correct_groups.Add(g);
            }

            if (broken_found)
            {
                List<List<Event>> corrected = new List<List<Event>>() {correct_groups[0]};
                for (int x = 1; x < correct_groups.Count; x++)
                {
                    List<Event> group = correct_groups[x];
                    if (corrected[^1][^1].Shift.AbsDiff(@group[0].Shift) >= allowed_error || corrected[^1].Union(group).Select(a => a.Shift).ToArray().Std() >= max_group_std)
                        corrected.Add(new List<Event>());
                    corrected[^1].AddRange(group);
                }

                correct_groups = corrected;
            }

            return correct_groups;
        }

        public float GetDistanceToClosestKF(float timestamp, List<float> keyframes)
        {
            int idx = keyframes.BisectLeft(timestamp);
            float kf;
            if (idx == 0)
                kf = keyframes[0];
            else if (idx == keyframes.Count)
                kf = keyframes[^1];
            else
            {
                float before = keyframes[idx - 1];
                float after = keyframes[idx];
                kf = (after - timestamp < timestamp - before) ? after : before;
            }

            return kf - timestamp;
        }

        private float? GetDistance(float src_distance, float dst_distance, float limit)
        {
            if (Math.Abs(dst_distance) > limit)
                return null;
            float shift = dst_distance - src_distance;
            return Math.Abs(shift) < limit ? shift : (float?) null;
        }


        public (float, float) FindKeyframesDistances(Event evnt, List<float> src_keytimes, List<float> dst_keytimes, ITimeCodes timecodes, float max_kf_distance)
        {
            return (FindKeyframeDistance(evnt.Start, evnt.ShiftedStart, src_keytimes, dst_keytimes, timecodes, max_kf_distance), FindKeyframeDistance(evnt.End, evnt.ShiftedEnd, src_keytimes, dst_keytimes, timecodes, max_kf_distance));
        }


        public (float?, float?) FindKeyFrameShift(List<Event> group, List<float> src_keytimes, List<float> dst_keytimes, ITimeCodes src_timecodes, ITimeCodes dst_timecodes, float max_kf_distance)
        {
            float src_start = GetDistanceToClosestKF(group[0].Start, src_keytimes);
            float src_end = GetDistanceToClosestKF(group[^1].End + src_timecodes.GetFrameSize(group[^1].End) ?? 0, src_keytimes);
            float dst_start = GetDistanceToClosestKF(group[0].ShiftedStart, dst_keytimes);
            float dst_end = GetDistanceToClosestKF(group[^1].ShiftedEnd + dst_timecodes.GetFrameSize(group[^1].End) ?? 0, dst_keytimes);
            float snapping_limit_start = (src_timecodes.GetFrameSize(group[0].Start) ?? 0) * max_kf_distance;
            float snapping_limit_end = (src_timecodes.GetFrameSize(group[0].End) ?? 0) * max_kf_distance;
            return (GetDistance(src_start, dst_start, snapping_limit_start), GetDistance(src_end, dst_end, snapping_limit_end));
        }

        private float FindKeyframeDistance(float src_time, float dst_time, List<float> src_keytimes, List<float> dst_keytimes, ITimeCodes timecodes, float max_kf_distance)
        {
            float src = GetDistanceToClosestKF(src_time, src_keytimes);
            float dst = GetDistanceToClosestKF(dst_time, dst_keytimes);
            float snapping_limit = (timecodes.GetFrameSize(src_time) ?? 0) * max_kf_distance;
            if (Math.Abs(src) < snapping_limit && Math.Abs(dst) < snapping_limit && Math.Abs(src - dst) < snapping_limit)
                return dst - src;
            return 0;
        }


        private List<float> InterpolateNones(List<float?> data, List<float> points)
        {
            List<(float, float)> values = new List<(float, float)>();
            List<float> zero_points = new List<float>();
            for (int x = 0; x < data.Count; x++)
            {
                if (data[x] != null)
                    values.Add((points[x], data[x].Value));
                else
                    zero_points.Add(points[x]);
            }

            if (values.Count < 2)
                return new List<float>();
            
            if (zero_points.Count == 0)
                return data.Select(a => a.Value).ToList();
            zero_points.Sort();
            values = values.OrderBy(a => a.Item1).ToList();
            List<float> results = Interpolate(zero_points, values.Select(a=>a.Item1).ToArray(), values.Select(a=>a.Item2).ToArray());
            for (int x = 0; x < zero_points.Count; x++)
                values.Add((zero_points[x],results[x]));
            return values.OrderBy(a=>a.Item1).Select(a=>a.Item2).ToList();
        }
        private List<float> Interpolate(List<float> req, float[] x, float[] y)
        {
            List<float> d = new List<float>();
            for (int i = 0; i < x.Length-1; i++)
            {
                d.Add((y[i+1]-y[i])/(x[i+1]-x[i]));
            }

            List<float> r = new List<float>();
            foreach (float t in req)
            {
                int idx = Array.BinarySearch(x, t);
                if (idx < 0)
                    idx = ~idx - 1;
                int m = Math.Min(Math.Max(idx, 0), x.Length - 2);
                r.Add(d[m] + (t - x[m]) * d[m]);
            }

            return r;
        }

        private List<List<Event>> MergeShortLinesIntoGroups(List<Event> events, List<float> chapter_times, float max_ts_duration, float max_ts_distance)
        {
            List<List<Event>> search_groups = new List<List<Event>>();
            Stack<float> chapters = new Stack<float>(chapter_times.Skip(1).Union(new[] {100000000f}));

            float next_chapter = chapters.Pop();
            HashSet<int> processed = new HashSet<int>();
            for (int x = 0; x < events.Count; x++)
            {
                if (processed.Contains(x))
                    continue;
                Event evnt = events[x];
                while (evnt.End > next_chapter)
                    next_chapter = chapters.Pop();
                if (evnt.Duration > max_ts_duration)
                {
                    search_groups.Add(new List<Event>() {evnt});
                    processed.Add(x);
                }
                else
                {
                    List<Event> group = new List<Event>();
                    group.Add(evnt);
                    float group_end = evnt.End;
                    int i = x + 1;
                    while (i < events.Count && Math.Abs(group_end - events[i].Start) < max_ts_distance)
                    {
                        if (events[i].End < next_chapter && events[i].Duration <= max_ts_duration)
                        {
                            processed.Add(i);
                            group.Add(events[i]);
                            group_end = Math.Max(group_end, events[i].End);
                        }

                        i++;
                    }

                    search_groups.Add(group);
                }
            }

            return search_groups;
        }

        public void SnapGroupsToKeyFrames(List<Event> events, List<float> chapter_times, float max_ts_duration, float max_ts_distance, List<float> src_keytimes, List<float> dst_keytimes, ITimeCodes src_timecodes, ITimeCodes dst_timecodes, float max_kf_distance, KFMode kf_mode)
        {
            if (max_kf_distance == 0)
                return;

            List<List<Event>> groups = MergeShortLinesIntoGroups(events, chapter_times, max_ts_duration, max_ts_distance);
            if (kf_mode == KFMode.All || kf_mode == KFMode.Shift)
            {
                List<float?> preshifts = new List<float?>();
                List<float> times = new List<float>();
                foreach (List<Event> group in groups)
                {
                    (float?, float?) sh = FindKeyFrameShift(group, src_keytimes, dst_keytimes, src_timecodes, dst_timecodes, max_kf_distance);
                    preshifts.Add(sh.Item1);
                    preshifts.Add(sh.Item2);
                    times.Add(group[0].ShiftedStart);
                    times.Add(group[^1].ShiftedEnd);
                }

                List<float> shifts = InterpolateNones(preshifts, times);
                if (shifts.Count > 0)
                {
                    float mean_shift = shifts.ToArray().Mean();
                    List<(float start, float end)> fl = new();
                    for (int x = 0; x < shifts.Count; x += 2)
                    {
                        fl.Add((shifts[x],shifts[x+1]));
                    }
                    _logger.LogInformation($"Group {events[0].Start.FormatTime()}-{events[^1].End.FormatTime()} corrected by {mean_shift}");
                    int cnt = 0;
                    foreach (List<Event> group in groups)
                    {
                        float start_shift = fl[cnt].start;
                        float end_shift = fl[cnt].end;
                        if (Math.Abs(start_shift - end_shift) > 0.001 && group.Count > 1)
                        {
                            float m1 = Math.Abs(start_shift - (float) mean_shift);
                            float m2 = Math.Abs(end_shift - (float) mean_shift);
                            float m3 = Math.Min(m1, m2);
                            float actual_shift = m3 == m1 ? start_shift : end_shift;
                            _logger.LogWarning($"Typesetting group at {group[0].Start.FormatTime()} had different shift at start/end points ({start_shift} and {end_shift}). Shifting by {actual_shift}.");
                            group.ForEach(a => a.AdjustShift(actual_shift));
                        }
                        else
                            group.ForEach(a => a.AdjustAdditionalShifts(start_shift, end_shift));
                    }
                }
            }

            if (kf_mode == KFMode.All || kf_mode == KFMode.Snap)
            {
                foreach (List<Event> group in groups)
                {
                    if (group.Count > 1)
                        continue;
                    (float start_shift, float end_shift) = FindKeyframesDistances(group[0], src_keytimes, dst_keytimes, src_timecodes, max_kf_distance);
                    if (Math.Abs(start_shift) > 0.01 || Math.Abs(end_shift) > 0.01)
                    {
                        _logger.LogInformation($"Snapping {group[0].Start.FormatTime()} to keyframes, start time by {start_shift}, end: {end_shift}");
                        group[0].AdjustAdditionalShifts(start_shift, end_shift);
                    }
                }
            }
        }


        public List<List<Event>> GroupsFromChapters(List<Event> events, List<float> times)
        {
            List<List<Event>> groups = new List<List<Event>>();
            groups.Add(new List<Event>());
            List<float> chapter_times = times.Skip(1).ToList();
            chapter_times.Add(36000000000);
            int cnt = 0;
            foreach (Event ev in events)
            {
                if (ev.End > chapter_times[cnt])
                {
                    groups.Add(new List<Event>());
                    while (ev.End > chapter_times[cnt])
                        cnt++;
                }

                groups[^1].Add(ev);
            }

            groups = groups.Where(a => a.Count > 0).ToList();
            List<List<Event>> broken_groups = groups.Where(a => a.All(b => b.Linked)).ToList();
            if (broken_groups.Count > 0)
            {
                foreach (List<Event> group in broken_groups)
                {
                    foreach (Event ev in group)
                    {
                        Event parent = ev.GetLinkChainEnd();
                        groups.First(a => a.Contains(parent)).Add(ev);
                    }

                    group.Clear();
                }

                groups = groups.Where(a => a.Count > 0).ToList();
                groups = groups.Select(a => a.OrderBy(b => b.Start).ToList()).ToList();
            }

            return groups;
        }


        public List<float> Running_Median(float[] values, int window_size)
        {
            if (window_size % 2 == 0)
                throw new SushiException("Median window size should be odd");
            int half_window = window_size / 2;
            List<float> medians = new List<float>();
            for (int x = 0; x < values.Length; x++)
            {
                int radius = Math.Min(Math.Min(half_window, x), values.Length - x - 1);
                medians.Add((float) values.Slice(x - radius, x + radius + 1).Median());
            }

            return medians;
        }


        public void SmoothEvents(List<Event> events, int radius)
        {
            if (radius == 0)
                return;
            int window_size = radius * 2 + 1;
            List<float> smoothed = Running_Median(events.Select(a => a.Shift).ToArray(), window_size);
            for (int x = 0; x < smoothed.Count; x++)
                events[x].SetShift(smoothed[x], events[x].Diff);
        }

        private int FixBorder(List<Event> events, float media_diff)
        {
            int count = 10;
            if (events.Count < 100)
                count = events.Count / 10;
            if (count == 0)
                return 0;
            double last_ten_diff = events.Skip(events.Count - count).Select(a => a.Diff).ToArray().Median();
            float diff_limit = Math.Min((float) last_ten_diff, media_diff);
            List<Event> broken = new List<Event>();
            foreach (Event evnt in events)
            {
                float delta = evnt.Diff / diff_limit;
                if (delta < 0.2 || delta > 5)
                    broken.Add(evnt);
                else
                {
                    foreach (Event rv in broken)
                        rv.LinkEvent(evnt);
                    return broken.Count;
                }
            }

            return 0;
        }


        public void Fix_Near_Borders(List<Event> events)
        {
            double median_diff = events.Select(a => a.Diff).ToArray().Median();
            int fixed_count = FixBorder(events, (float) median_diff);
            if (fixed_count > 0)
                _logger.LogInformation($"Fixing {fixed_count} border events right after {events[0].Start.FormatTime()}");
            fixed_count = FixBorder(events.Select(a => a).Reverse().ToList(), (float) median_diff);
            if (fixed_count > 0)
                _logger.LogInformation($"Fixing {fixed_count} border events right before {events[^1].Start.FormatTime()}");
        }


        public float AverageShifts<T>(List<T> events) where T : Event
        {
            List<T> nevents = events.Where(a => !a.Linked).ToList();
            if (nevents.Count == 0)
                return events.FirstOrDefault()?.Diff ?? 0f;
            float[] shifts = nevents.Select(a => a.Shift).ToArray();
            float[] weights = nevents.Select(a => 1 - a.Diff).ToArray();
            double average = shifts.Average(weights);
            nevents.ForEach(a => a.SetShift((float) average, a.Diff));
            return (float) average;
        }


        public List<List<Event>> GroupWithChapters(List<Event> events, List<float> chapter_times, bool ignore_chapters, bool grouping, int smooth_radius, float allowedDistance, float max_group_std)
        {
            if (grouping)
            {
                List<List<Event>> groups;
                if (!ignore_chapters && chapter_times.Count > 0)
                {
                    _logger.LogInformation($"Chapter start points: {string.Join(",", chapter_times.Select(a => a.FormatTime()))}");
                    groups = GroupsFromChapters(events, chapter_times);
                    foreach (List<Event> g in groups)
                    {
                        Fix_Near_Borders(g);
                        SmoothEvents(g.Where(a => !a.Linked).ToList(), smooth_radius);
                    }

                    groups = SplitBrokenGroups(groups, allowedDistance, max_group_std);
                }
                else
                {
                    Fix_Near_Borders(events);
                    SmoothEvents(events.Where(a => !a.Linked).ToList(), smooth_radius);
                    groups = DetectGroups(events, allowedDistance);
                }

                foreach (List<Event> g in groups)
                {
                    float start_shift = g[0].Shift;
                    float end_shift = g[^1].Shift;
                    float avg_shift = AverageShifts(g);
                    _logger.LogInformation($"Group (start: {g[0].Start.FormatTime()}, end: {g[^1].End.FormatTime()}, lines: {g.Count}), shifts (start: {start_shift}, end: {end_shift}, average: {avg_shift})");
                }

                return groups;
            }
            else
                Fix_Near_Borders(events);
            return new List<List<Event>> {events};
        }


        public List<List<Event>> PrepareSearchGroups(List<Event> events, float source_duration, List<float> chapter_times, float max_ts_duration, float max_ts_distance)
        {
            Event last_unliked = null;
            for (int x = 0; x < events.Count; x++)
            {
                Event evnt = events[x];
                if (evnt.IsComment)
                {
                    evnt.LinkEvent(x + 1 != events.Count ? events[x + 1] : last_unliked);
                    continue;
                }

                if (evnt.Start + evnt.Duration / 2.0 > source_duration)
                {
                    _logger.LogInformation($"Event time outside of audio range, ignoring: {evnt}");
                    evnt.LinkEvent(last_unliked);
                    continue;
                }

                if (evnt.End == evnt.Start)
                {
                    _logger.LogInformation($"{evnt.Start.FormatTime()}: skipped because zero duration");
                    evnt.LinkEvent(x + 1 != events.Count ? events[x + 1] : last_unliked);
                    continue;
                }

                if (last_unliked != null && last_unliked.Start == evnt.Start && last_unliked.End == evnt.End)
                    evnt.LinkEvent(last_unliked);
                else
                    last_unliked = evnt;
            }

            List<List<Event>> search_groups = MergeShortLinesIntoGroups(events.Where(a => !a.Linked).ToList(), chapter_times, max_ts_duration, max_ts_distance);
            List<List<Event>> parsed_groups = new List<List<Event>>();
            for (int x = 0; x < search_groups.Count; x++)
            {
                List<Event> group = search_groups[x];
                List<Event> other = search_groups.Take(x).Reverse().FirstOrDefault(a => a[0].Start <= group[0].Start && a[^1].End >= group[^1].End);
                if (other != null)
                    group.ForEach(a => a.LinkEvent(other[0]));
                else
                    parsed_groups.Add(group);
            }

            return parsed_groups;
        }
    }
}