using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sushi.Net.Library.Events.Subtitles.Aegis
{
    public class ScriptInfoList : List<string>
    {
        public const string PlayResX = "PlayResX";
        public const string PlayResY = "PlayResY";

        public int Width
        {
            get => GetValue(PlayResX);
            set => SetValue(PlayResX, value);
        }

        public int Height
        {
            get => GetValue(PlayResY);
            set => SetValue(PlayResY, value);
        }


        private string GetLineStartsWith(string str)
        {
            str += ":";
            return this.FirstOrDefault(a => a.StartsWith(str, StringComparison.InvariantCultureIgnoreCase));
        }

        private int ParseLine(int pos, string line)
        {
            if (line != null && line.Length > pos)
            {
                string val = line.Substring(pos).Trim();
                if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float r))
                    return (int) Math.Round(r);
            }

            return 0;
        }

        private int GetValue(string str)
        {
            string line = GetLineStartsWith(str);
            return ParseLine(str.Length + 1, line);
        }

        private void SetValue(string str, int value)
        {
            string new_line = str + ": " + value;
            string line = GetLineStartsWith(str);
            if (line != null)
                this[IndexOf(line)] = new_line;
            else
                Add(new_line);
        }

        public override string ToString()
        {
            StringBuilder bld = new();
            bld.AppendLine("[Script Info]");
            ForEach(a => bld.AppendLine(a));
            return bld.ToString();
        }
    }
}