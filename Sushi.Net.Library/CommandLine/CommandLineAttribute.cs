using System;
using System.Collections.Generic;

namespace Sushi.Net.Library.CommandLine
{
    public class CommandLineAttribute : Attribute
    {
        public List<string> Alias { get; set; }
        public string Name { get; set; }
        public object Default { get; set; }
        public string Help { get; set; }
        public bool Multiple { get; set; }
        public bool Required { get; set; }

        public CommandLineAttribute(string key, object def = null, string help = null, string name = null, bool required = false, bool multiple = false, params string[] alias)
        {
            Alias = new List<string>();
            Alias.Add(key);
            if (alias != null && alias.Length > 0)
                Alias.AddRange(alias);
            Name = name;
            Default = def;
            Help = help;
            Required = required;
            Multiple = multiple;
        }

    }
}