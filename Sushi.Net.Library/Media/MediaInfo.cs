using System.Collections.Generic;
using Sushi.Net.Library.Decoding;

namespace Sushi.Net.Library.Media
{
    public class MediaInfo
    {
        public List<MediaStreamInfo> Videos { get; set; }
        public List<MediaStreamInfo> Audios { get; set; }
        public List<SubtitleStreamInfo> Subtitles { get; set; }
        public Chapters Chapters { get; set; }
    }
}
