using System.Collections.Generic;
using System.Text;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class FontStyles : List<FontStyle>
    {
        public override string ToString()
        {
            StringBuilder bld = new();
            bld.AppendLine("[V4+ Styles]");
            bld.AppendLine("Format: Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,OutlineColour,BackColour,Bold,Italic,Underline,StrikeOut,ScaleX,ScaleY,Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,MarginV,Encoding");
            ForEach(a => bld.AppendLine(a.ToString()));
            return bld.ToString();
        }
    }
}