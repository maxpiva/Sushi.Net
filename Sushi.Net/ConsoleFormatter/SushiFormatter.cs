using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Sushi.Net.ConsoleFormatter
{

    public class SushiFormatter : Microsoft.Extensions.Logging.Console.ConsoleFormatter
    {
        private IDisposable _optionsReloadToken;

        public SushiFormatter(IOptionsMonitor<SushiFormatterOptions> options)
            : base(ConsoleFormatterNames.Simple)
        {
            ReloadLoggerOptions(options.CurrentValue);
            _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
        }

        internal SushiFormatterOptions FormatterOptions { get; set; }


        private void ReloadLoggerOptions(SushiFormatterOptions options)
        {
            FormatterOptions = options;
        }
        public string GetName(string category)
        {
            int idx = category.LastIndexOf(".");
            if (idx >= 0)
                return category.Substring(idx + 1);
            return category;
        }

        private bool firstwrite = true;
        private bool last_progress = false;
        private int last_length=0;
        private int last_category_length = 0;
        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && message == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(message))
                return;
            string nline = firstwrite ? "" : "\n";
            if (message == nline)
            {
                textWriter.Write(nline);
                return;
            }
            firstwrite=false;
            bool progress = false;
            bool progress_end=false;
            if (message[0] == '\x01')
            {
                progress = true;
                message = message.Substring(1);
            }
            else if (message[0] == '\x02')
            {
                progress_end=true;
                progress = true;
                message = message.Substring(1);
            }

            string erase = "";
            if (last_progress)
            {
                if (progress)
                {
                    erase = new string('\b', last_length);
                }
                else
                {
                    erase = new string('\b', last_length + last_category_length);
                    nline = "";
                }
            }
            if (!string.IsNullOrEmpty(erase))           
                textWriter.Write(erase);
            message = message.Trim();
            message = message.Replace("\r", string.Empty);
 
            if (message[message.Length-1]=='\n')
                message = message.Substring(0, message.Length - 1);
            if (progress && last_progress)
            {
                last_length = message.Length;
                last_progress = (progress && !progress_end);
                textWriter.Write(message);
                return;
            }

            last_length = message.Length;
            last_progress = (progress && !progress_end);
            ConsoleColors logLevelColors = GetLogLevelConsoleColors(logEntry.LogLevel);
            string obj = "[" + GetName(logEntry.Category) + "]";
            last_category_length = obj.Length+1;
            string padding = "\n"+new string(' ', last_category_length);
            textWriter.Write(nline);
            textWriter.WriteColoredMessage(obj, logLevelColors.Background, logLevelColors.Foreground);
            textWriter.WriteColoredMessage(" ",null,null);
            textWriter.Write(message.Replace("\n",padding));
        }

        private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
        {
            bool disableColors = (FormatterOptions.ColorBehavior == LoggerColorBehavior.Disabled) ||
                                 (FormatterOptions.ColorBehavior == LoggerColorBehavior.Default && System.Console.IsOutputRedirected);
            if (disableColors)
            {
                return new ConsoleColors(null, null);
            }
            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            return logLevel switch
            {
                LogLevel.Trace => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Debug => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
                LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
                LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.DarkRed),
                LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.DarkRed),
                _ => new ConsoleColors(null, null)
            };
        }


       

    }
}