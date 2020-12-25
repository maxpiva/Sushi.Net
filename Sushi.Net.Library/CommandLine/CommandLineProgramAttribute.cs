using System;

namespace Sushi.Net.Library.CommandLine
{
    public class CommandLineProgramAttribute : Attribute
    {

        public string Description { get; set; }

        public CommandLineProgramAttribute(string desc = null)
        {
            Description = desc;
        }
    }
}