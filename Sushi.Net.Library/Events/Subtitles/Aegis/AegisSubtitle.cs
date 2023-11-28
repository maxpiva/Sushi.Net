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
        public string Marked { get; set; }


        public override bool IsComment => string.Equals(Kind, "comment", System.StringComparison.InvariantCultureIgnoreCase);

       
        private AegisSubtitleParser _parser;
        public AegisSubtitle(AegisSubtitleParser parser, int position = 0)
        {
            SourceIndex = position;
            _parser = parser;
        }

        private AegisSubtitle()
        {

        }
        public override string ToString()
        {
            return _parser.CreateLine(this);
        }

        public override string FormatTime(float seconds) => seconds.FormatTime();

        public override Event Clone()
        {
            return new AegisSubtitle()
            {
                Text = this.Text,
                Actor = this.Actor,
                Effect = this.Effect,
                End = this.End,
                Kind = this.Kind,
                Layer = this.Layer,
                MarginLeft = this.MarginLeft,
                MarginRight = this.MarginRight,
                MarginVertical = this.MarginVertical,
                SourceIndex = this.SourceIndex,
                Start = this.Start,
                Style = this.Style,
                Marked=this.Marked,
                _parser = this._parser
            };
        }
    }
}