// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// Represents a contiguous range of characters in a markup text.
    /// </summary>
    public readonly record struct TextSpan
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextSpan"/> struct.
        /// </summary>
        /// <param name="start">The offset of the first character in the span.</param>
        /// <param name="length">The number of characters in the span.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> or <paramref name="length"/> is negative.</exception>
        public TextSpan(int start, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            Start = start;
            Length = length;
        }

        /// <summary>
        /// Gets the offset of the first character in the span.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// Gets the number of characters in the span.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the offset just past the last character in the span.
        /// </summary>
        public int End => Start + Length;

        /// <summary>
        /// Creates a span from its start and end offsets.
        /// </summary>
        /// <param name="start">The offset of the first character in the span.</param>
        /// <param name="end">The offset just past the last character in the span.</param>
        /// <returns>The span covering <c>[start, end)</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="end"/> is less than <paramref name="start"/>.</exception>
        public static TextSpan FromBounds(int start, int end) => new(start, end - start);

        /// <inheritdoc/>
        public override string ToString() => $"[{Start}..{End})";
    }
}
