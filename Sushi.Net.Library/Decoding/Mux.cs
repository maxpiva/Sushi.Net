using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Audio;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Media;

namespace Sushi.Net.Library.Decoding
{
    public class Mux
    {
        private Demuxer _demuxer;
        private ILogger _logger;

        public string Path { get; private set; }
        public MediaStreamInfo AudioStream { get; private set; }
        public string AudioPath { get; private set; }
        public int? AudioRate { get; private set; }
        public List<SubtitleStreamInfo> ScriptStreams { get; private set; }
        public List<string> ScriptPaths { get; private set; }
        public MediaStreamInfo VideoStream { get; set; }
        public string TimeCodesPath { get; private set; }
        public string KeyFramesPath { get; private set; }
        public string ChaptersPath { get; private set; }
        public bool IsWav { get; private set; }
        public bool DemuxAudio { get; private set; }
        public bool DemuxSubs { get; private set; }
        public bool MakeTimecodes { get; private set; }
        public bool MakeKeyframes { get; private set; }
        public bool WriteChapters { get; private set; }
        public MediaInfo MediaInfo { get; private set; }
        public bool HasVideo => !IsWav && MediaInfo.Videos.Count > 0;
        public bool HasSubtitles => !IsWav && MediaInfo.Subtitles.Count>0;
        public bool Processed { get; private set; }

        public float NormalizeGain { get; private set; }
        public AudioPostProcess AudioProcess { get; private set; }
        
        internal Mux(Demuxer demuxer, string path, ILogger logger)
        {
            _logger = logger;
            _demuxer = demuxer;
            Path = path;
            ScriptStreams = new List<SubtitleStreamInfo>();
            ScriptPaths = new List<string>();
            IsWav = path.GetExtension().ToLowerInvariant() == ".wav";
        }

        internal async Task GetMediaInfoAsync()
        {
            if (!IsWav)
                MediaInfo = await _demuxer.GetMediaInfoAsync(Path).ConfigureAwait(false);
        }
        public Task ShiftAudioAsync(string outputpath, List<Split> splits)
        {
            return _demuxer.ShiftAudioAsync(this, outputpath, splits);
        }
        
        internal async Task<List<(float start, float end)>> FindSilencesAsync(int? index, float silence_length, int silence_threshold)
        {
            (List<(float start, float end)> l, float val)=await _demuxer.FindSilencesAsync(Path, index, silence_length, silence_threshold).ConfigureAwait(false);
            NormalizeGain = val;
            return l;
            

        }
        private static string FormatStream<T>(T stream) where T: MediaStreamInfo
        {
            string rtitle = !string.IsNullOrEmpty(stream.Title) ? " (" + stream.Title + ")" : "";
            return $"{stream.Id}{rtitle}: {stream.Info}";
        }

        private static string FormatStreamList<T>(List<T> streams) where T : MediaStreamInfo
        {
            return string.Join("\n", streams.Select(FormatStream));
        }
        private T SelectStream<T>(List<T> streams, int? idx, string name) where T : MediaStreamInfo
        {
            if (streams==null || streams.Count==0)
                throw new SushiException($"No {name} streams found in {Path}");
            if (!idx.HasValue)
            {
                if (streams.Count > 1)
                {
                    T default_track = streams.FirstOrDefault(a => a.Default);
                    if (default_track != null)
                    {
                        _logger.LogWarning($"Using default track {FormatStream(default_track)} in {Path}");
                        return default_track;
                    }
                    throw new SushiException($"More than one {name} stream found in {Path}.You need to specify the exact one to demux. Here are all candidates:\n{FormatStreamList(streams)}");
                }

                return streams[0];
            }

            T stream = streams.FirstOrDefault(a => a.Id == idx.Value);
            if (stream==null)
                throw new SushiException($"Stream with index {idx.Value} doesn't exist in {Path}.\nHere are all that do:\n{FormatStreamList(streams)}");
            return stream;

        }




        public void SetAudio(int? stream_idx, string output_path, int? sample_rate, AudioPostProcess process=AudioPostProcess.SubtitleSearch)
        {
            AudioStream = SelectStream(MediaInfo.Audios, stream_idx, "audio");
            AudioPath = output_path;
            AudioRate = sample_rate;
            AudioProcess = process;
            DemuxAudio = true;
        }

        public void SetScript(int? stream_idx, string output_path)
        {
            ScriptStreams.Add(SelectStream(MediaInfo.Subtitles, stream_idx, "subtitles"));
            ScriptPaths.Add(output_path);
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

        public string GetSubType(int? index)
        {
            return SelectStream(MediaInfo.Subtitles, index, "subtitles").Type;
        }

        public Task ProcessAsync()
        {

            Processed = true;
            return _demuxer.ProcessAsync(this);
        }

        
        public void CleanUp()
        {
            if (DemuxAudio)
                File.Delete(AudioPath);
            if (DemuxSubs)
                ScriptPaths.ForEach(File.Delete);
            if (MakeTimecodes)
                File.Delete(TimeCodesPath);
            if (WriteChapters)
                File.Delete(ChaptersPath);
        }
    }
}