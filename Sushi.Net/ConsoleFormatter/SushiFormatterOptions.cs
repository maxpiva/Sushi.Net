using Microsoft.Extensions.Logging.Console;

namespace Sushi.Net.ConsoleFormatter
{
    public class SushiFormatterOptions : ConsoleFormatterOptions
    {
        public SushiFormatterOptions() { }

        /// <summary>
        /// Determines when to use color when logging messages.
        /// </summary>
        public LoggerColorBehavior ColorBehavior { get; set; }

        /// <summary>
        /// When <see langword="false" />, the entire message gets logged in a single line.
        /// </summary>
        public bool SingleLine { get; set; }
    }
}