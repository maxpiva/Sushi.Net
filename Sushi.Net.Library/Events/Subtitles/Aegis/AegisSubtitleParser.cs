using OpenCvSharp;
using Sushi.Net.Library.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class AegisSubtitleParser
    {

        List<string> fields = new List<string>();

        public AegisSubtitleParser(string format_line)
        {
            fields = format_line.Split(new char[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        public override string ToString()
        {
            return $"{fields[0]}: {string.Join(", ", fields.Skip(1))}";
        }

        public AegisSubtitle CreateSubtitle(string format_line, int position = 0)
        {
            AegisSubtitle sub = new AegisSubtitle(this, position);
            for (int x = 0; x < fields.Count; x++)
            {
                string par = string.Empty;
                if (x < fields.Count - 1)
                {
                    int idx = format_line.IndexOf(x == 0 ? ":" : ",");
                    if (idx >= 0)
                    {
                        par = format_line.Substring(0, idx).Trim().Strip();
                        format_line = format_line.Substring(idx + 1);
                    }
                }
                else
                {
                    par = format_line;
                    format_line = "";
                }

                switch (fields[x].ToUpperInvariant())
                {
                    case "KIND":
                    case "FORMAT":
                        sub.Kind = par.Strip();
                        break;
                    case "LAYER":
                        sub.Layer = int.Parse(par.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
                        break;
                    case "START":
                        sub.Start = par.Strip().ParseAssTime();
                        break;
                    case "END":
                        sub.End = par.Strip().ParseAssTime();
                        break;
                    case "STYLE":
                        sub.Style = par.Strip();
                        break;
                    case "ACTOR":
                    case "NAME":
                        sub.Actor = par.Strip();
                        break;
                    case "MARGINL":
                        sub.MarginLeft = int.Parse(par.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
                        break;
                    case "MARGINR":
                        sub.MarginRight = int.Parse(par.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
                        break;
                    case "MARGINV":
                        sub.MarginVertical = int.Parse(par.Strip(), NumberStyles.Any, CultureInfo.InvariantCulture);
                        break;
                    case "EFFECT":
                        sub.Effect = par.Strip();
                        break;
                    case "MARKED":
                        sub.Marked = par.Strip();
                        break;
                    case "TEXT":
                        sub.Text = par;
                        break;
                }
            }
            return sub;
        }
        public string CreateLine(AegisSubtitle sub)
        {
            StringBuilder bld = new StringBuilder();

            for (int x = 0; x < fields.Count; x++)
            {
                if (x == 1)
                    bld.Append(": ");
                if (x > 1)
                    bld.Append(",");
                switch (fields[x].ToUpperInvariant())
                {
                    case "KIND":
                    case "FORMAT":
                        bld.Append(sub.Kind);
                        break;
                    case "LAYER":
                        bld.Append(sub.Layer.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "START":
                        bld.Append(sub.Start.FormatTime());
                        break;
                    case "END":
                        bld.Append(sub.End.FormatTime());
                        break;
                    case "STYLE":
                        bld.Append(sub.Style);
                        break;
                    case "ACTOR":
                    case "NAME":
                        bld.Append(sub.Actor);
                        break;
                    case "MARGINL":
                        bld.Append(sub.MarginLeft.ToString("D4"));
                        break;
                    case "MARGINR":
                        bld.Append(sub.MarginRight.ToString("D4"));
                        break;
                    case "MARGINV":
                        bld.Append(sub.MarginVertical.ToString("D4"));
                        break;
                    case "EFFECT":
                        bld.Append(sub.Effect);
                        break;
                    case "MARKED":
                        bld.Append(sub.Marked);
                        break;
                    case "TEXT":
                        bld.Append(sub.Text);
                        break;

                }
            }
            return bld.ToString();
        }
    }
}
