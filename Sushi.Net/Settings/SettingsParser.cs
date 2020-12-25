using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Sushi.Net.Library.CommandLine;

namespace Sushi.Net.Settings
{
    public class SettingsParser<T>
    {
        private Argument CreateGenericArgument(Type t)
        {
            var dataType = new Type[] {t};
            var combinedType = typeof(Argument<>).MakeGenericType(dataType);
            return (Argument) Activator.CreateInstance(combinedType);
        }

        public RootCommand GetRootCommand(Func<T, IHost, Task> action)
        {

            CommandLineProgramAttribute programattr = (CommandLineProgramAttribute) Attribute.GetCustomAttribute(typeof(T), typeof(CommandLineProgramAttribute));
            RootCommand root = new RootCommand(programattr?.Description ?? "");
            foreach (var prop in typeof(T).GetProperties())
            {
                CommandLineAttribute attr = (CommandLineAttribute) prop.GetCustomAttribute(typeof(CommandLineAttribute));
                if (attr != null)
                {
                    string help = attr.Help ?? "";
                    Option opt = new Option(attr.Alias.First(), help);
                    opt.Argument = CreateGenericArgument(prop.PropertyType);
                    opt.Argument.Name = attr.Name;
                    if (attr.Default!=null)
                        opt.Argument.SetDefaultValue(attr.Default);
                    opt.IsRequired = attr.Required;
                    opt.AllowMultipleArgumentsPerToken = attr.Multiple;

                    if (attr.Alias.Count > 1)
                        attr.Alias.Skip(1).ToList().ForEach(a => opt.AddAlias(a));
                    root.AddOption(opt);
                }
            }
            root.Handler = CommandHandler.Create<T, IHost>(action);
            return root;
        }
    }
}