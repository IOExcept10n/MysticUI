// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace Icy.Data
{
    /// <summary>
    /// Represents an extension class over the <see cref="string"/> and <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Gets the default set of bracket pairs used to tokenize strings.
        /// </summary>
        public static ReadOnlySpan<(char Open, char Close)> DefaultBracketPairs => new[]
        {
            ('(', ')'),
            ('[', ']'),
            ('{', '}'),
            ('<', '>'),
            ('\"', '\"'),
            ('\'', '\''),
        };

        /// <summary>
        /// Enumerates all Unicode character codepoints inside given string.
        /// </summary>
        /// <param name="str">A string to enumerate.</param>
        /// <returns>An enumeration of all codepoints in given string.</returns>
        public static IEnumerable<int> EnumerateCodepoints(this string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char currentChar = str[i];

                if (char.IsHighSurrogate(currentChar) && i + 1 < str.Length)
                {
                    char lowSurrogate = str[i + 1];
                    if (char.IsLowSurrogate(lowSurrogate))
                    {
                        int codePoint = char.ConvertToUtf32(currentChar, lowSurrogate);
                        yield return codePoint;
                        i++;
                    }
                }
                else
                {
                    yield return currentChar;
                }
            }
        }

        /// <summary>
        /// Enumerates all Unicode character codepoints inside given readonly characters span.
        /// </summary>
        /// <remarks>
        /// Returned value is prepared for use especially inside <see langword="foreach"/> loops.
        /// </remarks>
        /// <param name="str">A <see cref="ReadOnlySpan{T}"/> to enumerate.</param>
        /// <returns>An enumeration of all codepoints in given string.</returns>
        public static ReadOnlySpanCodepointsEnumerable EnumerateCodepoints(this ReadOnlySpan<char> str) => new(str);

        /// <summary>
        /// Tokenizes a string into tokens considering specified separators and brackets.
        /// </summary>
        /// <param name="str">The string to be tokenized.</param>
        /// <param name="separator">The character used as a separator for tokenization.</param>
        /// <param name="brackets">The pairs of brackets to be considered during tokenization.</param>
        /// <returns>An enumeration of tokens that are tokenized considering the brackets.</returns>
        public static ReadOnlySpanTokensEnumerable TokenizeWithBrackets(this ReadOnlySpan<char> str, char separator, ReadOnlySpan<(char Open, char Close)> brackets) => new(str, separator, brackets);

        /// <summary>
        /// Tokenizes a string into tokens considering a specified separator and default bracket pairs.
        /// </summary>
        /// <param name="str">The string to be tokenized.</param>
        /// <param name="separator">The character used as a separator for tokenization.</param>
        /// <returns>An enumeration of tokens that are tokenized considering the default bracket pairs.</returns>
        public static ReadOnlySpanTokensEnumerable TokenizeWithBrackets(this ReadOnlySpan<char> str, char separator) => str.TokenizeWithBrackets(separator, DefaultBracketPairs);

        /// <summary>
        /// A <see langword="readonly"/> <see langword="ref"/> <see langword="struct"/> representing a dictionary of bracket pairs.
        /// </summary>
        public readonly ref struct BracketsPairDictionary
        {
            private readonly ReadOnlySpan<(char Open, char Close)> pairs;

            /// <summary>
            /// Initializes a new instance of the <see cref="BracketsPairDictionary"/> struct with the specified bracket pairs.
            /// </summary>
            /// <param name="pairs">The pairs of brackets to be stored in the dictionary.</param>
            public BracketsPairDictionary(ReadOnlySpan<(char Open, char Close)> pairs)
            {
                this.pairs = pairs;
            }

            /// <summary>
            /// Explicitly converts a <see cref="BracketsPairDictionary"/> to a <see cref="ReadOnlySpan{T}"/>.
            /// </summary>
            /// <param name="dict">The bracket pair dictionary.</param>
            /// <returns>A read-only span of character pairs.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static explicit operator ReadOnlySpan<(char Open, char Close)>(BracketsPairDictionary dict) => dict.pairs;

            /// <summary>
            /// Implicitly converts a <see cref="ReadOnlySpan{T}"/> to a <see cref="BracketsPairDictionary"/>.
            /// </summary>
            /// <param name="pairs">A read-only span of character pairs.</param>
            /// <returns>A new instance of <see cref="BracketsPairDictionary"/>.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator BracketsPairDictionary(ReadOnlySpan<(char Open, char Close)> pairs) => new(pairs);

            /// <summary>
            /// Tries to get the closing bracket for the specified opening bracket.
            /// </summary>
            /// <param name="open">The opening bracket.</param>
            /// <param name="close">The closing bracket, if found.</param>
            /// <returns><c>true</c> if the closing bracket is found; otherwise, <c>false</c>.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetClose(char open, out char close)
            {
                foreach (var (o, c) in pairs)
                {
                    if (o == open)
                    {
                        close = c;
                        return true;
                    }
                }

                close = default;
                return false;
            }

            /// <summary>
            /// Tries to get the opening bracket for the specified closing bracket.
            /// </summary>
            /// <param name="close">The closing bracket.</param>
            /// <param name="open">The opening bracket, if found.</param>
            /// <returns><see langword="true"/> if the opening bracket is found; otherwise, <see langword="false"/>.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetOpen(char close, out char open)
            {
                foreach (var (o, c) in pairs)
                {
                    if (c == close)
                    {
                        open = o;
                        return true;
                    }
                }

                open = default;
                return false;
            }
        }

        /// <summary>
        /// Represents a struct that iterates over specified char span and provides Unicode codepoints for each character in it.
        /// </summary>
        /// <remarks>
        /// This class is not intended for use manually. Its common usage is inside <see langword="foreach"/> loops.
        /// </remarks>
        public ref struct ReadOnlySpanCodepointsEnumerable
        {
            private readonly ReadOnlySpan<char> text;
            private int current;
            private int position;

            /// <summary>
            /// Initializes a new instance of the <see cref="ReadOnlySpanCodepointsEnumerable"/> struct.
            /// </summary>
            /// <param name="text">Characters readonly span to iterate over.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpanCodepointsEnumerable(ReadOnlySpan<char> text)
            {
                current = -1;
                this.text = text;
                position = 0;
            }

            /// <summary>
            /// Gets the current codepoint.
            /// </summary>
            public readonly int Current => current;

            /// <summary>
            /// Gets an enumerator over the span.
            /// </summary>
            /// <returns>This instance of the <see cref="ReadOnlySpanCodepointsEnumerable"/> struct.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpanCodepointsEnumerable GetEnumerator() => this;

            /// <summary>
            /// Moves to the next codepoint.
            /// </summary>
            /// <returns><see langword="true"/> if the next codepoint exists; otherwise <see langword="false"/>.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (position >= text.Length)
                    return false;

                char currentChar = text[position];

                if (char.IsHighSurrogate(currentChar) && position + 1 < text.Length)
                {
                    char lowSurrogate = text[position + 1];
                    if (char.IsLowSurrogate(lowSurrogate))
                    {
                        current = char.ConvertToUtf32(currentChar, lowSurrogate);
                        position++;
                    }
                }
                else
                {
                    current = currentChar;
                }

                position++;
                return true;
            }

            /// <summary>
            /// Resets the enumerator position to zero.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                position = 0;
                current = text[0];
            }
        }

        /// <summary>
        /// A <see langword="ref"/> <see langword="struct"/> that tokenizes a given <see cref="ReadOnlySpan{T}"/> instance with handling brackets as solid tokens.
        /// </summary>
        public ref struct ReadOnlySpanTokensEnumerable
        {
            private readonly BracketsPairDictionary bracketPairs;
            private readonly char separator;
            private readonly ReadOnlySpan<char> span;
            private int end;
            private int start;

            /// <summary>
            /// Initializes a new instance of the <see cref="ReadOnlySpanTokensEnumerable"/> struct.
            /// </summary>
            /// <param name="span">The source <see cref="ReadOnlySpan{T}"/> instance.</param>
            /// <param name="separator">The separator item to use.</param>
            /// <param name="bracketPairs">List of bracket pairs to handle brackets as separate tokens.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpanTokensEnumerable(ReadOnlySpan<char> span, char separator, BracketsPairDictionary bracketPairs)
            {
                this.span = span;
                this.separator = separator;
                this.bracketPairs = bracketPairs;
                start = 0;
                end = -1;
            }

            /// <summary>
            /// Gets the duck-typed <see cref="IEnumerator{T}.Current"/> property.
            /// </summary>
            public readonly ReadOnlySpan<char> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => span[start..end];
            }

            /// <summary>
            /// Implements the duck-typed <see cref="IEnumerable{T}.GetEnumerator"/> method.
            /// </summary>
            /// <returns>An <see cref="ReadOnlySpanTokensEnumerable"/> instance targeting the current <see cref="ReadOnlySpan{T}"/> value.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpanTokensEnumerable GetEnumerator() => this;

            /// <summary>
            /// Implements the duck-typed <see cref="System.Collections.IEnumerator.MoveNext"/> method.
            /// </summary>
            /// <returns><see langword="true"/> whether a new element is available, <see langword="false"/> otherwise.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int newEnd = end + 1;
                int length = span.Length;

                // Additional check if the separator is not the last character
                if (newEnd <= length)
                {
                    start = newEnd;

                    int index = -1;
                    for (int i = newEnd; i < span.Length; i++)
                    {
                        if (span[i] == separator)
                        {
                            index = i;
                            break;
                        }

                        if (bracketPairs.TryGetClose(span[i], out char close))
                        {
                            int until = span[(i + 1)..].IndexOf(close);
                            if (until >= 0)
                            {
                                // Add to 'until' i+1 because we shift span when finding index.
                                i += until + 1;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    // Extract the current subsequence
                    if (index >= 0)
                    {
                        end = index;

                        return true;
                    }

                    end = length;

                    return true;
                }

                return false;
            }
        }
    }
}