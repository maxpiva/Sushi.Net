using System;
using System.Text.RegularExpressions;

namespace Sushi.Net.Library.Media
{
    public class MediaStreamInfo
    {


        public int Id { get; set; }
        public string Info { get; set; }
        public bool Default { get; set; }
        public string Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Language { get; set; }
        
        internal MediaStreamInfo()
        {

        }
        public static MediaStreamInfo FromVideo(Match m)
        {
            return new MediaStreamInfo
            {
                Id = Convert.ToInt32(m.Groups[1].Value),
                Info = m.Groups[2].Value,
                Width = Convert.ToInt32(m.Groups[3].Value),
                Height = Convert.ToInt32(m.Groups[4].Value),
                Default = m.Groups[5].Value != "",
                Title = m.Groups[6].Value
            };
        }
        
        public static MediaStreamInfo FromAudio(Match m)
        {
            return new MediaStreamInfo
            {
                Id = Convert.ToInt32(m.Groups[1].Value),
                Language = m.Groups[2].Value,
                Info = m.Groups[3].Value,
                Default = m.Groups[4].Value != "",
                Title = m.Groups[5].Value
            };
        }


    }
}