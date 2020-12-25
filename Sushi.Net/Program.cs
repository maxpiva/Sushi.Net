using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Sushi.Net.ConsoleFormatter;
using Sushi.Net.Library.Common;
using Sushi.Net.Library.Settings;
using Sushi.Net.Settings;
using Thinktecture;
using Thinktecture.Extensions.Configuration;
using Extensions = Sushi.Net.Library.Common.Extensions;

namespace Sushi.Net
{
    internal class Program
    {
        private static GlobalCancellation _globalCancellation;
        private static SushiLoggingConfiguration _lcfg;

        
        private static Task<int> Main(string[] args)
        {
            _globalCancellation = new GlobalCancellation();
            _lcfg = new SushiLoggingConfiguration();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("\nSIGINT received, exiting program...");
                _lcfg.SetLevel(LogLevel.None);
                _globalCancellation.Source.Cancel();
                Thread.Sleep(200);
                Environment.Exit(1);
            };

            return BuildCommandLine().UseHost(_ =>
            {
                return Host.CreateDefaultBuilder(args).ConfigureAppConfiguration(config =>
                {
                    config.Sources.Clear();
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);

                });
            }, ConfigureHostBuilder).UseDefaults().Build().InvokeAsync(args);
        }


        private static Task Process(SushiSettings settings, IHost host)
        {
            Library.Sushi sushi = host.Services.GetService<Library.Sushi>();
            return sushi?.ValidateAndProcess(settings) ?? Task.FromResult(0);
        }

        private static CommandLineBuilder BuildCommandLine()
        {
            SettingsParser<SushiSettings> n = new ();
            RootCommand cmd = n.GetRootCommand(Process);
            return new CommandLineBuilder(cmd);
        }

        private static void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            hostBuilder
                .ConfigureServices((_, services) =>
                {
                    services.AddLogging((conf) =>
                    {
                        conf.ClearProviders();
                        conf.AddConsole();
                        List<ServiceDescriptor> objs = conf.Services.Where(a => a.ServiceType.Name == "ConsoleFormatter").ToList();
                        foreach(ServiceDescriptor o in objs)
                            conf.Services.Remove(o);
                        conf.AddConsoleFormatter<SushiFormatter, SushiFormatterOptions>(opt => { opt.ColorBehavior = LoggerColorBehavior.Enabled; });
                            conf.AddFilter("Microsoft", LogLevel.None);
                    });
                    services.AddSushi<ConsoleProgressLoggerFactory>(_globalCancellation);
                    services.AddSingleton<ILoggingConfiguration>(_lcfg);
                })
                .ConfigureAppConfiguration(cfg => cfg.AddLoggingConfiguration(_lcfg, "Logging"));
        }
    }
}