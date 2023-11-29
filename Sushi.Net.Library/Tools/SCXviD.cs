using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Thinktecture.Extensions.Configuration;

namespace Sushi.Net.Library.Tools
{
    public class SCXviD : Tool
    {
        public const string scxvid = "SCXvid";

        private readonly FFMpeg _ffmpeg;


        public SCXviD(FFMpeg ffmpeg, ILogger<SCXviD> logger, IProgressLoggerFactory fact, IGlobalCancellation cancel, ILoggingConfiguration cfg) : base(logger, fact, cancel, cfg, scxvid)
        {
            _ffmpeg = ffmpeg;
        }


        public async Task MakeKeyframes(string video_path, string log_path)
        {
            StringBuilder bld = new StringBuilder();
            CheckIfRequired(false);
            _ffmpeg.CheckIfRequired(false);
            _logger.LogInformation($"Generating keyframes for {video_path}...");
            Command ffmpeg = _ffmpeg.Command.WithArguments("-i " + video_path.Quote() + " -f yuv4mpegpipe -vf scale=640:360 -pix_fmt yuv420p -vsync drop -").WithStandardErrorPipe(PipeTarget.ToStringBuilder(bld));
            _logger.LogDebug("CMD: " + ffmpeg.ToString());
            IPercentageProcessor p = new FFMpeg.FFMpegPercentageProcessor();
            IProgress<int> pro = CreateProgress(p);
            ffmpeg = AddStandardErrorLogging(ffmpeg, p,pro);
            Command scx = Command.WithArguments(log_path.Quote());
            Command piped = ffmpeg | scx;
            await ExecuteAsync(piped).ConfigureAwait(false);
            pro?.Report(100);
        }
    }
}