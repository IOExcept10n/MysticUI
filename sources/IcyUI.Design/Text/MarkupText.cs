// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// An immutable snapshot of a markup document's text, with a version that grows by one with every change.
    /// </summary>
    public sealed class MarkupText
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupText"/> class.
        /// </summary>
        /// <param name="text">The markup text.</param>
        /// <param name="version">The snapshot's version.</param>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
        public MarkupText(string text, int version = 0)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfNegative(version);
            Text = text;
            Version = version;
        }

        /// <summary>
        /// Gets the markup text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the snapshot's version.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Applies <paramref name="changes"/> and returns the next snapshot.
        /// </summary>
        /// <param name="changes">The changes, expressed against <see cref="Text"/>.</param>
        /// <returns>A snapshot with the changed text and the next version.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="changes"/> is <see langword="null"/>.</exception>
        public MarkupText Apply(TextChangeSet changes)
        {
            ArgumentNullException.ThrowIfNull(changes);
            return new MarkupText(changes.Apply(Text), Version + 1);
        }

        /// <inheritdoc/>
        public override string ToString() => Text;
    }
}
