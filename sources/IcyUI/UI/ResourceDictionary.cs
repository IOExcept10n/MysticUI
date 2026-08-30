using System.Collections;

namespace Icy.UI
{
    /// <summary>
    /// A string-keyed collection of named resources (<see cref="Styles.Style"/>s, <see cref="Animations.Timeline"/>s,
    /// brushes, or any other value), optionally composed from other dictionaries via <see cref="MergedDictionaries"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <see cref="UIElement"/> carries its own via <see cref="UIElement.Resources"/> - lookup
    /// (<c>{StaticResource}</c>, see <see cref="Markup.Extensions.StaticResourceExtension"/>) resolves once, at
    /// load time, by walking outward from the innermost element currently under construction (the loader's
    /// construction-time element stack, not <see cref="UIElement.Parent"/>/<see cref="UIElement.LogicalParent"/> -
    /// see <see cref="Markup.Extensions.StaticResourceExtension"/>'s own remarks for why) to the document root,
    /// checking each element's own <see cref="UIElement.Resources"/> in turn before moving outward.
    /// </para>
    /// <para>
    /// Within a single dictionary, a direct entry always shadows a same-key entry from
    /// <see cref="MergedDictionaries"/>; among merged dictionaries themselves, a later one shadows an earlier one
    /// on collision - both are intentional (theme layering), not errors.
    /// </para>
    /// </remarks>
    public class ResourceDictionary : IDictionary<string, object?>
    {
        /// <summary>
        /// The reserved key prefix an implicit (keyless, type-targeted) <see cref="Styles.Style"/> registers under.
        /// </summary>
        private const string ImplicitStyleKeyPrefix = "#implicit-style:";

        private readonly Dictionary<string, object?> entries = [];

        /// <summary>
        /// Gets the other dictionaries this one is composed from, checked in order (a later one shadows an
        /// earlier one) after this dictionary's own direct entries.
        /// </summary>
        /// <remarks>
        /// Only recognized in markup as a property element on a document's own root - a standalone
        /// <c>&lt;ResourceDictionary&gt;</c> document loaded via <see cref="Markup.MarkupLoader.LoadObject(string, string?)"/> can
        /// have a nested <c>&lt;ResourceDictionary.MergedDictionaries&gt;</c>, but nesting it under another
        /// element's own dictionary-valued property (e.g. inside <c>&lt;Panel.Resources&gt;</c>) does not work today:
        /// every child there is routed straight into keyed-entry population with no check for a nested property
        /// element first. To merge into a <see cref="UIElement.Resources"/> dictionary, add the loaded dictionary
        /// directly in code instead: <c>element.Resources.MergedDictionaries.Add(loadedDictionary)</c> (there is no
        /// setter for <see cref="UIElement.Resources"/> itself - only <see cref="MergedDictionaries"/>'s own
        /// <c>Add</c> is available).
        /// </remarks>
        public IList<ResourceDictionary> MergedDictionaries { get; } = [];

        /// <summary>
        /// Gets the reserved resource key an implicit <see cref="Styles.Style"/> targeting <paramref name="targetType"/>
        /// registers under.
        /// </summary>
        /// <param name="targetType">The exact type the implicit style targets.</param>
        /// <returns>The reserved key.</returns>
        public static string GetImplicitStyleKey(Type targetType) => ImplicitStyleKeyPrefix + targetType.FullName;

        /// <inheritdoc/>
        public object? this[string key]
        {
            get => entries[key];
            set => entries[key] = value;
        }

        /// <inheritdoc/>
        public ICollection<string> Keys => entries.Keys;

        /// <inheritdoc/>
        public ICollection<object?> Values => entries.Values;

        /// <inheritdoc/>
        public int Count => entries.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Looks up <paramref name="key"/> in this dictionary's own entries, then in <see cref="MergedDictionaries"/>
        /// (in reverse order, so a later merged dictionary wins).
        /// </summary>
        /// <param name="key">The resource key to look up.</param>
        /// <param name="value">The resolved value, when this returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when <paramref name="key"/> is found, directly or through a merge.</returns>
        public bool TryGetValue(string key, out object? value)
        {
            if (entries.TryGetValue(key, out value))
                return true;

            for (int i = MergedDictionaries.Count - 1; i >= 0; i--)
            {
                if (MergedDictionaries[i].TryGetValue(key, out value))
                    return true;
            }

            value = null;
            return false;
        }

        /// <inheritdoc/>
        public void Add(string key, object? value) => entries.Add(key, value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<string, object?> item) => Add(item.Key, item.Value);

        /// <inheritdoc/>
        public void Clear() => entries.Clear();

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)entries).Contains(item);

        /// <inheritdoc/>
        public bool ContainsKey(string key) => entries.ContainsKey(key);

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, object?>>)entries).CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => entries.GetEnumerator();

        /// <inheritdoc/>
        public bool Remove(string key) => entries.Remove(key);

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)entries).Remove(item);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
