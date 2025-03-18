using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.ML;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Tools;
using UtfUnknown;

namespace Sushi.Net.Library.Common
{
    public static class Extensions
    {
        public static string GetExtension(this string path)
        {
            return Path.GetExtension(path);
        }
        private static Dictionary<string, string> maps = new Dictionary<string, string> {
            {
                "ssa",
                ".ass"},
            {
                "ass",
                ".ass"},
            {
                "subrip",
                ".srt"
            },            {
                "text",
                ".srt"
            },



        };

        public static string ToExtension(this string codec)
        {
            codec = codec.ToLowerInvariant();
            if (codec.StartsWith("pcm_"))
                // Use .wav for raw audio formats (http://trac.ffmpeg.org/wiki/audio%20types)
                return ".wav";
            if (maps.ContainsKey(codec))
                return maps[codec];
            return "." + codec;
        }

        public static string ToCodec(this string extension)
        {
            extension = extension.ToLowerInvariant();
            if (maps.ContainsValue(extension))
            {
                KeyValuePair<string, string> value = maps.FirstOrDefault(a => a.Value == extension);
                return value.Key;
            }

            return extension.Substring(1);
        }
        public static async Task<string> ReadAllTextAsync(this string path, bool FromContainer=true)
        {
            DetectionResult result = CharsetDetector.DetectFromFile(path); // or pass FileInfo
            DetectionDetail det=result.Detected;
            Encoding enc = result?.Detected?.Encoding ?? Encoding.GetEncoding(1252);
            return await File.ReadAllTextAsync(path, enc).ConfigureAwait(false);
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
        public static string FormatTime2(this float seconds)
        {
            long cs = (long) Math.Round(seconds * 1000);
            return $"{cs / 3600000}:{cs / 60000 % 60:d2}:{cs / 1000 % 60:d2}.{cs % 1000:d3}";
        }

        public static float ParseAssTime(this string str)
        {
            float[] vals = str.Split(":").Select(Convert.ToSingle).ToArray();
            return vals[0] * 3600 + vals[1] * 60 + vals[2];
        }
        public static float ParseTime(this string str)
        {
            float[] vals = str.Split(':','.').Select(Convert.ToSingle).ToArray();
            return (vals[0] * 3600000 + vals[1] * 60000 + vals[2] * 1000 + vals[3])/1000;

        }
        public static float Clip(this float value, float minimum, float maximum)
        {
            return Math.Max(Math.Min(value, maximum), minimum);
        }

        public static string Strip(this string str)
        {
            if (str == null)
                return null;
            return str.Trim(new char[] {'"', '\'', ' ','\0'});
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

        public static string SanitizeFileName(this string filename)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (filename.Contains(c))
                    filename = filename.Replace(c.ToString(), "");
            }
            while (filename.Contains("  "))
                filename = filename.Replace("  ", " ");
            return filename;
        }
        public static string FormatFullPath(this string base_path, string postfix, string temp_dir = null)
        {
            string filename = (Path.GetFileNameWithoutExtension(base_path) + postfix).SanitizeFileName();
            if (!string.IsNullOrEmpty(temp_dir))
                return Path.Combine(temp_dir, filename);
            return Path.Combine(Path.GetDirectoryName(base_path), filename);
        }
        public static void ProcessProgress(this IPercentageProcessor processor, string text, IProgress<int> progress)
        {
            if (processor!=null)
                progress?.Report(processor.PercentageFromLine(text));
        }
        public static Encoding GetEncoding(this string filename)
        {
            //FROM StackOverflow user: 2Toad
            //https://stackoverflow.com/a/19283954
            // Read the BOM
            var bom = new byte[4];
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0) return Encoding.UTF32; //UTF-32LE
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return new UTF32Encoding(true, true);  //UTF-32BE

            // We actually have no idea what the encoding is if we reach this point, so
            // you may wish to return null instead of defaulting to ASCII
            return null;
        }

        public static async Task<string> WithLogger(this Command cmd, ILogger logger, CancellationToken token, bool useStdError=true, IPercentageProcessor processor=null, IProgress<int> progress=null, Encoding enc=null)
        {

            
            StringBuilder bld = new StringBuilder();
            IAsyncEnumerable<CommandEvent> ev = enc == null ? cmd.ListenAsync(token) : cmd.ListenAsync(enc, token);
            await foreach (var cmdEvent in ev)
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
        public static void AddSushi<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(this IServiceCollection service, GlobalCancellation globalCancel) where T : class, IProgressLoggerFactory
        {
            service.AddSingleton<Sushi>();
            service.AddSingleton<FFMpeg>();
            service.AddSingleton<FFProbe>();
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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
