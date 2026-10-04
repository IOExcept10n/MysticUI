// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Text;

namespace Icy.Design.Text
{
    /// <summary>
    /// An ordered set of non-overlapping <see cref="TextChange"/>s, all expressed against the same original text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The changes are kept sorted by start offset. Two changes may touch, but they can't overlap, and at most one
    /// pure insertion may sit at any offset, so applying the set is unambiguous.
    /// </para>
    /// <para>
    /// A change set is the unit every edit of a design document produces, and the unit a future text editor
    /// receives to update its buffer without losing its caret or its own undo history.
    /// </para>
    /// </remarks>
    public sealed class TextChangeSet : IReadOnlyList<TextChange>
    {
        private readonly TextChange[] changes;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextChangeSet"/> class.
        /// </summary>
        /// <param name="changes">The changes, in any order.</param>
        /// <exception cref="ArgumentNullException"><paramref name="changes"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// A change has <see langword="null"/> new text, two changes overlap, or two insertions share an offset.
        /// </exception>
        public TextChangeSet(IEnumerable<TextChange> changes)
        {
            ArgumentNullException.ThrowIfNull(changes);

            TextChange[] sorted = [.. changes];
            Array.Sort(sorted, static (a, b) => a.Span.Start != b.Span.Start
                ? a.Span.Start.CompareTo(b.Span.Start)
                : a.Span.Length.CompareTo(b.Span.Length));

            for (int i = 0; i < sorted.Length; i++)
            {
                if (sorted[i].NewText == null)
                    throw new ArgumentException("A change's new text can't be null.", nameof(changes));

                if (i == 0)
                    continue;

                TextChange previous = sorted[i - 1];
                bool overlaps = previous.Span.End > sorted[i].Span.Start;
                bool sharedInsertion = previous.Span.Length == 0 && sorted[i].Span.Length == 0 && previous.Span.Start == sorted[i].Span.Start;
                if (overlaps || sharedInsertion)
                    throw new ArgumentException("Changes in a set must not overlap.", nameof(changes));
            }

            this.changes = sorted;
        }

        /// <summary>
        /// Gets a change set that changes nothing.
        /// </summary>
        public static TextChangeSet Empty { get; } = new([]);

        /// <inheritdoc/>
        public int Count => changes.Length;

        /// <inheritdoc/>
        public TextChange this[int index] => changes[index];

        /// <summary>
        /// Applies every change to <paramref name="text"/> in one pass.
        /// </summary>
        /// <param name="text">The original text the changes were expressed against.</param>
        /// <returns>The changed text.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">A change lies outside <paramref name="text"/>.</exception>
        public string Apply(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (changes.Length == 0)
                return text;

            if (changes[^1].Span.End > text.Length)
                throw new ArgumentException("A change lies outside the text.", nameof(text));

            int delta = 0;
            foreach (TextChange change in changes)
                delta += change.NewText.Length - change.Span.Length;

            var builder = new StringBuilder(text.Length + delta);
            int copied = 0;
            foreach (TextChange change in changes)
            {
                builder.Append(text, copied, change.Span.Start - copied);
                builder.Append(change.NewText);
                copied = change.Span.End;
            }

            builder.Append(text, copied, text.Length - copied);
            return builder.ToString();
        }

        /// <summary>
        /// Creates the change set that turns the changed text back into <paramref name="originalText"/>.
        /// </summary>
        /// <param name="originalText">The text this set was expressed against.</param>
        /// <returns>The inverse change set, expressed against the changed text.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="originalText"/> is <see langword="null"/>.</exception>
        public TextChangeSet Invert(string originalText)
        {
            ArgumentNullException.ThrowIfNull(originalText);

            var inverse = new TextChange[changes.Length];
            int delta = 0;
            for (int i = 0; i < changes.Length; i++)
            {
                TextChange change = changes[i];
                inverse[i] = new TextChange(
                    new TextSpan(change.Span.Start + delta, change.NewText.Length),
                    originalText.Substring(change.Span.Start, change.Span.Length));
                delta += change.NewText.Length - change.Span.Length;
            }

            return new TextChangeSet(inverse);
        }

        /// <summary>
        /// Maps an offset in the original text to the same character's offset in the changed text.
        /// </summary>
        /// <param name="position">An offset in the original text.</param>
        /// <returns>
        /// The offset in the changed text, or <see langword="null"/> when the character at
        /// <paramref name="position"/> was replaced or deleted. An insertion exactly at <paramref name="position"/>
        /// counts as coming before it, so the character shifts past the inserted text.
        /// </returns>
        public int? MapPosition(int position)
        {
            int delta = 0;
            foreach (TextChange change in changes)
            {
                if (change.Span.Length == 0)
                {
                    if (change.Span.Start > position)
                        break;

                    delta += change.NewText.Length;
                    continue;
                }

                if (change.Span.End <= position)
                {
                    delta += change.NewText.Length - change.Span.Length;
                    continue;
                }

                if (change.Span.Start <= position)
                    return null;

                break;
            }

            return position + delta;
        }

        /// <inheritdoc/>
        public IEnumerator<TextChange> GetEnumerator() => ((IEnumerable<TextChange>)changes).GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
