using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Span = Codex.Utilities.Range;

namespace Codex.Utilities
{
    public static class StringExtensions
    {
        public static bool HasExtension(this string path, params string[] extensions)
        {
            var actualExtension = Path.GetExtension(path).TrimStart('.');
            return actualExtension != null
                && extensions.Any(e => actualExtension.Equals(e, StringComparison.OrdinalIgnoreCase));
        }

        public static bool ContainsIgnoreCase(this string s, string value)
        {
            return s.IndexOf(value, StringComparison.OrdinalIgnoreCase) > -1;
        }

        public static string TrimEndIgnoreCase(this string s, string value)
        {
            if (s.EndsWith(value, StringComparison.OrdinalIgnoreCase))
            {
                return s.Substring(0, s.Length - value.Length);
            }

            return s;
        }

        public static bool IsArgument(this string argument, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(argumentName))
            {
                return false;
            }

            if (argument.StartsWith("/") || argument.StartsWith("-"))
            {
                argument = argument.Substring(1);
            }

            return string.Equals(argument, argumentName);
        }

        private static readonly char[] lineBreakCharacters = new char[] { '\r', '\n' };

        public static bool IsLineBreakChar(this char c)
        {
            return c == '\r' || c == '\n';
        }

        public static string EscapeLineBreaks(this string text)
        {
            if (text == "\r\n")
            {
                return @"\r\n";
            }
            else if (text == "\n")
            {
                return @"\n";
            }

            return text
                .Replace("\r\n", @"\r\n")
                .Replace("\n", @"\n")
                .Replace("\r", @"\r")
                .Replace("\u0085", @"\u0085")
                .Replace("\u2028", @"\u2028")
                .Replace("\u2029", @"\u2029");
        }

        public static string GetFirstLine(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            int cr = text.IndexOf('\r');
            int lf = text.IndexOf('\n');
            if (cr > 0)
            {
                if (lf > 0 && lf < cr)
                {
                    cr = lf;
                }

                text = text.Substring(0, cr);
            }
            else if (lf > 0)
            {
                text = text.Substring(0, lf);
            }

            return text;
        }

        public static string ReplaceIgnoreCase(this string input, string oldValue, string newValue)
        {
            oldValue = Regex.Escape(oldValue);
            return Regex.Replace(input, oldValue, newValue, RegexOptions.IgnoreCase);
        }

        public static string ToUpper(this Guid guid)
        {
            return guid.ToString("B").ToUpperInvariant();
        }

        public static void CollectLineSpans(this string text, ICollection<Span> spans, bool includeLineBreakInSpan = true)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (spans == null)
            {
                throw new ArgumentNullException(nameof(spans));
            }

            if (text.Length == 0)
            {
                return;
            }

            int currentPosition = 0;
            int currentLineLength = 0;
            bool previousWasCarriageReturn = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (previousWasCarriageReturn)
                    {
                        int lineLengthIncludingLineBreak = currentLineLength;
                        if (!includeLineBreakInSpan)
                        {
                            currentLineLength--;
                        }

                        spans.Add(new Span(currentPosition, currentLineLength));

                        currentPosition += lineLengthIncludingLineBreak;
                        currentLineLength = 1;
                    }
                    else
                    {
                        currentLineLength++;
                        previousWasCarriageReturn = true;
                    }
                }
                else if (text[i] == '\n')
                {
                    var lineLength = currentLineLength;
                    if (previousWasCarriageReturn)
                    {
                        lineLength--;
                    }

                    currentLineLength++;
                    previousWasCarriageReturn = false;
                    if (includeLineBreakInSpan)
                    {
                        lineLength = currentLineLength;
                    }

                    spans.Add(new Span(currentPosition, lineLength));
                    currentPosition += currentLineLength;
                    currentLineLength = 0;
                }
                else
                {
                    if (previousWasCarriageReturn)
                    {
                        var lineLength = currentLineLength;
                        if (!includeLineBreakInSpan)
                        {
                            lineLength--;
                        }

                        spans.Add(new Span(currentPosition, lineLength));
                        currentPosition += currentLineLength;
                        currentLineLength = 0;
                    }

                    currentLineLength++;
                    previousWasCarriageReturn = false;
                }
            }

            var finalLength = currentLineLength;
            if (previousWasCarriageReturn && !includeLineBreakInSpan)
            {
                finalLength--;
            }

            spans.Add(new Span(currentPosition, finalLength));

            if (previousWasCarriageReturn)
            {
                spans.Add(new Span(currentPosition, 0));
            }
        }

        private static readonly IReadOnlyList<Span> EmptySpanList = new Span[] { default(Span) };

        public static IReadOnlyList<Span> GetLineSpans(this string text, bool includeLineBreakInSpan = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return EmptySpanList;
            }

            var result = new List<Span>();
            text.CollectLineSpans(result, includeLineBreakInSpan);
            return result.ToArray();
        }

        public static IReadOnlyList<string> GetLines(this string text, bool includeLineBreak = false)
        {
            if (text == null)
            {
                return Array.Empty<string>();
            }

            return GetLineSpans(text, includeLineBreakInSpan: includeLineBreak)
                .Select(span => text.Substring(span.Start, span.Length))
                .ToArray();
        }

        public static IEnumerable<string> WhereNotNullOrEmpty(this IEnumerable<string> strings)
        {
            return strings.Where(s => !string.IsNullOrEmpty(s));
        }
    }
}
