using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;

namespace Sushi.Net.ConsoleFormatter
{
    public class ConsoleProgressLoggerFactory : IProgressLoggerFactory
    {
        public IProgressLogger CreateProgressLogger(ILogger logger)
        {
            return new ConsoleProgressLogger(logger);
        }
    }
}