using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Media
{
    public class SubtitleStreamInfo : MediaStreamInfo
    {
        private static Dictionary<string, string> maps = new Dictionary<string, string> {
            {
                "ssa",
                ".ass"},
            {
                "ass",
                ".ass"},
            {
                "subrip",
                ".srt"}};


        public SubtitleStreamInfo(Match m)
        {

            Id = Convert.ToInt32(m.Groups[1].Value);
            Info = m.Groups[3].Value;
            Language = m.Groups[2].Value;
            Type = maps.ContainsKey(m.Groups[4].Value.ToLowerInvariant()) ? maps[m.Groups[4].Value.ToLowerInvariant()] : m.Groups[4].Value;
            Default = m.Groups[5].Value != "";
            Title = m.Groups[6].Value.Strip();
        }
        public string Type { get; set; }

    }
}