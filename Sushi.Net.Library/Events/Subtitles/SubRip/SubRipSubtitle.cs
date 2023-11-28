using System;
using System.Text.RegularExpressions;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events.Subtitles.SubRip
{
    public class SubRipSubtitle : Event
    {
        public static Regex Event_Regex = new Regex(@"(\d+?)\s+?(\d{1,2}:\d{1,2}:\d{1,2},\d+)\s-->\s(\d{1,2}:\d{1,2}:\d{1,2},\d+).(.+?)(?=(?:\d+?\s+?\d{1,2}:\d{1,2}:\d{1,2},\d+\s-->\s\d{1,2}:\d{1,2}:\d{1,2},\d+)|$)", RegexOptions.Compiled | RegexOptions.Singleline);
 
        public override bool IsComment => false;

        private SubRipSubtitle()
        {

        }

        public SubRipSubtitle(string text)
        {
            Match m = Event_Regex.Match(text);
            FillMatch(m);
        }

        public SubRipSubtitle(Match m)
        {
            FillMatch(m);
        }

        private void FillMatch(Match m)
        {
            Start = ParseTime(m.Groups[2].Value);
            End = ParseTime(m.Groups[3].Value);
            SourceIndex = Convert.ToInt32(m.Groups[1].Value);
            Text = m.Groups[4].Value.Strip();

        }
        public override string ToString()
        {
            return $"{SourceIndex}\n{FormatTime(Start)} --> {FormatTime(End)}\n{Text}";
        }

        private float ParseTime(string str) => str.Replace(",", ".").ParseAssTime();

        public override string FormatTime(float seconds) => seconds.FormatSrtTime();
        
        public override Event Clone()
        {
            return new SubRipSubtitle()
            {
                Text = this.Text,
                End = this.End,
                SourceIndex = this.SourceIndex,
                Start = this.Start
            };
        }
    }
}