// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.CompilerServices;

namespace Icy.Data
{
    /// <summary>
    /// Represents an extension class over the <see cref="string"/> and <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
    /// </summary>
    public static class StringExtensions
    {
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
    }
}
