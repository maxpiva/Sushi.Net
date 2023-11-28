using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sushi.Net.Library.Events.Subtitles;
using Sushi.Net.Library.Events.Subtitles.Aegis;
using Sushi.Net.Library.Events.Subtitles.SubRip;

namespace Sushi.Net.Library.Events
{

    
    public interface IEvents
    {
        List<Event> Events { get; set; }
        public void SortByTime() => Events = Events.OrderBy(a => a.Start).ToList();
        Task SaveAsync(string path);

        public static Task<IEvents> CreateFromFileAsync(string file, bool fromContainer)
        {
            if (System.IO.Path.GetExtension(file).ToLowerInvariant() == ".srt")
                return SubRipSubtitles.CreateFromFile(file, fromContainer);
            return AegisSubtitles.CreateFromFile(file, fromContainer);
        }
    }
}