using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;
using UtfUnknown;

namespace Sushi.Net.Library.Events.Subtitles.SubRip
{
    public class SubRipSubtitles : IEvents
    {
        public List<Event> Events { get;  set; }

        public SubRipSubtitles(List<SubRipSubtitle> events)
        {
            Events = events.Cast<Event>().ToList();
        }

        public static async Task<IEvents> CreateFromFile(string path, bool fromContainer)
        {
            DetectionResult result = CharsetDetector.DetectFromFile(path); // or pass FileInfo
            DetectionDetail det=result.Detected;
            Encoding enc = result?.Detected?.Encoding ?? Encoding.GetEncoding(1252);
            string text = await File.ReadAllTextAsync(path, enc).ConfigureAwait(false);
            MatchCollection collection = SubRipSubtitle.Event_Regex.Matches(text);
            List<SubRipSubtitle> events = collection.Where(a => a.Success).Select(a => new SubRipSubtitle(a)).OrderBy(a=>a.Start).ToList();
            return new SubRipSubtitles(events);
        }


        public Task SaveAsync(string path)
        {
            string text = string.Join("\n", Events.OrderBy(a=>a.Start));
            return File.WriteAllTextAsync(path, text, Encoding.UTF8);
        }
    }
}