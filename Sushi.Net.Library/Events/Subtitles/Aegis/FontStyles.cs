using System.Collections.Generic;
using System.Text;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class FontStyles : List<FontStyle>
    {
        internal FontStyleParser _fontStyleParser;

        public string V4Version { get; set; }

        public override string ToString()
        {
            StringBuilder bld = new();
            bld.AppendLine(V4Version);
            bld.AppendLine(_fontStyleParser.ToString());
            ForEach(a => bld.AppendLine(a.ToString()));
            return bld.ToString();
        }
    }
}