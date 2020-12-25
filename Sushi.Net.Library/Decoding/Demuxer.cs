using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Events;
using Sushi.Net.Library.Media;
using Sushi.Net.Library.Tools;

namespace Sushi.Net.Library.Decoding
{
    public class Demuxer
    {
        private FFMpeg _ffmpeg;
        private MkvExtract _extract;
        private SCXviD _scx;
        private ILogger _logger;

        public Demuxer(FFMpeg ffmpeg, MkvExtract extract, SCXviD scx, ILogger<Demuxer> logger)
        {
            _ffmpeg = ffmpeg;
            _extract = extract;
            _scx = scx;
            _logger = logger;
        }

        public Task<MediaInfo> GetMediaInfoAsync(string path)
        {
            _logger.LogInformation($"Getting Media information for {path}");
            return _ffmpeg.GetMediaInfoAsync(path);
        }

        public Task<(List<(float start, float end)>,float vol)> FindSilencesAsync(string path, int? index, float silence_length, int silence_threshold)
        {
            return _ffmpeg.FindSilencesAsync(path, index, silence_length, silence_threshold);
        }
        public async Task<Mux> CreateAsync(string path)
        {
            Mux m = new Mux(this, path, _logger);
            await m.GetMediaInfoAsync().ConfigureAwait(false);
            return m;
        }
        public Task ShiftAudioAsync(Mux mux, string outputpath, List<Split> splits)
        {
            return _ffmpeg.ShiftAudioAsync(mux, outputpath, splits);
        }
        internal async Task ProcessAsync(Mux mux)
        {
            
            if (mux.WriteChapters)
            {
                Chapters chap = mux.MediaInfo.Chapters;
                await File.WriteAllTextAsync(mux.ChaptersPath, chap.ToOgmChapter()).ConfigureAwait(false);
            }
            if (mux.MakeKeyframes)
                await _scx.MakeKeyframes(mux.Path, mux.KeyFramesPath).ConfigureAwait(false);
            
            if (mux.MakeTimecodes)
            {
                if (mux.Path.GetExtension().ToLowerInvariant() == ".mkv")
                {
                    await _extract.ExtractTimeCodesAsync(mux.Path, mux.MediaInfo.Videos[0].Id, mux.TimeCodesPath).ConfigureAwait(false);
                }
                mux.VideoStream = mux.MediaInfo.Videos[0];
            }
            
            _logger.LogInformation($"Demuxing/Converting {mux.Path}...");
            await _ffmpeg.DeMux(mux).ConfigureAwait(false);
        }
    }
}
