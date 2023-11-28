using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sushi.Net.Library.Script
{
    public class LineCounter 
    {
        public List<string> Lines { get;  }
        public int Count { get; set; }
        public LineCounter(List<string> lines)
        {
            Lines = lines;
            Count = 0;
        }

        public string GetNext()
        {
            do
            {
                if (Lines == null || Lines.Count == Count)
                    return null;
                string str = Lines[Count++]?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(str))
                {
                    if (!str.StartsWith("//"))
                    {
                        int a = str.IndexOf("//", StringComparison.Ordinal);
                        if (a > 0)
                            str = str.Substring(0, a).Trim();
                        return str;
                    }
                }
            } while (true);
        }

        public string[] GetNextSplited()
        {
            string s = GetNext();
            if (s==null)
                return null;
            return split(s,' ').ToArray();
        }

        public static List<string> split(string stringToSplit, params char[] delimiters)
        {
            List<string> results = new List<string>();

            bool inQuote = false;
            StringBuilder currentToken = new StringBuilder();
            for (int index = 0; index < stringToSplit.Length; ++index)
            {
                char currentCharacter = stringToSplit[index];
                if (currentCharacter == '"')
                {
                    // When we see a ", we need to decide whether we are
                    // at the start or send of a quoted section...
                    inQuote = !inQuote;
                }
                else if (delimiters.Contains(currentCharacter) && inQuote == false)
                {
                    // We've come to the end of a token, so we find the token,
                    // trim it and add it to the collection of results...
                    string result = currentToken.ToString().Trim();
                    if (result != "") results.Add(result);

                    // We start a new token...
                    currentToken = new StringBuilder();
                }
                else
                {
                    // We've got a 'normal' character, so we add it to
                    // the curent token...
                    currentToken.Append(currentCharacter);
                }
            }

            // We've come to the end of the string, so we add the last token...
            string lastResult = currentToken.ToString().Trim();
            if (lastResult != "") results.Add(lastResult);

            return results;
        }
    }
}