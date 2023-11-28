using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OpenCvSharp.Aruco;
using Sushi.Net.Library.CommandLine;
using Sushi.Net.Library.Settings;

namespace Sushi.Net.Settings
{
    public class SettingsParser<T> where T:class, new()
    {
        private Argument CreateGenericArgument(Type t)
        {
            var dataType = new Type[] {t};
            var combinedType = typeof(Argument<>).MakeGenericType(dataType);
            return (Argument) Activator.CreateInstance(combinedType);
        }
        private Option CreateGenericOption(Type t, params object[] pars)
        {
            var dataType = new Type[] { t };
            var combinedType = typeof(Option<>).MakeGenericType(dataType);
            return (Option)Activator.CreateInstance(combinedType, pars);
        }
        public RootCommand GetRootCommand(Func<T, InvocationContext, Task> action)
        {

            CommandLineProgramAttribute programattr = (CommandLineProgramAttribute) Attribute.GetCustomAttribute(typeof(T), typeof(CommandLineProgramAttribute));
            RootCommand root = new RootCommand(programattr?.Description ?? "");
            Dictionary<PropertyInfo, Option> options = new Dictionary<PropertyInfo, Option>();
            foreach (var prop in typeof(T).GetProperties())
            {
                CommandLineAttribute attr = (CommandLineAttribute) prop.GetCustomAttribute(typeof(CommandLineAttribute));
                if (attr != null)
                {
                    string help = attr.Help ?? "";
                    Option opt = CreateGenericOption(prop.PropertyType, attr.Alias.ToArray(), help);
                    opt.IsRequired = attr.Required;
                    opt.AllowMultipleArgumentsPerToken = attr.Multiple;
                    opt.ArgumentHelpName = attr.Name;
                    if (attr.Default!=null)
                        opt.SetDefaultValue(attr.Default);
                    root.AddOption(opt);
                    options.Add(prop, opt);
                }
            }

            root.SetHandler((a)=>
            {
                T settings = new T();
                foreach (var prop in options.Keys)
                    prop.SetValue(settings, a.ParseResult.GetValueForOption(options[prop]));
                return action(settings, a);
            });

            return root;
        }
    }
}