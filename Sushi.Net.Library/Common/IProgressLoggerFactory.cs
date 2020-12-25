using Microsoft.Extensions.Logging;

namespace Sushi.Net.Library.Common
{
    public interface IProgressLoggerFactory
    {
        IProgressLogger CreateProgressLogger(ILogger logger);
    }
}