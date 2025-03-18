using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Decoding
{
    public class Mux : MediaInfo
    {
        private Demuxer _demuxer;
        private ILogger _logger;

        public string Path { get; private set; }
        public string TimeCodesPath { get; private set; }
        public string KeyFramesPath { get; private set; }
        public string ChaptersPath { get; private set; }
        public bool MakeTimecodes { get; private set; }
        public bool MakeKeyframes { get; private set; }
        public bool WriteChapters { get; private set; }
        public bool HasVideos => Videos?.Count > 0;
        public bool HasSubtitles => Subtitles?.Count > 0;
        public bool HasAudios => Audios?.Count > 0;
        public string AudioOutputCodec { get; set; }
        public string AudioOutputCodecParameters { get; set;}
        public Demuxer Demuxer => _demuxer;

        public double ReScale { get; set; } = 0d;

        public int LimitSeconds = 0;

        public Mux CloneForSync(int? audio_id)
        {
            Mux n = new Mux(_demuxer,Path,_logger);
            n.Videos = n.Videos.ToList();
            n.LimitSeconds = 0;
            
            n.Audios = new List<AudioMedia> { this.SelectStream<AudioMedia>(Audios, audio_id, "audio") };
            n.AudioOutputCodec = AudioOutputCodec;
            n.AudioOutputCodecParameters = AudioOutputCodecParameters;
            return n;
        }

        internal Mux(Demuxer demuxer, string path, ILogger logger)
        {
            _logger = logger;
            _demuxer = demuxer;
            Path = path;
            Subtitles = new List<SubtitleMedia>();
            Audios = new List<AudioMedia>();
            Videos = new List<VideoMedia>();
        }

        internal Task GetMediaInfoAsync()
        {
            return _demuxer.PopulateMediaInfoAsync(this, Path);
        }
        public Task ProcessAsync()
        {
            return _demuxer.ProcessAsync(this);
        }
        

        private static string FormatStream<T>(T stream) where T: Media
        {
            string rtitle = !string.IsNullOrEmpty(stream.Info.Title) ? " (" + stream.Info.Title + ")" : "";
            return $"{stream.Info.Id}{rtitle}: {stream.Info.MediaType.ToString()}";
        }

        private static string FormatStreamList<T>(List<T> streams) where T : Media
        {
            return string.Join("\n", streams.Select(FormatStream));
        }

        public async Task<AudioMedia> ObtainMediaInfoForProcessAsync(int? idx, Settings.SushiSettings args, bool shouldProcess = true)
        {
            await GetMediaInfoAsync().ConfigureAwait(false);
            AudioMedia audio = GetAudioStream(idx);
            audio.ShouldProcess = shouldProcess;
            audio.SetPaths(args.TempDir, args.Output);
            audio.SetSilenceSearch(args.SilenceMinLength,args.SilenceThreshold);
            return audio;
        }


        public AudioMedia GetAudioStream(int? idx) => SelectStream(Audios, idx, "audio");
        public VideoMedia GetVideoStream(int? idx) => SelectStream(Videos, idx, "video");
        public SubtitleMedia GetSubtitleStream(int? idx) => SelectStream(Subtitles, idx, "subtitle");

        public int GetAudioIndex(int? index)
        {
            if (Audios.Count == 0)
                throw new ArgumentException("Cannot find audio, archive do not contains audio");
            AudioMedia media;
            if (index == null)
                media = Audios.FirstOrDefault(a => a.Info.Default) ?? Audios[0];
            else
                media = Audios.FirstOrDefault(a => a.Info.Id == index.Value);
            if (media == null)
                throw new ArgumentException($"Cannot find audio with index {index.Value}");
            return Audios.IndexOf(media);
        }

        public Media GetShiftableMedia(int idx)
        {
            return (Media)Audios.FirstOrDefault(a => a.Info.Id == idx) ?? (Media)Subtitles.FirstOrDefault(a => a.Info.Id == idx);
        }
        private T SelectStream<T>(List<T> streams, int? idx, string name) where T : Media
        {
            if (streams==null || streams.Count==0)
                throw new SushiException($"No {name} streams found in {Path}");
            if (!idx.HasValue)
            {
                if (streams.Count > 1)
                {
                    T default_track = streams.FirstOrDefault(a => a.Info.Default);
                    if (default_track != null)
                    {
                        _logger.LogWarning($"Using default track {FormatStream(default_track)} in {Path}");
                        return default_track;
                    }
                    throw new SushiException($"More than one {name} stream found in {Path}.You need to specify the exact one to demux. Here are all candidates:\n{FormatStreamList(streams)}");
                }

                return streams[0];
            }

            T stream = streams.FirstOrDefault(a => a.Info.Id == idx.Value);
            if (stream==null)
                throw new SushiException($"Stream with index {idx.Value} doesn't exist in {Path}.\nHere are all that do:\n{FormatStreamList(streams)}");
            return stream;

        }

        public void SetTimecodes(string output_path)
        {
            TimeCodesPath = output_path;
            MakeTimecodes = true;
        }
        public void SetChapters(string output_path)
        {
            ChaptersPath = output_path;
            WriteChapters = true;
        }
        public void SetKeyframes(string output_path)
        {
            KeyFramesPath = output_path;
            MakeKeyframes = true;
        }




        private void DeleteCollection<T>(List<T> medias) where T: Media
        {
            foreach (Media m in medias)
            {
                if (m.Processed && !string.IsNullOrEmpty(m.ProcessPath) && File.Exists(m.ProcessPath))
                    File.Delete(m.ProcessPath);
            }
        }

        public void CleanUp()
        {
            DeleteCollection(Audios);
            DeleteCollection(Subtitles);
            if (MakeTimecodes)
                File.Delete(TimeCodesPath);
            if (WriteChapters)
                File.Delete(ChaptersPath);
        }
    }
}