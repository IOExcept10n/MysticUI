// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;

namespace Icy.Markup
{
    /// <summary>
    /// The exception raised for every markup error - a malformed document, an unresolvable type or property, a bad
    /// value, or a structural rule the document broke.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Loading fails on the <em>first</em> error and never returns a partially-constructed tree, so a caught
    /// <see cref="MarkupException"/> always means nothing was produced.
    /// </para>
    /// <para>
    /// <see cref="Exception.Message"/> is prefixed with the source position in the standard compiler format
    /// (<c>path(line,column): message</c>) whenever the loader knows it, which it does for anything anchored to a
    /// node, since documents are parsed with <see cref="System.Xml.Linq.LoadOptions.SetLineInfo"/>.
    /// </para>
    /// </remarks>
    public class MarkupException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupException"/> class.
        /// </summary>
        /// <param name="message">A description of what went wrong.</param>
        /// <param name="sourcePath">The path of the markup file, or <see langword="null"/> when it isn't known.</param>
        /// <param name="line">The 1-based line the error occurred on, or <c>0</c> when it isn't known.</param>
        /// <param name="column">The 1-based column the error occurred on, or <c>0</c> when it isn't known.</param>
        /// <param name="innerException">The underlying exception, when this error wraps one.</param>
        public MarkupException(string message, string? sourcePath = null, int line = 0, int column = 0, Exception? innerException = null)
            : base(FormatMessage(message, sourcePath, line, column), innerException)
        {
            Description = message;
            SourcePath = sourcePath;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the error description without the source-position prefix that <see cref="Exception.Message"/> carries.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the path of the markup file the error occurred in, or <see langword="null"/> when it isn't known.
        /// </summary>
        public string? SourcePath { get; }

        /// <summary>
        /// Gets the 1-based line the error occurred on, or <c>0</c> when it isn't known.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the 1-based column the error occurred on, or <c>0</c> when it isn't known.
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Creates an exception anchored at the position of an XML node.
        /// </summary>
        /// <param name="message">A description of what went wrong.</param>
        /// <param name="node">The node the error is about; its line info is used when it has any.</param>
        /// <param name="sourcePath">The path of the markup file, or <see langword="null"/> when it isn't known.</param>
        /// <param name="innerException">The underlying exception, when this error wraps one.</param>
        /// <returns>An exception carrying <paramref name="node"/>'s position.</returns>
        public static MarkupException At(string message, IXmlLineInfo? node, string? sourcePath, Exception? innerException = null)
        {
            bool hasPosition = node?.HasLineInfo() == true;
            return new MarkupException(
                message,
                sourcePath,
                hasPosition ? node!.LineNumber : 0,
                hasPosition ? node!.LinePosition : 0,
                innerException);
        }

        private static string FormatMessage(string message, string? sourcePath, int line, int column)
        {
            if (sourcePath == null && line == 0)
                return message;

            string position = line == 0 ? string.Empty : $"({line},{column})";
            return $"{sourcePath ?? "<markup>"}{position}: {message}";
        }
    }
}
