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
        private FFProbe _probe;
        private FFMpeg _ffmpeg;
        private MkvExtract _extract;
        private SCXviD _scx;
        private ILogger _logger;

        public Demuxer(FFMpeg ffmpeg, FFProbe probe, MkvExtract extract, SCXviD scx, ILogger<Demuxer> logger)
        {
            _ffmpeg = ffmpeg;
            _extract = extract;
            _scx = scx;
            _logger = logger;
            _probe = probe;
        }

        public Task PopulateMediaInfoAsync(Mux info, string path)
        {
            return _probe.PopulateMediaInfoAsync(info, path);
        }


        public async Task<Mux> CreateAsync(string path)
        {
            Mux m = new Mux(this, path, _logger);
            await m.GetMediaInfoAsync().ConfigureAwait(false);
            return m;
        }

        public Task ShiftAudioAsync(AudioMedia stream, string outputpath, List<IShiftBlock> blocks, string temppath)
        {
            return _ffmpeg.ShiftAudioAsync(stream, outputpath, blocks, temppath);
        }
        internal async Task ProcessAsync(Mux mux)
        {
            
            if (mux.WriteChapters)
            {
                Chapters chap = mux.Chapters;
                await File.WriteAllTextAsync(mux.ChaptersPath, chap.ToOgmChapter()).ConfigureAwait(false);
            }
            if (mux.MakeKeyframes)
                await _scx.MakeKeyframes(mux.Path, mux.KeyFramesPath).ConfigureAwait(false);
            
            if (mux.MakeTimecodes)
            {
                if (mux.Path.GetExtension().ToLowerInvariant() == ".mkv")
                {
                    await _extract.ExtractTimeCodesAsync(mux.Path, mux.Videos[0].Info.Id, mux.TimeCodesPath).ConfigureAwait(false);
                }
            }
            
            _logger.LogInformation($"Demuxing/Converting {mux.Path}...");
            await _ffmpeg.DeMux(mux).ConfigureAwait(false);
        }
    }
}
