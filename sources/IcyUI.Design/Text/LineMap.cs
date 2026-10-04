// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// Converts the 1-based (line, column) positions <see cref="System.Xml.IXmlLineInfo"/> reports into offsets.
    /// </summary>
    /// <remarks>
    /// XML treats <c>\r\n</c>, <c>\r</c> and <c>\n</c> each as one line break, and counts a column per UTF-16
    /// character (a tab is one column), so this does the same.
    /// </remarks>
    internal sealed class LineMap
    {
        private readonly int[] lineStarts;

        /// <summary>
        /// Initializes a new instance of the <see cref="LineMap"/> class.
        /// </summary>
        /// <param name="text">The text whose line starts to index.</param>
        public LineMap(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    starts.Add(i + 1);
                }
                else if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            lineStarts = [.. starts];
        }

        /// <summary>
        /// Gets the number of lines in the text.
        /// </summary>
        public int LineCount => lineStarts.Length;

        /// <summary>
        /// Converts a 1-based (line, column) position to an offset.
        /// </summary>
        /// <param name="line">The 1-based line.</param>
        /// <param name="column">The 1-based column.</param>
        /// <returns>The offset.</returns>
        public int ToOffset(int line, int column) =>
            TryToOffset(line, column, out int offset)
                ? offset
                : throw new ArgumentOutOfRangeException(nameof(line), $"Line {line}, column {column} is outside the text.");

        /// <summary>
        /// Converts a 1-based (line, column) position to an offset, when the line exists.
        /// </summary>
        /// <param name="line">The 1-based line.</param>
        /// <param name="column">The 1-based column.</param>
        /// <param name="offset">The offset.</param>
        /// <returns><see langword="true"/> when the position is inside the text's lines.</returns>
        public bool TryToOffset(int line, int column, out int offset)
        {
            offset = 0;
            if (line < 1 || line > lineStarts.Length || column < 1)
                return false;

            offset = lineStarts[line - 1] + column - 1;
            return true;
        }
    }
}
