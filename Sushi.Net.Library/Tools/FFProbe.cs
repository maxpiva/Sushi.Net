using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Decoding;
using Sushi.Net.Library.Media;
using Thinktecture.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Sushi.Net.Library.Tools
{
    public class FFProbe : Tool
    {
        public const string ffprobe = "ffprobe";
        private static readonly Regex Start = new Regex(@"dts=(.*)", RegexOptions.Compiled);

        private static readonly string info= "-hide_banner -v quiet -print_format json -show_format -show_entries stream=index,codec_name,codec_type,width,height,sample_rate,channels,channel_layout,start_time,duration,bit_rate -show_entries stream_tags=title,language,handler_name -show_entries stream_disposition=default,forced,comment,hearing_impaired -show_entries chapters";


        public FFProbe(ILogger<FFProbe> logger, IProgressLoggerFactory fact, IGlobalCancellation cancel, ILoggingConfiguration cfg) : base(logger, fact, cancel, cfg, ffprobe)
        {
        }

        public async Task PopulateMediaInfoAsync(Mux r, string file) 
        {
            CheckIfRequired(false);
            _logger.LogInformation($"Getting Media Information from {file}...");
            Command cmd = Command.WithArguments($"{info} {file.Quote()}").WithValidation(CommandResultValidation.None);
            string res=await ExecuteAsync(cmd,false,null, Encoding.UTF8).ConfigureAwait(false);
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = JsonTypeInfoResolver.Combine(MyContext.Default, new DefaultJsonTypeInfoResolver())
            };
            Base b = new Base();
            try
            {
                b = JsonSerializer.Deserialize<Base>(res, options);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            if (b.chapters != null && b.chapters.Count > 0)
                r.Chapters = new Chapters(b.chapters.Select(a => float.Parse(a.start_time)));
            float duration = float.Parse(b.format.duration);
            r.Videos = new List<VideoMedia>();
            r.Audios = new List<AudioMedia>();
            r.Subtitles = new List<SubtitleMedia>();
           
            foreach (Stream s in b.streams)
            {
                MediaStreamInfo m = new MediaStreamInfo();
                m.Id = s.index;
                m.CodecName = s.codec_name;
                m.BitRate = !string.IsNullOrEmpty(s.bit_rate) ? int.Parse(s.bit_rate) : 0;
                m.Default = (s.disposition?.@default ?? 0) == 1;
                m.Forced = (s.disposition?.forced ?? 0) == 1;
                m.Comment = (s.disposition?.comment ?? 0) == 1;
                m.HearingImpaired = (s.disposition?.hearing_impaired ?? 0) == 1;
                m.Language = s.tags?.language;

                m.Title = s.tags?.title;
                if (!string.IsNullOrEmpty(s.start_time) && float.TryParse(s.start_time, out float re))
                    m.StartTime = re;
                float du = duration;
                if (!string.IsNullOrEmpty(s.duration) && float.TryParse(s.duration, out float d2))
                    du = d2;
                m.Duration = du;
                m.Extension = s.codec_name?.ToExtension();
                switch (s.codec_type)
                {
                    case "video":
                        m.Width = s.width;
                        m.Height = s.height;
                        m.MediaType = MediaStreamType.Video;
                        r.Videos.Add(new VideoMedia(r, m));
                        break;
                    case "audio":
                        m.Channels = s.channels ?? 1;
                        m.ChannelLayout = s.channel_layout;
                        if (!string.IsNullOrEmpty(s.sample_rate) && int.TryParse(s.sample_rate, out int ir))
                            m.SampleRate = ir;
                        m.MediaType = MediaStreamType.Audio;
                        r.Audios.Add(new AudioMedia(r, m));
                        break;
                    case "subtitle":
                        if (m.CodecName.Contains("pgs"))
                            continue;
                        if (string.IsNullOrEmpty(m.Title) && s.tags?.handler_name != null && s.tags.handler_name != "SubtitleHandler")
                            m.Title = s.tags.handler_name;
                        if (m.CodecName == "subrip")
                            m.Extension = ".srt";
                        m.MediaType = MediaStreamType.Subtitle;
                        r.Subtitles.Add(new SubtitleMedia(r,m));
                        break;
                }
            }

        }

        public async Task<long> GetAudioDelayAsync(string file, int audioindex)
        {
            CheckIfRequired(false);
            _logger.LogInformation($"Finding Audio Delay in {file}...");
            Command cmd = Command.WithArguments($"-select_streams a:{audioindex} -hide_banner -of default=noprint_wrappers=1 -show_entries packet=dts -read_intervals 0%+#1 {file.Quote()}").WithValidation(CommandResultValidation.None);
            string res=await ExecuteAsync(cmd,false,null, Encoding.UTF8).ConfigureAwait(false);
            Match vol = Start.Match(res);
            if (vol.Success)
            {
                string ms = vol.Groups[1].Value.Trim();
                if (long.TryParse(ms, out long r))
                {
                    if (r != 0)
                    {
                        _logger.LogInformation($"Found Audio Delay of {r}ms");
                    }
                    return r;
                }
            }

            return 0;
        }

    }


    [JsonSerializable(typeof(Base))]
    [JsonSerializable(typeof(Disposition))]
    [JsonSerializable(typeof(Tags))]
    [JsonSerializable(typeof(Stream))]
    [JsonSerializable(typeof(Chapter))]
    [JsonSerializable(typeof(Format))]
    [JsonSourceGenerationOptions]
    public partial class MyContext : JsonSerializerContext { }


    [JsonSerializable(typeof(Disposition))]
    public class Disposition
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Disposition))]
        [JsonConstructor]
        public Disposition()
        {

        }
        [JsonInclude]
        public int @default { get; set; }
        [JsonInclude]
        public int forced { get; set; }

        public int comment { get; set; }

        public int hearing_impaired { get; set; }
    }
    [JsonSerializable(typeof(Tags))]
    public class Tags
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Tags))]
        [JsonConstructor]
        public Tags()
        {

        }
        [JsonInclude]
        public string title { get; set; }
        [JsonInclude]
        public string language { get; set; }
        [JsonInclude]
        public string encoder { get; set; }
        [JsonInclude]
        public string handler_name { get; set; }
        [JsonInclude]
        public DateTime creation_time { get; set; }
    }
    [JsonSerializable(typeof(Stream))]
    public class Stream
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Stream))]
        [JsonConstructor]
        public Stream()
        {
            disposition = new Disposition();
            tags = new Tags();
        }
        [JsonInclude]
        public int index { get; set; }
        [JsonInclude]
        public string codec_type { get; set; }
        [JsonInclude]
        public string codec_name { get; set; }
        [JsonInclude]
        public int width { get; set; }
        [JsonInclude]
        public int height { get; set; }
        [JsonInclude]
        public string start_time { get; set; }
        [JsonInclude]
        public string sample_rate { get; set; }
        [JsonInclude]
        public string duration { get; set; }
        [JsonInclude]
        public string bit_rate { get; set; }
        [JsonInclude]
        public Disposition disposition { get; set; }
        [JsonInclude]
        public Tags tags { get; set; }
        [JsonInclude]
        public int? channels { get; set; }
        [JsonInclude]
        public string channel_layout { get; set; }
    }
    [JsonSerializable(typeof(Chapter))]
    public class Chapter
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Chapter))]
        [JsonConstructor]
        public Chapter()
        {
            tags = new Tags();
        }
        [JsonInclude]
        public long id { get; set; }
        [JsonInclude]
        public string time_base { get; set; }
        [JsonInclude]
        public long start { get; set; }
        [JsonInclude]
        public string start_time { get; set; }
        [JsonInclude]
        public long end { get; set; }
        [JsonInclude]
        public string end_time { get; set; }
        [JsonInclude]
        public Tags tags { get; set; }
    }
    [JsonSerializable(typeof(Format))]
    public class Format
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Format))]
        [JsonConstructor]
        public Format()
        {
            tags = new Tags();
        }
        [JsonInclude]
        public string filename { get; set; }
        [JsonInclude]
        public int nb_streams { get; set; }
        [JsonInclude]
        public int nb_programs { get; set; }
        [JsonInclude]
        public string format_name { get; set; }
        [JsonInclude]
        public string format_long_name { get; set; }
        [JsonInclude]
        public string start_time { get; set; }
        [JsonInclude]
        public string duration { get; set; }
        [JsonInclude]
        public string size { get; set; }
        [JsonInclude]
        public string bit_rate { get; set; }
        [JsonInclude]
        public int probe_score { get; set; }
        [JsonInclude]
        public Tags tags { get; set; }
    }
    [JsonSerializable(typeof(Base))]
    public class Base
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Base))]
        [JsonConstructor]

        public Base()
        {
            //Trimming Issues
            Stream s = new Stream();
            Chapter c = new Chapter();
            streams = new List<Stream>();
            chapters = new List<Chapter>();
            format = new Format();
        }
        [JsonInclude]
        public List<object> programs { get; set; }
        [JsonInclude]
        public List<Stream> streams { get; set; }
        [JsonInclude]
        public List<Chapter> chapters { get; set; }
        [JsonInclude]
        public Format format { get; set; }
    }
}