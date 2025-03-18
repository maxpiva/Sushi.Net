using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Media
{
    public enum MediaStreamType
    {
        Video,
        Audio,
        Subtitle
    }

    public class MediaStreamInfo
    {

        private static readonly Regex BaseDurationRegex = new Regex(@"Duration:\s?(.*?),",RegexOptions.Compiled);

        public int Id { get; set; }
        public bool Default { get; set; }
        public bool Forced { get; set; }
        public bool Comment { get; set; }
        public bool HearingImpaired { get; set; }
        public string Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Language { get; set; }
        public float Duration { get; set; }
        public float StartTime { get; set; }
        public string Extension { get; set; }
        public int Channels { get; set; }
        public string ChannelLayout { get; set; }
        public string CodecName { get; set; }
        public int BitRate { get; set; }
        public int? SampleRate { get; set; }
        public MediaStreamType MediaType { get; set; }

        public string FrameRate { get; set; }

        public double FrameRateValue
        {
            get
            {
                if (string.IsNullOrEmpty(FrameRate))
                    return 0;
                string[] parts = FrameRate.Split('/');
                if (parts.Length != 2)
                    return double.Parse(FrameRate);
                if (!float.TryParse(parts[0], out float num))
                    return 0;
                if (!float.TryParse(parts[1], out float den))
                    return 0;
                return num / den;
            }
        }

        internal MediaStreamInfo()
        {

        }
        /*
        private static void PopulateDuration(MediaStreamInfo s, Match m)
        {
            Match m2 = BaseDurationRegex.Match(m.Value);
            if (m2.Success)
                s.Duration=m2.Groups[1].Value.Trim().ParseAssTime();
        }

        public static MediaStreamInfo FromVideo(Match m)
        {
            MediaStreamInfo s = new MediaStreamInfo
            {
                Id = Convert.ToInt32(m.Groups[1].Value),
                Info = m.Groups[2].Value,
                Width = Convert.ToInt32(m.Groups[3].Value),
                Height = Convert.ToInt32(m.Groups[4].Value),
                Default = m.Groups[5].Value != "",
                Title = m.Groups[6].Value,
                MediaType = MediaStreamType.Video
            };
            PopulateDuration(s,m);
            return s;
        }

        public List<string> SplitInfo()
        {
            return Info.Split(",").Select(a => a.Trim()).ToList();
        }
        public static MediaStreamInfo FromAudio(Match m)
        {

            MediaStreamInfo s = new MediaStreamInfo
            {
                Id = Convert.ToInt32(m.Groups[1].Value),
                Language = m.Groups[2].Value,
                Info = m.Groups[3].Value,
                Default = m.Groups[4].Value != "",
                Title = m.Groups[5].Value,
                MediaType = MediaStreamType.Audio
            };
            PopulateDuration(s,m);
            return s;
        }*/


    }
}