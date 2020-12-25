using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sushi.Net.Library.Events.Audio
{
    public class AudioEvents : IEvents
    {
        public List<Event> Events { get; set; }
        
        public AudioEvents(List<(float start, float end)> silences, float maxduration)
        {
            float orig = 0;
            bool endsilence=false;
            Events = new List<Event>();
            foreach ((float start, float end) in silences)
            {
                if (start == 0)
                {
                    orig = end;
                    continue;
                }
                Events.Add(new AudioEvent(orig,start));
                orig=end;
                if (orig+.5 > maxduration)
                    endsilence = true;
            }
            if (!endsilence)
                Events.Add(new AudioEvent(orig, maxduration));
        }
        public Task SaveAsync(string path)
        {
            throw new System.NotSupportedException();
        }
    }
}