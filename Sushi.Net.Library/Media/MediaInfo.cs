using System.Collections.Generic;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Media
{
    public class MediaInfo
    {
        public List<VideoMedia> Videos { get; set; }
        public List<AudioMedia> Audios { get; set; }
        public List<SubtitleMedia> Subtitles { get; set; }
        public Chapters Chapters { get; set; }
    }
}
