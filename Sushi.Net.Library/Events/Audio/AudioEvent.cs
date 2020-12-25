using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events.Audio
{
    public class AudioEvent : Event
    {
        public override bool IsComment  => false;
        public override string FormatTime(float seconds) => seconds.FormatTime();
        
        public AudioEvent(float start, float end)
        {
            Start = start;
            End = end;
        }

    }
}