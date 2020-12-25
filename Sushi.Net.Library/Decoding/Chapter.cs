using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sushi.Net.Library.Common;

namespace Sushi.Net.Library.Decoding
{
    public class Chapters
    {
        private readonly  List<float> times=new List<float>();

        public List<float> Times => times;

        private static Regex XmlRegex = new Regex(@"<ChapterTimeStart>(\d+:\d+:\d+\.\d+)</ChapterTimeStart>", RegexOptions.Compiled);
        private static Regex OgmRegex = new Regex(@"CHAPTER\d+=(\d+:\d+:\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Chapters(IEnumerable<string> chapters)
        {
            times = new List<float>();
            foreach (string chap in chapters)
            {
                float[] vals = chap.Split(":").Select(Convert.ToSingle).ToArray();
                times.Add(vals[0]*3600+vals[1]*60+vals[2]);
            }
            if (times[0]!=0)
                times.Insert(0,0);
        }


        public Chapters(IEnumerable<Match> matches)
        {
            times = matches.Select(a => Convert.ToSingle(a.Groups[1].Value)).ToList();
        }
        private static Chapters CreateFromMatchCollection(MatchCollection m)
        {
            List<string> values = m.Where(a => a.Success).Select(a => a.Groups[1].Value).ToList();
            return new Chapters(values);
        }

        public static Chapters CreateFromTextXML(string text)
        {
            return CreateFromMatchCollection(XmlRegex.Matches(text));

        }
        public static async Task<Chapters> CreateFromFileXMLAsync(string path)
        {
            string text=await path.ReadAllTextAsync().ConfigureAwait(false);
            return CreateFromTextXML(text);
        }
        public static Chapters CreateFromTextOGM(string text)
        {
            return CreateFromMatchCollection(OgmRegex.Matches(text));
        }
        public static async Task<Chapters> CreateFromFileOGMAsync(string path)
        {
            string text = await path.ReadAllTextAsync().ConfigureAwait(false);
            return CreateFromTextOGM(text);
        }
        public string ToOgmChapter()
        {
            if (times.Count == 0)
                return string.Empty;
            StringBuilder bld = new StringBuilder();
            bld.Append("\n");
            for (int x = 0; x < times.Count; x++)
                bld.Append(string.Format("{0:D2}={1}\nCHAPTER{0:D2}NAME=\n", x + 1, times[x].FormatSrtTime().Replace(",", ".")));
            return bld.ToString();
        }
    }
}
