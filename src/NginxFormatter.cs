using System;
using System.Collections.Generic;
using System.Text;

namespace FrontendDeployTool
{
    internal static class NginxFormatter
    {
        public static string Format(string source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            List<string> output = new List<string>();
            int indent = 0;
            bool previousBlank = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    if (!previousBlank && output.Count > 0)
                    {
                        output.Add(string.Empty);
                    }
                    previousBlank = true;
                    continue;
                }

                previousBlank = false;
                int leadingClosings = CountLeadingClosings(line);
                indent = Math.Max(0, indent - leadingClosings);
                output.Add(new string(' ', indent * 4) + line);

                int opens;
                int closes;
                CountBraces(line, out opens, out closes);
                indent = Math.Max(0, indent + opens - Math.Max(0, closes - leadingClosings));
            }

            while (output.Count > 0 && output[output.Count - 1].Length == 0)
            {
                output.RemoveAt(output.Count - 1);
            }
            return string.Join(Environment.NewLine, output.ToArray()) + Environment.NewLine;
        }

        private static int CountLeadingClosings(string line)
        {
            int count = 0;
            int index = 0;
            while (index < line.Length)
            {
                while (index < line.Length && char.IsWhiteSpace(line[index]))
                {
                    index++;
                }
                if (index < line.Length && line[index] == '}')
                {
                    count++;
                    index++;
                    continue;
                }
                break;
            }
            return count;
        }

        private static void CountBraces(string line, out int opens, out int closes)
        {
            opens = 0;
            closes = 0;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            bool escaped = false;

            for (int i = 0; i < line.Length; i++)
            {
                char value = line[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (value == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (!inDoubleQuote && value == '\'')
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }
                if (!inSingleQuote && value == '"')
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }
                if (!inSingleQuote && !inDoubleQuote)
                {
                    if (value == '#')
                    {
                        break;
                    }
                    if (value == '{')
                    {
                        opens++;
                    }
                    else if (value == '}')
                    {
                        closes++;
                    }
                }
            }
        }
    }
}
