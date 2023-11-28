using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Thinktecture.Extensions.Configuration;

namespace Sushi.Net.Library.Tools
{
    public class Tool
    {
        internal string _exename;
        internal string _exepath;
        internal ILogger _logger;
        internal IProgressLoggerFactory _progres;
        internal IGlobalCancellation _cancellation;
        internal ILoggingConfiguration _cfg;
        internal Tool(ILogger logger, IProgressLoggerFactory progress, IGlobalCancellation cancellation, ILoggingConfiguration cfg, string name)
        {
            _exename = name;
            _logger = logger;
            _exename = name;
            _progres = progress;
            _cancellation = cancellation;
            _cfg=cfg;
            _exepath = GetInstallPath(name);
        }


        public bool IsAvailable => _exepath != null;
        public virtual Command Command => Cli.Wrap(_exepath.Strip());

        public Command AddStandardErrorLogging(Command cmd, IPercentageProcessor percentageProcessor = null, IProgress<int> pro = null)
        {
            PipeTarget tg=PipeTarget.ToDelegate((a) =>
            {
                _logger.LogTrace(a);
                percentageProcessor?.ProcessProgress(a, pro);
            });
            return cmd.WithStandardErrorPipe(tg);
        }

        public IProgress<int> CreateProgress(IPercentageProcessor percentageProcessor)
        {
            if (_cfg is SushiLoggingConfiguration sl && percentageProcessor != null)
            {
                if (sl.LastLevel != LogLevel.Trace)
                {
                    IProgressLogger logger=_progres.CreateProgressLogger(_logger);
                    percentageProcessor.Init();
                    return logger.CreateProgress();
                }
            }

            return null;
        }
        public async Task<string> ExecuteAsync(Command cmd, bool useStdError=true, IPercentageProcessor percentageProcessor=null, Encoding enc=null)
        {
            try
            {
                IProgress<int> pro = CreateProgress(percentageProcessor);
                return await cmd.WithLogger(_logger, _cancellation.GetToken(), useStdError,percentageProcessor, pro, enc).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                _cfg.SetLevel(LogLevel.None);
                throw;
            }
            catch (OperationCanceledException)
            {
                _cfg.SetLevel(LogLevel.None);
                throw;
            }
        }
        
        public OSPlatform Platform
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return OSPlatform.Windows;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                    return OSPlatform.FreeBSD;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return OSPlatform.Linux;
                return OSPlatform.OSX;
            }
        }


        public void CheckIfRequired(bool bypass)
        {
            if (IsAvailable)
                return;
            if (!IsAvailable && !bypass)
                NotFound();
        }

        internal virtual string GetInstallPath(string fileName)
        {
            if (Platform == OSPlatform.Windows)
                fileName += ".exe";
            if (File.Exists(fileName.Strip()))
                return Path.GetFullPath(fileName);
            string basname = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(basname))
                return basname;
            var values = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(values))
                return null;
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        public void NotFound()
        {
            throw new SushiException($"Couldn't find {_exename}, check if it is installed");
        }
    }
}