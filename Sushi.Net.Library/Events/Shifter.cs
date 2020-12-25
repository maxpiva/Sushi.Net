using System;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Events.Subtitles;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sushi.Net.Library.Events.Audio;
using Thinktecture.Extensions.Configuration;


namespace Sushi.Net.Library.Events
{
    public class Shifter
    {
        private readonly ILogger _logger;
        private readonly IGlobalCancellation _cancel;
        private readonly IProgressLoggerFactory _factory;
        private readonly ILoggingConfiguration _cfg;
        public Shifter(ILogger<Shifter> logger, IGlobalCancellation cancel, IProgressLoggerFactory fact, ILoggingConfiguration cfg)
        {
            _logger = logger;
            _cancel = cancel;
            _factory = fact;
            _cfg=cfg;
        }

        internal class State
        {
            public float StartTime { get; set; }
            public float EndTime { get; set; }
            public float? Shift { get; set; }
            public float? Diff { get; set; }
            public bool Inverse { get; set; }

            public string StartTimeString => (Inverse ? StartTime + (Shift ?? 0) : StartTime).FormatTime();
            public string EndTimeString => (Inverse ? EndTime + (Shift ?? 0) : EndTime).FormatTime();
            public string ShiftString => (Inverse ? -Shift : Shift).ToString();
            public string DiffString => Math.Abs(Diff ?? 0).ToString();

        }

        private void LogShift(State state, List<State> previous)
        {
            float start = state.StartTime;
            float end = state.EndTime;
            float shift = state.Shift ?? 0;
            string warning = "";
            previous = previous.Count>0 ? previous.Take(previous.Count - 1).ToList() : previous;
            if (state.Inverse)

            {
                start+=shift;
                end+=shift;
                shift=-shift;
                State prev = previous.FirstOrDefault(a => (a.EndTime + (a.Shift ?? 0)) > start);
                if (prev!=null)
                    warning = $" [Warning: A section of this block is already used for {(prev.EndTime+(prev.Shift??0)-start).FormatTime()}]";
            }
            _logger.LogDebug($"{start.FormatTime()}-{end.FormatTime()}: shift: {shift:F10}, diff: {state.Diff:F10}{warning}");
        }

        private void LogUncommitted(State state, float shift, float search_offset)
        {
            _logger.LogTrace($"{state.StartTimeString}-{state.EndTimeString}: shift: {shift:F5} search offset: {search_offset:F6}");
        }


        private (bool terminate, float new_time, float diff) Find(State group_state, SubStream pattern, AudioStream dest, float original_time, float offset, float window, float sample_rate, float allowed_error, bool is_audio)
        {

            (float diff, float new_time) = dest.FindSubStream(pattern, original_time + offset, window,!is_audio);
            (bool terminate, float k_new_time, float k_diff) = PromN(new_time, diff, 2, 2, pattern, dest, original_time, offset, window, sample_rate, allowed_error,!is_audio);
//            if (!terminate)
//                (terminate, k_new_time, k_diff) = PromN(new_time, diff, 2, 2, pattern, dest, original_time, offset, window, sample_rate, allowed_error, is_audio);
            LogUncommitted(group_state, k_diff, offset);
            return (terminate, k_new_time, k_diff);
        }
        
        public class Prom
        {
            public float Time;
            public float Diff;
            public int EqCount;

            
            public Prom(float time, float diff)
            {
                Time=time;
                Diff=diff;
            }
        }
        private (bool terminate, float new_time, float diff) PromN(float new_time, float diff, int cnt, int match_cnt, SubStream pattern, AudioStream dest, float original_time, float offset, float window, float sample_rate, float allowed_error, bool type=false)
        {
            List<Prom> values = new List<Prom>();
            values.Add(new Prom(new_time,diff));
            List<SubStream> subs = pattern.SplitSubStream(cnt);
            float reloc = 0;
            foreach(SubStream s in subs)
            {
                (float d, float t) = dest.FindSubStream(s, original_time + offset+reloc, window, type);
                values.Add(new Prom(t-reloc,d));
                reloc += s.Size / sample_rate;
            }

            for(int x=0;x<values.Count-1;x++)
            {
                for (int c = x+1; c < values.Count; c++)
                {

                    if (values[x].Time.AbsDiff(values[c].Time) < allowed_error)
                    {
                        values[c].EqCount++;
                        values[x].EqCount++;
                    }
                }
            }

            Prom pr = values.OrderByDescending(a => a.EqCount).First();
            bool terminate = pr.EqCount >= match_cnt;
            return (terminate, pr.Time, pr.Diff);
        }

        public Task CalculateShiftsAsync(AudioStream src_stream, AudioStream dst_stream, List<List<Event>> groups_list, float normal_window, float max_window, float rewind_trash, float allowed_error)
        {
            return Task.Run(() =>
            {
                float small_window = 1.5f;
                int idx = 0;
                List<State> committed_states = new List<State>();
                List<State> uncommitted_states = new List<State>();
                float window = normal_window;
                bool is_audio = groups_list[0][0] is AudioEvent;
                IProgress<int> progress = null;
                if (_cfg is SushiLoggingConfiguration l && l.LastLevel == LogLevel.Information)
                {
                    IProgressLogger logger = _factory.CreateProgressLogger(_logger);
                    progress = logger.CreateProgress();
                }
                while (idx < groups_list.Count)
                {
                    float percentage = idx * 100 / (float)groups_list.Count;
                    progress?.Report((int)percentage);
                    List<Event> search_group = groups_list[idx];
                    SubStream tv_audio = src_stream.GetSubStream(search_group[0].Start, search_group[^1].End);
                    float original_time = search_group[0].Start;
                    State group_state = new State { StartTime = search_group[0].Start, EndTime = search_group[^1].End , Inverse = is_audio};
                    float last_committed_shift = committed_states.Count > 0 ? (committed_states[^1].Shift ?? 0) : 0;
                    float? diff = null;
                    float? new_time = null;
                    float kmax = dst_stream.DurationInSeconds - (search_group[^1].End - search_group[0].Start);
                    if (uncommitted_states.Count == 0)
                    {
                        if (original_time >= src_stream.DurationInSeconds)
                        {
                            // event outside of audio range, all events past it are also guaranteed to fail
                            for (int x = idx; x < groups_list.Count; x++)
                            {
                                List<Event> g = groups_list[idx];
                                State st = new State {StartTime = g[0].Start, EndTime = g[^1].End, Inverse = is_audio};
                                committed_states.Add(st);
                                _logger.LogWarning($"{st.StartTimeString}-{st.EndTimeString}: outside of audio range");
                            }

                            break;
                        }

                        if (small_window < window && (original_time + last_committed_shift<=kmax))
                            (diff, new_time) = dst_stream.FindSubStream(tv_audio, original_time + last_committed_shift, small_window,!is_audio);
                        if (new_time != null && (new_time.Value - original_time).AbsDiff(last_committed_shift) <= allowed_error)
                        {
                            // fastest case - small window worked, commit the group immediately
                            group_state.Shift = new_time.Value - original_time;
                            group_state.Diff = diff;
                            committed_states.Add(group_state);
                            LogShift(group_state,committed_states);
                            if (window != normal_window)
                            {
                                _logger.LogInformation($"Going back to window {normal_window} from {window}");
                                window = normal_window;
                            }

                            idx++;
                            continue;
                        }
                    }

                    bool terminate = false;
                    if (original_time <= kmax)
                    {
                        (terminate, new_time, diff) = Find(group_state, tv_audio, dst_stream, original_time, last_committed_shift, window, src_stream.SampleRate, allowed_error, is_audio);
                    }
                    if (!terminate && uncommitted_states.Count > 0 && uncommitted_states[^1].Shift.HasValue && original_time  <= kmax)
                    {
                        float start_offset = uncommitted_states[^1].Shift.Value;
                        (terminate, new_time, diff) = Find(group_state, tv_audio, dst_stream, original_time, start_offset, window, src_stream.SampleRate, allowed_error, is_audio);
                    }

                    float? shift = null;
                    if (new_time.HasValue)
                        shift = new_time.Value - original_time;
                    if (!terminate)
                    {
                        //we aren't back on track yet - add this group to uncommitted
                        group_state.Shift = shift;
                        group_state.Diff = diff;
                        uncommitted_states.Add(group_state);
                        idx++;
                        if ((rewind_trash == uncommitted_states.Count) && window < max_window)
                        {
                            _logger.LogWarning($"Detected possibly broken segment starting at {uncommitted_states[0].StartTimeString}, increasing the window from {window} to {max_window}");
                            idx = committed_states.Count;
                            uncommitted_states.Clear();
                            window = max_window;
                        }

                        continue;
                    }

                    // we're back on track - apply current shift to all broken events
                    if (uncommitted_states.Count > 0)
                    {
                        _logger.LogWarning($"Events from {uncommitted_states[0].StartTimeString} to {uncommitted_states[^1].EndTimeString} will most likely be broken!");
                    }

                    uncommitted_states.Add(group_state);
                    foreach (State state in uncommitted_states)
                    {
                        state.Shift = shift;
                        state.Diff = diff;
                        committed_states.Add(state);
                        LogShift(state,committed_states);
                    }

                    uncommitted_states.Clear();
                    idx++;
                }
                progress?.Report(100);

                uncommitted_states.ForEach(a=>LogShift(a,committed_states));

                List<State> allstates = committed_states.Union(uncommitted_states).ToList();
                for (int x = 0; x < groups_list.Count; x++)
                {
                    List<Event> search_group = groups_list[x];
                    State group_state = allstates[x];
                    if (!group_state.Shift.HasValue)
                    {
                        foreach (List<Event> group in groups_list.Take(x).Reverse())
                        {
                            Event link_to = group.Where(a=>!a.Linked).Reverse().FirstOrDefault();
                            if (link_to != null)
                                search_group.ForEach(a => a.LinkEvent(link_to));
                        }
                    }
                    else
                        search_group.ForEach(a => a.SetShift(group_state.Shift.Value, group_state.Diff.Value));
                }
            }, _cancel.GetToken());
        }
    }
}
