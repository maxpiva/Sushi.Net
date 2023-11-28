using System.Globalization;
using System.Text.RegularExpressions;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class FontStyle
    {
        public static Regex StyleLine = new Regex("(.*?):(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*)", RegexOptions.Compiled);

        private FontStyleParser _parser;

        public FontStyle(FontStyleParser parser)
        {
            _parser = parser;
        }


        public string Kind { get; set; }
        public string Name { get; set; }
        public string FontName { get; set; }
        public float FontSize { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
        public string OutlineColor { get; set; }
        public string BackColor { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Strikeout { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float Spacing { get; set; }
        public float Angle { get; set; }
        public int BorderStyle { get; set; }
        public float Outline { get; set; }
        public float Shadow { get; set; }
        public int Alignment { get; set; }
        public int MarginLeft { get; set; }
        public int MarginRight { get; set; }
        public int MarginVertical { get; set; }
        public int Encoding { get; set; }
        public float AlphaLevel { get; set; }
        public override string ToString()
        {
            return _parser.CreateLine(this);
        }
    }
}