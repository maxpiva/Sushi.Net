using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Thinktecture.Extensions.Configuration;

namespace Sushi.Net.Library.Tools
{
    public class MkvExtract : Tool, IPercentageProcessor
    {

        public const string mkvextract = "mkvextract";
        private static readonly Regex ProgressRegex = new Regex(@"Progress:(.*?)%", RegexOptions.Compiled);

        
        public MkvExtract(ILogger<MkvExtract> logger, IProgressLoggerFactory fact, IGlobalCancellation cancel, ILoggingConfiguration cfg) : base(logger, fact, cancel, cfg, mkvextract)
        {

        }

        internal override string GetInstallPath(string fileName)
        {
            string res = base.GetInstallPath(fileName);
            if (res != null)
                return res;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MKVToolNix", mkvextract + ".exe");
            if (File.Exists(path))
                return path;
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MKVToolNix", mkvextract + ".exe");
            if (File.Exists(path))
                return path;
            return null;
        }

        public async Task ExtractTimeCodesAsync(string file, int index, string outputpath)
        {
            CheckIfRequired(false);
            _logger.LogInformation($"Extracting TimeCodes from {file}...");
            string arguments = $"timecodes_v2 {file.Quote()} {index}:{outputpath.Quote()}";
            Command cmd = Command.WithArguments(arguments);
            await ExecuteAsync(cmd, false,this).ConfigureAwait(false);
        }

        private int _lastval = 0;
        public int PercentageFromLine(string line)
        {
            Match m = ProgressRegex.Match(line);
            if (m.Success)
            {
                if (int.TryParse(m.Groups[1].Value.Trim(), out int value))
                    _lastval=value;
            }

            return _lastval;
        }

        public void Init()
        {
            _lastval = 0;
        }
    }
}
