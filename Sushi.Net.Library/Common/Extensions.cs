using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Tools;


namespace Sushi.Net.Library.Common
{
    public static class Extensions
    {
        public static string GetExtension(this string path)
        {
            return Path.GetExtension(path);
        }

        public static Task<string> ReadAllTextAsync(this string path)
        {
            return File.ReadAllTextAsync(path);
        }

        public static string FormatSrtTime(this float seconds)
        {
            long cs = (long) Math.Round(seconds * 1000);
            return $"{cs / 3600000:d2}:{cs / 60000 % 60:d2}:{cs / 1000 % 60:d2},{cs % 1000:d3}";
        }

        public static string FormatTime(this float seconds)
        {
            long cs = (long) Math.Round(seconds * 100);
            return $"{cs / 360000}:{cs / 6000 % 60:d2}:{cs / 100 % 60:d2}.{cs % 100:d2}";
        }

        public static float ParseAssTime(this string str)
        {
            float[] vals = str.Split(":").Select(Convert.ToSingle).ToArray();
            return vals[0] * 3600 + vals[1] * 60 + vals[2];
        }

        public static float Clip(this float value, float minimum, float maximum)
        {
            return Math.Max(Math.Min(value, maximum), minimum);
        }

        public static string Strip(this string str)
        {
            if (str == null)
                return null;
            return str.Trim(new char[] {'"', '\'', ' '});
        }

        public static string Quote(this string str, char quote = '"')
        {
            return quote + str.Trim(new char[] {'"', '\'', ' '}) + quote;
        }

        public static string LeftStrip(this string str)
        {
            return str.TrimStart(new char[] {'"', '\'', ' '});
        }

        public static float AbsDiff(this float a, float b)
        {
            return Math.Abs(a - b);
        }

        public static int BisectLeft(this List<float> list, float value)
        {
            int max = list.Count;
            if (value < list[0])
                return 0;
            if (value > list[^1])
                return max;
            int lower = 0;
            int x = 1;
            while (x < max && list[x] < value)
            {
                lower = x;
                x <<= 1;
            }

            while (lower < max && list[lower] < value)
                lower++;
            return lower;
        }

        public static List<(float, float)> ToTuple(this List<float> ls)
        {
            List<(float, float)> res = new List<(float, float)>();
            for (int x = 0; x < ls.Count; x += 2)
            {
                res.Add((ls[x], ls[x + 1]));
            }

            return res;
        }


        public static IEnumerable<string> SplitLines(this string input)
        {
            if (input == null)
            {
                yield break;
            }

            using (StringReader reader = new StringReader(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        public static void CheckFileExists(this string file, string title)
        {
            if (!string.IsNullOrEmpty(file) && !File.Exists(file))
                throw new SushiException($"{title} file doesn't exist");
        }

        public static string FormatFullPath(this string base_path, string postfix, string temp_dir = null)
        {
            if (!string.IsNullOrEmpty(temp_dir))
                return Path.Combine(temp_dir, Path.GetFileNameWithoutExtension(base_path) + postfix);
            return Path.Combine(Path.GetDirectoryName(base_path),Path.GetFileNameWithoutExtension(base_path) + postfix);
        }
        public static void ProcessProgress(this IPercentageProcessor processor, string text, IProgress<int> progress)
        {
            if (processor!=null)
                progress?.Report(processor.PercentageFromLine(text));
        }
        public static async Task<string> WithLogger(this Command cmd, ILogger logger, CancellationToken token, bool useStdError=true, IPercentageProcessor processor=null, IProgress<int> progress=null)
        {

            
            StringBuilder bld = new StringBuilder();
            await foreach (var cmdEvent in cmd.ListenAsync(token))
            {
                switch (cmdEvent)
                {
                    case StandardOutputCommandEvent stdOut:
                        if (!useStdError)
                        {
                            bld.AppendLine(stdOut.Text);
                            logger.LogTrace(stdOut.Text);
                            processor?.ProcessProgress(stdOut.Text,progress);
                        }

                        break;
                    case StandardErrorCommandEvent stdErr:
                        if (useStdError)
                        {
                            bld.AppendLine(stdErr.Text);
                            logger.LogTrace(stdErr.Text);
                            processor?.ProcessProgress(stdErr.Text, progress);
                        }
                        break;
                }
            }
            progress?.Report(100);
            return bld.ToString();
        }
        
        public static void CreateDirectoryIfNotExists(this string path)
        {
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static string GetFullPathWithoutExtension(this string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
        }

        public static string GetSubtitleWithNumber(this string path, string extension, int? idx)
        {
            if (idx == null)
                return path;
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path)) + $"_{idx.Value:D2}{extension}";
        }

        public static string NormalizePath(this string path)
        {
            if (path == null)
                return null;
            if (path.EndsWith("\\"))
                return path.Substring(0, path.Length - 1);
            if (path.EndsWith("/"))
                return path.Substring(0, path.Length - 1);
            return path;
        }

        public static void AddSushi<T>(this IServiceCollection service, GlobalCancellation globalCancel) where T : class, IProgressLoggerFactory
        {
            service.AddSingleton<Sushi>();
            service.AddSingleton<FFMpeg>();
            service.AddSingleton<MkvExtract>();
            service.AddSingleton<Sushi>();
            service.AddSingleton<SCXviD>();
            service.AddSingleton<Demuxer>();
            service.AddSingleton<AudioReader>();
            service.AddSingleton<Grouping>();
            service.AddSingleton<Shifter>();
            service.AddSingleton<BlockManipulation>();
            service.AddSingleton<IProgressLoggerFactory, T>();
            service.AddSingleton<IGlobalCancellation>(globalCancel);
        }
    }
}