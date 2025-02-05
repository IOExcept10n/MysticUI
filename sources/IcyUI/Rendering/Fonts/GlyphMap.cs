// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a collection that stores info about font glyphs and maps them to their keys.
    /// </summary>
    /// <remarks>
    /// Note that for fonts with less than 256 characters map will use list for storage instead
    /// of dictionary so it will be efficient in terms of memory despite some lookup slowness.
    /// </remarks>
    public sealed class GlyphMap() : KeyedCollection<int, FontGlyph>(null, 256), IReadOnlyDictionary<int, FontGlyph>
    {
        /// <inheritdoc/>
        public IEnumerable<int> Keys => from item in Values select item.Codepoint;

        /// <inheritdoc/>
        public IEnumerable<FontGlyph> Values => Items;

        /// <inheritdoc/>
        public bool ContainsKey(int key) => Contains(key);

        /// <inheritdoc/>
        IEnumerator<KeyValuePair<int, FontGlyph>> IEnumerable<KeyValuePair<int, FontGlyph>>.GetEnumerator()
        {
            return (from item in Values
                    select new KeyValuePair<int, FontGlyph>(item.Codepoint, item)).GetEnumerator();
        }

        /// <inheritdoc/>
        protected override int GetKeyForItem(FontGlyph item) => item.Codepoint;
    }
}
