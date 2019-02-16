using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Utilities;

namespace Codex.Storage.Utilities
{
    public static class FullTextUtilities
    {
        public const char StartOfLineSpecifierChar = '\u0011';
        public const char EndOfLineSpecifierChar = '\u0012';
        public const char HighlightStartTagChar = '\u0001';
        public const string HighlightStartTagCharString = "\u0001";
        public const char HighlightEndTagChar = '\u0002';
        public const string HighlightEndTagCharString = "\u0002";
        private const string HighlightStartTag = "<em>";
        private const string HighlightEndTag = "</em>";
        private const int MaxLineEncodingDistance = 40;
        private static readonly char[] NewLineChars = new[] { '\n', '\r' };

        public static string Capitalize(this string s)
        {
            StringBuilder sb = new StringBuilder();

            bool lastCharacterWasSeparator = true;
            foreach (var character in s)
            {
                var processedCharacter = character;

                if (character == '.')
                {
                    lastCharacterWasSeparator = true;
                }
                else if (lastCharacterWasSeparator)
                {
                    processedCharacter = char.ToUpperInvariant(character);
                    lastCharacterWasSeparator = false;
                }

                sb.Append(processedCharacter);
            }

            return sb.ToString();
        }

        public static string EncodeLineSpecifier(string lineSpecifier)
        {
            return StartOfLineSpecifierChar + lineSpecifier + EndOfLineSpecifierChar;
        }

        public static IEnumerable<TextLineSpan> ParseHighlightSpans(string highlight, int lineOffset)
        {
            List<TextLineSpan> spans = new List<TextLineSpan>(1);

            using (var sbLease = Pools.StringBuilderPool.Acquire())
            {
                var sb = sbLease.Instance;
                sb.Append("<r>");
                foreach (var lineSpan in highlight.EnumerateLineSpans(includeLineBreakInSpan: false))
                {
                    sb.Append("<l>");
                    sb.Append(highlight.Substring(lineSpan.Start, lineSpan.Length));
                    var lineElementText = $"<r>{lineSpan}</r>";
                    sb.Append("</l>");
                }
                sb.Append("</r>");

                XElement element = XElement.Parse(sb.ToString(), LoadOptions.PreserveWhitespace);
                int? lineIndex = null;

                foreach (var lineElement in element.Elements())
                {
                    lineIndex++;

                    TextLineSpan span = null;
                    sb.Clear();

                    int end = 0;

                    foreach (var child in lineElement.Nodes())
                    {
                        if (child is XElement childElement)
                        {
                            var name = childElement.Name.LocalName;
                            if (name == "fv")
                            {
                                lineIndex = lineOffset + (int)childElement.Attribute("idx");
                                continue;
                            }

                            if (name == "em")
                            {
                                span = span ?? new TextLineSpan()
                                {
                                    // Set start to non-zero value to indicate that the line index hasn't been set
                                    // yet
                                    Start = short.MaxValue,
                                    LineSpanStart = sb.Length
                                };

                                sb.Append(childElement.Value);
                                end = sb.Length;
                            }
                        }
                        else if (child is XText text)
                        {
                            sb.Append(text.Value);
                        }
                    }

                    if (span != null)
                    {
                        if (lineIndex != null)
                        {
                            // Set start to 0 to indicate that the span has the right line index set
                            span.Start = 0;
                        }

                        span.LineIndex = lineIndex ?? lineOffset;
                        span.Length = end - span.LineSpanStart;
                    }

                    span.LineSpanText = sb.ToString();
                    spans.Add(span);
                }
            }

            // Moving in reverse, try to set the line index for span i based on line index for span i + 1
            int lastLineIndex = lineOffset;
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                var span = spans[i];
                if (span.Start != 0)
                {
                    span.Start = 0;
                    span.LineIndex = lastLineIndex;
                    span.Trim();
                }
                else
                {
                    lastLineIndex = span.LineIndex;
                }

                lastLineIndex--;
            }

            return spans;
        }

        public static IEnumerable<TextLineSpan> ParseHighlightSpans(string highlight)
        {
            highlight = highlight.Replace(HighlightStartTag, HighlightStartTagCharString);
            highlight = highlight.Replace(HighlightEndTag, HighlightEndTagCharString);
            highlight += "\n";

            List<TextLineSpan> spans = new List<TextLineSpan>(1);
            StringBuilder builder = new StringBuilder();
            SymbolSpan currentSpan = new SymbolSpan();

            for (int i = 0; i < highlight.Length; i++)
            {
                var ch = highlight[i];
                switch (ch)
                {
                    case StartOfLineSpecifierChar:
                        var endOfLineSpecifierIndex = highlight.IndexOf(EndOfLineSpecifierChar, i + 1);
                        if (endOfLineSpecifierIndex >= 0)
                        {
                            int lineNumber = 0;
                            var lineNumberString = highlight.Substring(i + 1, endOfLineSpecifierIndex - (i + 1));
                            if (int.TryParse(lineNumberString, out lineNumber))
                            {
                                currentSpan.LineIndex = lineNumber;
                            }

                            i = endOfLineSpecifierIndex;
                        }
                        else
                        {
                            i = highlight.Length;
                        }

                        continue;
                    case HighlightStartTagChar:
                        if (currentSpan.Length == 0)
                        {
                            currentSpan.LineSpanStart = builder.Length;
                        }

                        break;
                    case HighlightEndTagChar:
                        currentSpan.Length = (builder.Length - currentSpan.LineSpanStart);
                        break;
                    case EndOfLineSpecifierChar:
                        // This is only encountered if this character appears before
                        // a start of line specifier character. Truncate in that case.
                        builder.Clear();
                        break;
                    case '\r':
                        // Just skip carriage return.
                        break;
                    case '\n':
                        if (spans.Count != 0)
                        {
                            var priorSpan = spans[spans.Count - 1];
                            if (currentSpan.LineIndex != 0)
                            {
                                priorSpan.LineIndex = currentSpan.LineIndex - 1;
                            }
                            else
                            {
                                currentSpan.LineIndex = priorSpan.LineIndex + 1;
                            }
                        }

                        currentSpan.LineSpanText = builder.ToString();
                        currentSpan.LineSpanText = currentSpan.LineSpanText.Trim();
                        spans.Add(currentSpan);
                        currentSpan = new SymbolSpan();
                        builder.Clear();
                        break;
                    default:
                        if (char.IsWhiteSpace(ch) && builder.Length == 0)
                        {
                            currentSpan.LineOffset++;

                            // Skip leading whitespace
                            continue;
                        }

                        builder.Append(ch);
                        break;
                }
            }

            return spans.Where(s => s.Length != 0);
        }

        public static TextLineSpan ParseHighlightSpan(string highlight)
        {
            return ParseHighlightSpan(highlight, new StringBuilder());
        }

        private static TextLineSpan ParseHighlightSpan(string highlight, StringBuilder builder)
        {
            builder.Clear();
            int lineIndex = -1;
            int lineStart = highlight.IndexOf(HighlightStartTag);

            for (int i = 0; i < highlight.Length; i++)
            {
                if (highlight[i] == StartOfLineSpecifierChar)
                {
                    int lineSpecifierStartIndex = i + 1;
                    int lineSpecifierLength = 0;
                    for (i = i + 1; i < highlight.Length; i++, lineSpecifierLength++)
                    {
                        if (highlight[i] == EndOfLineSpecifierChar)
                        {
                            if (lineIndex == -1 || i < lineStart)
                            {
                                if (!int.TryParse(highlight.Substring(lineSpecifierStartIndex, lineSpecifierLength), out lineIndex))
                                {
                                    lineIndex = -1;
                                }
                            }
                            break;
                        }
                    }
                }
                else
                {
                    builder.Append(highlight[i]);
                }
            }

            highlight = builder.ToString();
            lineStart = highlight.IndexOf(HighlightStartTag);

            int lastNewLineBeforeStartTag = highlight.LastIndexOfAny(NewLineChars, lineStart) + 1;
            lineStart -= lastNewLineBeforeStartTag;
            highlight = highlight.Substring(lastNewLineBeforeStartTag);

            int length = highlight.LastIndexOf(HighlightEndTag) - lineStart;
            var lineSpanText = highlight.Replace(HighlightStartTag, string.Empty).Replace(HighlightEndTag, string.Empty);
            length -= ((highlight.Length - lineSpanText.Length) - HighlightEndTag.Length);

            var firstNewLineAfterEndTag = lineSpanText.IndexOfAny(NewLineChars, lineStart + length);
            if (firstNewLineAfterEndTag > 0)
            {
                lineSpanText = lineSpanText.Substring(0, firstNewLineAfterEndTag);
            }

            return new TextLineSpan()
            {
                LineIndex = lineIndex >= 0 ? lineIndex : 0,
                LineSpanText = lineSpanText,
                LineSpanStart = lineStart,
                Length = length,
            };
        }

        public static SourceFile EnableFullTextSearch(this SourceFile sourceFile)
        {
            sourceFile.Content = EncodeFullTextString(sourceFile.Content);
            return sourceFile;
        }

        public static void DecodeFullText(string str, StringBuilder stringBuilder)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == StartOfLineSpecifierChar)
                {
                    for (i = i + 1; i < str.Length; i++)
                    {
                        if (str[i] == EndOfLineSpecifierChar)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    stringBuilder.Append(str[i]);
                }
            }
        }

        public static string DecodeFullTextString(string str, StringBuilder stringBuilder = null)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            stringBuilder = stringBuilder ?? new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == StartOfLineSpecifierChar)
                {
                    for (i = i + 1; i < str.Length; i++)
                    {
                        if (str[i] == EndOfLineSpecifierChar)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    stringBuilder.Append(str[i]);
                }
            }

            return stringBuilder.ToString();
        }

        public static void DecodeFullText(List<string> contentLines)
        {
            if (contentLines == null)
            {
                return;
            }

            using (var lease = Pools.EncoderContextPool.Acquire())
            {
                EncoderContext context = lease.Instance;
                var sb = context.StringBuilder;
                for (int lineNumber = 0; lineNumber < contentLines.Count; lineNumber++)
                {
                    sb.Clear();
                    contentLines[lineNumber] = DecodeFullTextString(contentLines[lineNumber], sb);
                }
            }
        }

        public static void EncodeFullText(List<string> contentLines)
        {
            if (contentLines == null)
            {
                return;
            }

            using (var lease = Pools.EncoderContextPool.Acquire())
            {
                EncoderContext context = lease.Instance;
                var sb = context.StringBuilder;
                for (int lineNumber = 0; lineNumber < contentLines.Count; lineNumber++)
                {
                    sb.Clear();
                    var line = contentLines[lineNumber];
                    var remaining = line.Length;
                    while (remaining >= 0)
                    {
                        EncodeLineNumber(lineNumber, sb);
                        sb.Append(line, line.Length - remaining, Math.Min(remaining, MaxLineEncodingDistance));
                        remaining -= MaxLineEncodingDistance;
                    }

                    contentLines[lineNumber] = sb.ToString();
                }
            }
        }

        public static string EncodeFullTextString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            int lineNumber = 0;
            int lineEncodingDistance = 0;
            var stringBuilder = new StringBuilder();
            EncodeAndIncrementLineNumber(ref lineNumber, stringBuilder);
            for (int i = 0; i < str.Length; i++, lineEncodingDistance++)
            {
                stringBuilder.Append(str[i]);
                if (str[i] == '\n')
                {
                    EncodeAndIncrementLineNumber(ref lineNumber, stringBuilder);
                    lineEncodingDistance = 0;
                }
                else if (lineEncodingDistance > MaxLineEncodingDistance && char.IsWhiteSpace(str[i]))
                {
                    EncodeLineNumber(lineNumber - 1, stringBuilder);
                    lineEncodingDistance = 0;
                }
            }

            return stringBuilder.ToString();
        }

        private static void EncodeAndIncrementLineNumber(ref int lineNumber, StringBuilder stringBuilder)
        {
            EncodeLineNumber(lineNumber, stringBuilder);
            lineNumber++;
        }

        private static void EncodeLineNumber(int lineNumber, StringBuilder stringBuilder)
        {
            stringBuilder.Append(StartOfLineSpecifierChar);
            stringBuilder.Append(lineNumber);
            stringBuilder.Append(EndOfLineSpecifierChar);
        }
    }
}