using System.Globalization;
using System.Text.RegularExpressions;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class FontStyle
    {
        public static Regex StyleLine = new Regex("(.*?):(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*?),(.*)", RegexOptions.Compiled);

        public FontStyle(string line)
        {
            Match m = StyleLine.Match(line);
            Kind = m.Groups[1].Value.Strip();
            Name = m.Groups[2].Value.Strip();
            FontName = m.Groups[3].Value.Strip();
            FontSize = float.Parse(m.Groups[4].Value.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
            PrimaryColor = m.Groups[5].Value.Strip();
            SecondaryColor = m.Groups[6].Value.Strip();
            OutlineColor = m.Groups[7].Value.Strip();
            BackColor = m.Groups[8].Value.Strip();
            Bold = int.Parse(m.Groups[9].Value.Strip()) != 0;
            Italic = int.Parse(m.Groups[10].Value.Strip()) != 0;
            Underline = int.Parse(m.Groups[11].Value.Strip()) != 0;
            Strikeout = int.Parse(m.Groups[12].Value.Strip()) != 0;
            ScaleX = float.Parse(m.Groups[13].Value.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
            ScaleY = float.Parse(m.Groups[14].Value.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
            Spacing = float.Parse(m.Groups[15].Value.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
            Angle = float.Parse(m.Groups[16].Value.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
            BorderStyle = int.Parse(m.Groups[17].Value.Strip());
            Outline = float.Parse(m.Groups[18].Value.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
            Shadow = float.Parse(m.Groups[19].Value.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
            Alignment = int.Parse(m.Groups[20].Value.Strip());
            MarginLeft = int.Parse(m.Groups[21].Value.Strip());
            MarginRight = int.Parse(m.Groups[22].Value.Strip());
            MarginVertical = int.Parse(m.Groups[23].Value.Strip());
            Encoding = int.Parse(m.Groups[24].Value.Strip());
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

        public override string ToString()
        {
            return $"{Kind}: {Name},{FontName},{FontSize.ToString(CultureInfo.InvariantCulture)},{PrimaryColor},{SecondaryColor},{OutlineColor},{BackColor},{(Bold ? "-1" : "0")},{(Italic ? "-1" : "0")},{(Underline ? "-1" : "0")},{(Strikeout ? "-1" : "0")},{ScaleX.ToString(CultureInfo.InvariantCulture)},{ScaleY.ToString(CultureInfo.InvariantCulture)},{Spacing.ToString(CultureInfo.InvariantCulture)},{Angle.ToString(CultureInfo.InvariantCulture)},{BorderStyle},{Outline.ToString(CultureInfo.InvariantCulture)},{Shadow.ToString(CultureInfo.InvariantCulture)},{Alignment},{MarginLeft:D4},{MarginRight:D4},{MarginVertical:D4},{Encoding}";
        }
    }
}