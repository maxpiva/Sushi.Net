using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;

namespace Sushi.Net.ConsoleFormatter
{
    public class ConsoleProgressLogger : IProgressLogger
    {
        private ILogger _logger;
        private int _lastsize;
        private int _lastval = -1;
        const char _block = '■';

        
        internal ConsoleProgressLogger(ILogger logger)
        {
            _logger=logger;
        }
        
        private void DoProgress(int percent)
        {
            if (percent == _lastval)
                return;
            string prefix = "\x01";
            if (percent == 100)
                prefix = "\x02";
            _lastval = percent;
            var p = (int)((percent / 2f) + .5f);
            StringBuilder bld = new StringBuilder();
            bld.Append("[");
            for (var i = 0; i < 50; ++i)
                bld.Append(i >= p ? ' ' : _block);
            bld.Append($"] {percent,3:##0}%");
            _lastsize=bld.Length;
            bld.Insert(0, prefix);
            _logger.LogInformation(bld.ToString());
        }
        
        public Progress<int> CreateProgress() => new Progress<int>(DoProgress);
    }
}