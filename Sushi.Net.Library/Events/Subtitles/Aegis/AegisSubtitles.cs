using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class AegisSubtitles : IEvents
    {
        private AegisSubtitles()
        {
        }

        public ScriptInfoList ScriptInfo { get; } = new();
        public GarbageList Garbage { get;} = new();
        public FontStyles Styles { get; } = new();
        public SortedDictionary<string, List<string>> Other { get; } = new();
        public List<Event> Events { get; set; } = new();


        internal AegisSubtitleParser _subsParser;

        public Task SaveAsync(string path)
        {
            StringBuilder bld = new StringBuilder();
            if (ScriptInfo.Count > 0)
            {
                bld.AppendLine(ScriptInfo.ToString());
            }
            if (Garbage.Count > 0)
            {
                bld.AppendLine(Garbage.ToString());
            }
            if (Styles.Count > 0)
            {
                bld.AppendLine(Styles.ToString());
            }

            if (Events.Count > 0)
            {
                List<AegisSubtitle> events = Events.OrderBy(a => a.Start).Cast<AegisSubtitle>().ToList();
                bld.AppendLine("[Events]");
                bld.AppendLine(_subsParser.ToString());
                events.ForEach(a => bld.AppendLine(a.ToString()));
            }

            if (Other.Count > 0)
            {
                foreach (KeyValuePair<string, List<string>> kv in Other)
                {
                    bld.AppendLine();
                    bld.AppendLine(kv.Key);
                    kv.Value.ForEach(a => bld.AppendLine(a));
                }
            }

            return File.WriteAllTextAsync(path, bld.ToString(), Encoding.UTF8);
        }

        private static void ParseScriptInfoLine(AegisSubtitles ass, string line)
        {
            if (line.StartsWith("Format:"))
                return;
            ass.ScriptInfo.Add(line);
        }
        private static void ParseGarbageLine(AegisSubtitles ass, string line)
        {
            ass.Garbage.Add(line);
        }

        private static void ParseStyleLine(AegisSubtitles ass, string line)
        {
            if (line.StartsWith("Format:"))
            {
                ass.Styles._fontStyleParser = new FontStyleParser(line);
                return;
            }
            ass.Styles.Add(ass.Styles._fontStyleParser.CreateFontStyle(line));
        }
            
        private static void ParseEventLine(AegisSubtitles ass, string line)
        {
            if (line.StartsWith("Format:"))
            {
                ass._subsParser = new AegisSubtitleParser(line);
                return;
            }

            if (line.StartsWith("{"))
                return;
            ass.Events.Add(ass._subsParser.CreateSubtitle(line, ass.Events.Count + 1));
        }

        private static Action<AegisSubtitles, string> CreateGenericParse(AegisSubtitles ass, string line)
        {
            if (ass.Other.ContainsKey(line))
                throw new SushiException("Duplicate section detected, invalid script?");
            ass.Other[line] = new List<string>();
            return (rass, rline) => { rass.Other[line].Add(rline); };
        }

        public static async Task<IEvents> CreateFromFile(string path, bool fromContainer)
        {
            string text = await path.ReadAllTextAsync(fromContainer).ConfigureAwait(false);
            Action<AegisSubtitles, string> func = null;
            AegisSubtitles ass = new AegisSubtitles();
            int cnt = 0;
            foreach (string lr in text.SplitLines())
            {
                cnt++;
                string line = lr.Strip();
                if (string.IsNullOrEmpty(line))
                    continue;
                string low = line.ToLowerInvariant();
                if (low == "[script info]")
                    func = ParseScriptInfoLine;
                else if (low == "[v4+ styles]" || low == "[v4 styles]")
                {
                    ass.Styles.V4Version = line;
                    func = ParseStyleLine;
                }
                else if (low == "[aegisub project garbage]")
                    func = ParseGarbageLine;
                else if (low == "[events]")
                    func = ParseEventLine;
                else if (low.Trim().StartsWith("[") && low.Trim().EndsWith("]"))
                {
                    func = CreateGenericParse(ass, line);
                }
                else if (func==null && low.StartsWith(";"))
                    continue;
                else if (func == null)
                    throw new SushiException("That's some invalid ASS script");
                else
                {
                    try
                    {
                        func(ass, line);
                    }
                    catch (Exception e)
                    {
                        throw new SushiException($"That's some invalid ASS script: {e.Message} [line {cnt}]");
                    }
                }
            }

            ass.Events = ass.Events.OrderBy(a => a.Start).ToList();
            return ass;
        }

        private string Mul(string val, float mul)
        {
            if (string.IsNullOrEmpty(val))
                return val;
            if (float.TryParse(val, out float f))
                return (f * mul).ToString(CultureInfo.InvariantCulture);
            return val;
        }

        private string ParseClipDrawing(string prefix, string[] parts, float multiplier)
        {
            string dwc;
            if (parts.Length == 1)
                dwc = parts[0];
            else
            {
                dwc = parts[1];
                prefix += parts[0] + ",";
            }

            return prefix + ParseDrawing(dwc, multiplier) + ")";
        }
        private (string result, bool drawingmode) ParseTag(string tag, bool drawingmode, float multiplier)
        {

            if (tag.StartsWith("fsp"))
                return ("fsp" + Mul(tag.Substring(3), multiplier), drawingmode);
            if (tag.StartsWith("pbo"))
                return ("pbo" + Mul(tag.Substring(3), multiplier), drawingmode);
            if (tag.StartsWith("fs") && !tag.StartsWith("fsc"))
                return ("fs" + Mul(tag.Substring(2), multiplier), drawingmode);
            if (tag.StartsWith("move("))// && tag.EndsWith(")"))
            {
                string[] kk = tag.Substring(5, tag.Length - 5).Replace(")", "").Split(',');
                for (int x = 0; x < 4; x++)
                    kk[x] = Mul(kk[x], multiplier);
                return ("move(" + string.Join(",", kk) + ")", drawingmode);
            }
            if (tag.StartsWith("pos("))// && tag.EndsWith(")"))

                return ("pos("+string.Join(",",tag.Substring(4, tag.Length - 4).Replace(")","").Split(',').Select(a => Mul(a, multiplier))) + ")", drawingmode);
            if (tag.StartsWith("org("))// && tag.EndsWith(")"))
                return ("org("+string.Join(",",tag.Substring(4, tag.Length - 4).Replace(")", "").Split(',').Select(a => Mul(a, multiplier))) + ")", drawingmode);
            if (tag.StartsWith("clip("))
            {
                string[] kk = tag.Substring(5, tag.Length - 6).Split(',');
                if (kk.Length == 4)
                    return ("clip(" + string.Join(",", tag.Substring(5, tag.Length - 5).Replace(")", "").Split(',').Select(a => Mul(a, multiplier))) + ")",drawingmode);
                return (ParseClipDrawing("clip(", kk,multiplier),drawingmode);
            }
            if (tag.StartsWith("iclip("))
            {
                string[] kk = tag.Substring(6, tag.Length - 6).Replace(")", "").Split(',');
                if (kk.Length == 4)
                    return ("iclip(" + string.Join(",", tag.Substring(6, tag.Length - 6).Replace(")", "").Split(',').Select(a => Mul(a, multiplier))) + ")",drawingmode);
                return (ParseClipDrawing("iclip(", kk,multiplier),drawingmode);
            }

            if (tag.StartsWith("p"))
            {
                if (tag.EndsWith(")"))
                    tag=tag.Substring(0, tag.Length - 1);
                if (tag.Length>1)
                    drawingmode = int.Parse(tag.Substring(1)) != 0;
            }
            return (tag,drawingmode);
        }
        private string ParseDrawing(string dwc, float multiplier)
        {
            string[] ccc = dwc.Split(' ');
            for (int x = 0; x < ccc.Length; x++)
            {
                switch (ccc[x])
                {
                    case "m":
                    case "n":
                    case "l":
                    case "b":
                    case "s":
                    case "c":
                    case "p":
                        continue;
                    default:
                        ccc[x] = Mul(ccc[x], multiplier);
                        break;
                }
            }
            return string.Join(" ", ccc);
        }
        
        private (string result, bool drawingmode) ParseTags(string tags, bool drawingmode, float multiplier)
        {
            int depth = 0;
            int start = 0;
            StringBuilder bld = new StringBuilder();
            List<string> parsed = new List<string>();
            for (int cur = 1; cur < tags.Length; cur++)
            {
                if (tags[cur] == '(')
                    depth++;
                else if (depth > 0 && tags[cur] == ')')
                    depth--;
                else if (tags[cur] == '\\')
                {
                    parsed.Add(tags.Substring(start, cur - start));
                    start = cur;
                }
            }

            if (!string.IsNullOrEmpty(tags))
            {
                parsed.Add(tags.Substring(start));
            }

            foreach (string tag in parsed)
            {
                string r;
                (r, drawingmode) = ParseTag(tag.Substring(1), drawingmode, multiplier);
                bld.Append("\\"+r);
            }
            return (bld.ToString(), drawingmode);
        }
        private string ParseText(string text, float multiplier)
        {
            try
            {
                StringBuilder ret = new StringBuilder();
                bool drawingmode = false;
                int cur = 0;
                while (cur < text.Length)
                {
                    if (text[cur] == '{')
                    {
                        int end = text.IndexOf("}", cur, StringComparison.InvariantCulture);
                        if (end != -1)
                        {
                            try
                            {
                                string work = text.Substring(cur + 1, end - (cur + 1));
                                if (work.IndexOf("\\", StringComparison.InvariantCulture) >= 0)
                                    (work, drawingmode) = ParseTags(work, drawingmode, multiplier);
                                ret.Append("{" + work + "}");
                                cur = end + 1;
                                continue;
                            }
                            catch (Exception e)
                            {
                                int a1 = 1;
                            }

                            int b = 1;

                        }
                    }

                    string nwork;
                    int end2 = text.IndexOf("{", cur + 1, StringComparison.InvariantCulture);
                    if (end2 >= 0)
                    {
                        nwork = text.Substring(cur, end2 - cur);
                        cur = end2;
                    }
                    else
                    {
                        nwork = text.Substring(cur);
                        cur = text.Length;
                    }

                    if (drawingmode)
                        ret.Append(ParseDrawing(nwork, multiplier));
                    else
                        ret.Append(nwork);
                }

                return ret.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return text;
        }

        private Regex fn = new Regex("\\fn(.*?)(\\|})", RegexOptions.Compiled | RegexOptions.Singleline);
        public List<string> CollectFonts()
        {
            List<string> usedStyles = Events.Cast<AegisSubtitle>().Where(a=>!a.IsComment).Select(a=>a.Style.ToLowerInvariant()).Distinct().ToList();
            HashSet<string> fonts = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (string s in this.Styles.Where(a => usedStyles.Contains(a.Name.ToLowerInvariant()))
                         .Select(a => a.FontName).Distinct())
            {
                if (!fonts.Contains(s))
                    fonts.Add(s);
            }
            List<string> allText = Events.Cast<AegisSubtitle>().Where(a => !a.IsComment).Select(a => a.Text).ToList();
            foreach (string n in allText)
            {
                MatchCollection mc = fn.Matches(n);
                foreach (Match m in mc)
                {
                    if (m.Success)
                    {
                        string font = m.Groups[1].Value;
                        if (!string.IsNullOrEmpty(font) && !fonts.Contains(font))
                            fonts.Add(font);
                    }
                }
            }
            return fonts.ToList();
        }
        public void Resize(int width, int height, bool resize_borders)
        {

            if (ScriptInfo.Width == 0 || ScriptInfo.Height == 0)
                return;
            if (ScriptInfo.Width == width && ScriptInfo.Height == height)
                return;
            float multiplier=(float)width/(float)ScriptInfo.Width;
            if (height/(float)ScriptInfo.Height<multiplier)
                multiplier = (float)height / (float)ScriptInfo.Height;
            ScriptInfo.Width = width;
            ScriptInfo.Height = height;
            foreach(FontStyle style in Styles)
            {
                style.FontSize*=multiplier;
                style.Spacing *= multiplier;
                style.MarginLeft = (int)Math.Round(style.MarginLeft * multiplier);
                style.MarginRight = (int)Math.Round(style.MarginRight * multiplier);
                style.MarginVertical = (int)Math.Round(style.MarginVertical * multiplier);
                if (resize_borders)
                {
                    style.Outline *= multiplier;
                    style.Shadow *= multiplier;
                }
            }
            foreach(AegisSubtitle sub in Events.Cast<AegisSubtitle>())
            {
                sub.MarginLeft = (int)Math.Round(sub.MarginLeft * multiplier);
                sub.MarginRight = (int)Math.Round(sub.MarginRight * multiplier);
                sub.MarginVertical = (int)Math.Round(sub.MarginVertical * multiplier);
                if (sub.Text.StartsWith("{="))
                    continue;
                if (!sub.Text.Contains("{"))
                    continue;
                sub.Text = ParseText(sub.Text, multiplier);
            }

        }

    }
}