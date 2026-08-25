// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Parses the <c>{Name arg1, Key=Value, ...}</c> grammar a markup extension is written in.
    /// </summary>
    /// <remarks>
    /// Deliberately small: an extension name, then a comma-separated argument list where each argument is either
    /// bare (positional - see <see cref="MarkupExtensionDefaultPropertyAttribute"/>) or <c>Key=Value</c>. A value
    /// can be single-quoted to contain a comma or a closing brace, e.g. <c>{Binding Path='A, B'}</c>.
    /// </remarks>
    internal static class MarkupExtensionSyntax
    {
        /// <summary>
        /// Parses <paramref name="text"/>, which must already be known to start with <c>{</c> and end with <c>}</c>.
        /// </summary>
        /// <param name="text">The full <c>{...}</c> text, including the braces.</param>
        /// <param name="name">The extension name.</param>
        /// <param name="arguments">
        /// The parsed arguments, in the order they were written. A <see langword="null"/> key marks a positional
        /// argument.
        /// </param>
        /// <returns><see langword="true"/> when the text parses; <see langword="false"/> when it is malformed.</returns>
        public static bool TryParse(string text, out string name, out List<(string? Key, string Value)> arguments)
        {
            name = string.Empty;
            arguments = [];

            if (text.Length < 2 || text[0] != '{' || text[^1] != '}')
                return false;

            string inner = text[1..^1];
            int i = 0;
            SkipWhitespace(inner, ref i);

            int nameStart = i;
            while (i < inner.Length && !char.IsWhiteSpace(inner[i]))
                i++;
            name = inner[nameStart..i];
            if (name.Length == 0)
                return false;

            while (true)
            {
                SkipWhitespace(inner, ref i);
                if (i >= inner.Length)
                    return true;

                if (!TryReadArgument(inner, ref i, out (string? Key, string Value) argument))
                    return false;
                arguments.Add(argument);

                SkipWhitespace(inner, ref i);
                if (i >= inner.Length)
                    return true;

                if (inner[i] != ',')
                    return false;
                i++;
            }
        }

        private static bool TryReadArgument(string text, ref int i, out (string? Key, string Value) argument)
        {
            string token = ReadToken(text, ref i);
            int equals = FindTopLevelEquals(token);
            if (equals < 0)
            {
                argument = (null, Unquote(token.Trim()));
                return true;
            }

            string key = token[..equals].Trim();
            if (key.Length == 0)
            {
                argument = default;
                return false;
            }

            argument = (key, Unquote(token[(equals + 1)..].Trim()));
            return true;
        }

        /// <summary>
        /// Reads one comma-separated argument's raw text, honoring single-quoted spans and brace nesting so a
        /// quoted comma or a nested <c>{...}</c> doesn't end the argument early.
        /// </summary>
        private static string ReadToken(string text, ref int i)
        {
            int start = i;
            int depth = 0;
            bool quoted = false;

            while (i < text.Length)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '\'')
                        quoted = false;
                }
                else if (c == '\'')
                {
                    quoted = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}' && depth > 0)
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    break;
                }

                i++;
            }

            return text[start..i];
        }

        private static int FindTopLevelEquals(string token)
        {
            bool quoted = false;
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (c == '\'')
                    quoted = !quoted;
                else if (c == '=' && !quoted)
                    return i;
            }

            return -1;
        }

        private static string Unquote(string value)
        {
            return value.Length >= 2 && value[0] == '\'' && value[^1] == '\''
                ? value[1..^1]
                : value;
        }

        private static void SkipWhitespace(string text, ref int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
        }
    }
}
