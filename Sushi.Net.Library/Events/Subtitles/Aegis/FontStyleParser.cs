using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class FontStyleParser
    {

        List<string> fields=new List<string>();

        public FontStyleParser(string format_line)
        {
            fields = format_line.Split(new char[] { ',',':'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        public override string ToString()
        {
            return $"{fields[0]}: {string.Join(", ", fields.Skip(1))}";
        }

        public FontStyle CreateFontStyle(string format_line)
        {

            FontStyle style = new FontStyle(this);
            for (int x = 0; x < fields.Count; x++)
            {
                string par=string.Empty;
                if (x < fields.Count - 1)
                {
                    int idx = format_line.IndexOf(x == 0 ? ":" : ",");
                    if (idx >= 0)
                    {
                        par = format_line.Substring(0, idx).Trim();
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
                            style.Kind = par;
                            break;
                        case "NAME":
                            style.Name = par;
                            break;
                        case "FONTNAME":
                            style.FontName = par;
                            break;
                        case "FONTSIZE":
                            style.FontSize = float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "PRIMARYCOLOUR":
                        case "PRIMARYCOLOR":
                            style.PrimaryColor = par;
                            break;
                        case "SECONDARYCOLOUR":
                        case "SECONDARYCOLOR":
                            style.SecondaryColor = par;
                            break;
                        case "TERTIARYCOLOUR":
                        case "TERTIARYCOLOR":
                        case "OUTLINECOLOUR":
                        case "OUTLINECOLOR":
                            style.OutlineColor = par;
                            break;
                        case "BACKCOLOUR":
                        case "BACKCOLOR":
                            style.BackColor = par;
                            break;
                        case "BOLD":
                            style.Bold = int.Parse(par) != 0;
                            break;
                        case "ITALIC":
                            style.Italic = int.Parse(par) != 0;
                            break;
                        case "UNDERLINE":
                            style.Underline = int.Parse(par) != 0;
                            break;
                        case "STRIKEOUT":
                            style.Strikeout = int.Parse(par) != 0;
                            break;
                        case "BORDERSTYLE":
                            style.BorderStyle = int.Parse(par);
                            break;
                        case "SCALEX":
                            style.ScaleX = float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "SCALEY":
                            style.ScaleY = float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "ANGLE":
                            style.Angle = float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "OUTLINE":
                            style.Outline = float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "SHADOW":
                            style.Shadow = float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "ALIGNMENT":
                            style.Alignment = int.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "SPACING":
                            style.Spacing= float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "MARGINL":
                            style.MarginLeft= int.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "MARGINR":
                            style.MarginRight = int.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "MARGINV":
                            style.MarginVertical = int.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "ALPHALEVEL":
                            style.AlphaLevel = float.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;
                        case "ENCODING":
                            style.Encoding = int.Parse(par, NumberStyles.Any, CultureInfo.InvariantCulture);
                            break;

                    }
            
            }
            return style;
        }


        public string CreateLine(FontStyle style)
        {
            StringBuilder bld = new StringBuilder();

            for (int x = 0; x < fields.Count; x++)
            {
                if (x  == 1)
                    bld.Append(": ");
                if (x > 1)
                    bld.Append(",");
                switch (fields[x].ToUpperInvariant())
                {
                    case "KIND":
                    case "FORMAT":
                        bld.Append(style.Kind);
                        break;
                    case "NAME":
                        bld.Append(style.Name);
                        break;
                    case "FONTNAME":
                        bld.Append(style.FontName);
                        break;
                    case "FONTSIZE":
                        bld.Append(style.FontSize.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "PRIMARYCOLOUR":
                    case "PRIMARYCOLOR":
                        bld.Append(style.PrimaryColor);
                        break;
                    case "SECONDARYCOLOUR":
                    case "SECONDARYCOLOR":
                        bld.Append(style.SecondaryColor);
                        break;
                    case "TERTIARYCOLOUR":
                    case "TERTIARYCOLOR":
                    case "OUTLINECOLOUR":
                    case "OUTLINECOLOR":
                        bld.Append(style.OutlineColor);
                        break;
                    case "BACKCOLOUR":
                    case "BACKCOLOR":
                        bld.Append(style.BackColor);
                        break;
                    case "BOLD":
                        bld.Append(style.Bold ? "-1" : "0");
                        break;
                    case "ITALIC":
                        bld.Append(style.Italic ? "-1" : "0");
                        break;
                    case "UNDERLINE":
                        bld.Append(style.Underline ? "-1" : "0");
                        break;
                    case "STRIKEOUT":
                        bld.Append(style.Strikeout ? "-1" : "0");
                        break;
                    case "BORDERSTYLE":
                        bld.Append(style.BorderStyle);
                        break;
                    case "SCALEX":
                        bld.Append(style.ScaleX.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "SCALEY":
                        bld.Append(style.ScaleY.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "OUTLINE":
                        bld.Append(style.Outline.ToString(CultureInfo.InvariantCulture));                        
                        break;
                    case "SHADOW":
                        bld.Append(style.Shadow.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "ANGLE":
                        bld.Append(style.Angle.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "ALIGNMENT":
                        bld.Append(style.Alignment.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "SPACING":
                        bld.Append(style.Spacing.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "MARGINL":
                        bld.Append(style.MarginLeft.ToString("D4"));
                        break;
                    case "MARGINR":
                        bld.Append(style.MarginRight.ToString("D4"));
                        break;
                    case "MARGINV":
                        bld.Append(style.MarginVertical.ToString("D4"));
                        break;
                    case "ALPHALEVEL":
                        bld.Append(style.AlphaLevel.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "ENCODING":
                        bld.Append(style.Encoding.ToString(CultureInfo.InvariantCulture));
                        break;

   
                }
            }
            return bld.ToString();
        }
    }
}
