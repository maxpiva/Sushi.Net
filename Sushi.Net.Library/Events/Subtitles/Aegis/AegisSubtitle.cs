using System.Text.RegularExpressions;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class AegisSubtitle : Event
    {
        public string Effect { get; set; }
        public string Kind { get; set; }
        public int Layer { get; set; }
        public int MarginLeft { get; set; }
        public int MarginRight { get; set; }
        public int MarginVertical { get; set; }
        public string Actor { get; set; }
        public string Style { get; set; }


        public override bool IsComment => string.Equals(Kind, "comment", System.StringComparison.InvariantCultureIgnoreCase);

        public static Regex AssLine = new Regex("(.*?):(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*)", RegexOptions.Compiled);

        public AegisSubtitle(string text, int position = 0)
        {
            SourceIndex = position;
            Match m = AssLine.Match(text);
            Kind = m.Groups[1].Value.Strip();
            Layer = int.Parse(m.Groups[2].Value.Strip());
            Start = m.Groups[3].Value.Strip().ParseAssTime();
            End = m.Groups[4].Value.Strip().ParseAssTime();
            Style = m.Groups[5].Value.Strip();
            Actor = m.Groups[6].Value.Strip();
            MarginLeft = int.Parse(m.Groups[7].Value.Strip());
            MarginRight = int.Parse(m.Groups[8].Value.Strip());
            MarginVertical = int.Parse(m.Groups[9].Value.Strip());
            Effect = m.Groups[10].Value.Strip();
            Text = m.Groups[11].Value;
        }

        public override string ToString()
        {
            return $"{Kind}: {Layer},{FormatTime(Start)},{FormatTime(End)},{Style},{Actor},{MarginLeft:D4},{MarginRight:D4},{MarginVertical:D4},{Effect},{Text}";
        }

        public override string FormatTime(float seconds) => seconds.FormatTime();
    }
}