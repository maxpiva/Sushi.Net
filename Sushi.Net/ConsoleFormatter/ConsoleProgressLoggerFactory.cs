using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Settings;
using System.Diagnostics.CodeAnalysis;

namespace Sushi.Net.ConsoleFormatter
{
    [RequiresUnreferencedCode("DI")]
    public class ConsoleProgressLoggerFactory : IProgressLoggerFactory
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConsoleProgressLoggerFactory))]
        [ActivatorUtilitiesConstructor]
        public ConsoleProgressLoggerFactory()
        {

        }
        [RequiresUnreferencedCode("")]
        public IProgressLogger CreateProgressLogger(ILogger logger)
        {
            return new ConsoleProgressLogger(logger);
        }
    }
}