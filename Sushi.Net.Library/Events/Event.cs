using System.Diagnostics;

namespace Sushi.Net.Library.Events
{
    public abstract class Event
    {
        private float _diff = 1;
        private float _end_shift = 0;
        private Event _linked_event = null;
        private float _shift = 0;
        private float _start_shift = 0;
        public int SourceIndex { get; set; }
        public float Start { get; set; }
        public float End { get; set; }
        public string Text { get; set; }

        public float Shift => Linked ? _linked_event.Shift : _shift;

        public float Diff => Linked ? _linked_event.Diff : _diff;

        public float Duration => End - Start;

        public float ShiftedEnd => End + Shift + _end_shift;

        public float ShiftedStart => Start + Shift + _start_shift;

        public bool Linked => _linked_event != null;


        public abstract bool IsComment { get; }

        public abstract string FormatTime(float seconds);


        public void ApplyShift()
        {
            Start = ShiftedStart;
            End = ShiftedEnd;
        }

        public void SetShift(float shift, float audio_diff)
        {
            Debug.Assert(!Linked, "Cannot set shift of a linked event");
            _shift = shift;
            _diff = audio_diff;
        }

        public void AdjustAdditionalShifts(float start_shift, float end_shift)
        {
            Debug.Assert(!Linked, "Cannot set shift of a linked event");
            _start_shift = start_shift;
            _end_shift = end_shift;
        }

        public Event GetLinkChainEnd() => Linked ? _linked_event.GetLinkChainEnd() : this;

        public void LinkEvent(Event other)
        {
            Debug.Assert(other.GetLinkChainEnd() != this, "Circular link detected");
            _linked_event = other;
        }

        public void ResolveLink()
        {
            Debug.Assert(Linked, "Cannot resolve unlinked events");
            _shift = _linked_event.Shift;
            _diff = _linked_event.Diff;
            _linked_event = null;
        }

        public void AdjustShift(float value)
        {
            Debug.Assert(!Linked, "Cannot adjust time of linked events");
            _shift += value;
        }
    }
}