using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        public FontStyles Styles { get; } = new();
        public SortedDictionary<string, List<string>> Other { get; } = new();
        public List<Event> Events { get; set; } = new();


        public Task SaveAsync(string path)
        {
            StringBuilder bld = new StringBuilder();
            if (ScriptInfo.Count > 0)
            {
                bld.AppendLine(ScriptInfo.ToString());
            }

            if (Styles.Count > 0)
            {
                bld.AppendLine(Styles.ToString());
            }

            if (Events.Count > 0)
            {
                List<AegisSubtitle> events = Events.OrderBy(a => a.Start).Cast<AegisSubtitle>().ToList();
                bld.AppendLine("[Events]");
                bld.AppendLine("Format: Layer,Start,End,Style,Name,MarginL,MarginR,MarginV,Effect,Text");
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

        private static void ParseStyleLine(AegisSubtitles ass, string line)
        {
            if (line.StartsWith("Format:"))
                return;
            ass.Styles.Add(new FontStyle(line));
        }

        private static void ParseEventLine(AegisSubtitles ass, string line)
        {
            if (line.StartsWith("Format:"))
                return;
            ass.Events.Add(new AegisSubtitle(line, ass.Events.Count + 1));
        }

        private static Action<AegisSubtitles, string> CreateGenericParse(AegisSubtitles ass, string line)
        {
            if (ass.Other.ContainsKey(line))
                throw new SushiException("Duplicate section detected, invalid script?");
            ass.Other[line] = new List<string>();
            return (rass, rline) => { rass.Other[line].Add(rline); };
        }

        public static async Task<IEvents> CreateFromFile(string path)
        {
            string text = await path.ReadAllTextAsync().ConfigureAwait(false);
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
                else if (low == "[v4+ styles]")
                    func = ParseStyleLine;
                else if (low == "[events]")
                    func = ParseEventLine;
                else if (low.Trim().StartsWith("[") && low.Trim().EndsWith("]"))
                {
                    func = CreateGenericParse(ass, line);
                }
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
            if (tag.StartsWith("move(") && tag.EndsWith(")"))
            {
                string[] kk = tag.Substring(5, tag.Length - 6).Split(',');
                for (int x = 0; x < 4; x++)
                    kk[x] = Mul(kk[x], multiplier);
                return ("move(" + string.Join(",", kk) + ")", drawingmode);
            }
            if (tag.StartsWith("pos(") && tag.EndsWith(")"))
                return ("pos("+string.Join(",",tag.Substring(4, tag.Length - 5).Split(',').Select(a => Mul(a, multiplier))) + ")", drawingmode);
            if (tag.StartsWith("org(") && tag.EndsWith(")"))
                return ("org("+string.Join(",",tag.Substring(4, tag.Length - 5).Split(',').Select(a => Mul(a, multiplier))) + ")", drawingmode);
            if (tag.StartsWith("clip("))
            {
                string[] kk = tag.Substring(5, tag.Length - 6).Split(',');
                if (kk.Length == 4)
                    return ("clip(" + string.Join(",", tag.Substring(5, tag.Length - 6).Split(',').Select(a => Mul(a, multiplier))) + ")",drawingmode);
                return (ParseClipDrawing("clip(", kk,multiplier),drawingmode);
            }
            if (tag.StartsWith("iclip("))
            {
                string[] kk = tag.Substring(6, tag.Length - 7).Split(',');
                if (kk.Length == 4)
                    return ("iclip(" + string.Join(",", tag.Substring(6, tag.Length - 7).Split(',').Select(a => Mul(a, multiplier))) + ")",drawingmode);
                return (ParseClipDrawing("iclip(", kk,multiplier),drawingmode);
            }

            if (tag.StartsWith("p"))
            {
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
                (r, drawingmode) = ParseTag(tag, drawingmode, multiplier);
                bld.Append(r);
            }
            return (bld.ToString(), drawingmode);
        }
        private string ParseText(string text, float multiplier)
        {
            StringBuilder ret = new StringBuilder();
            bool drawingmode=false;
            int cur = 0;
            while(cur<text.Length)
            {
                if (text[cur] == '{')
                {
                    int end = text.IndexOf("}",cur, StringComparison.InvariantCulture);
                    if (end != -1)
                    {
                        string work = text.Substring(cur+1, end - (cur+1));
                        if (work.IndexOf("\\",StringComparison.InvariantCulture) >= 0)
                            (work,drawingmode) = ParseTags(work, drawingmode, multiplier);
                        ret.Append("{"+work+"}");
                        cur = end + 1;
                        continue;
                        
                    }
                }

                string nwork;
                int end2 = text.IndexOf("{", cur + 1,StringComparison.InvariantCulture);
                if (end2 >= 0)
                {
                    nwork = text.Substring(cur, end2 - cur);
                    cur=end2;
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
        
        public void Resize(int width, int height, bool resize_borders)
        {

            if (ScriptInfo.Width == 0 || ScriptInfo.Height == 0)
                return;
            if (ScriptInfo.Width == width && ScriptInfo.Height == height)
                return;
            float multiplier=width/(float)ScriptInfo.Width;
            if (height/(float)ScriptInfo.Height<multiplier)
                multiplier = height / (float)ScriptInfo.Height;
            ScriptInfo.Width=width;
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