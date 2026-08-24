// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using Icy.Data.Markup;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// The set of <c>x:Name</c>d elements produced by one markup document, keyed by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One scope covers one document, so names only have to be unique within the file that declares them - two
    /// pages may each have an <c>ok</c> button. The scope is attached to the tree's root and reached from any
    /// element in it via <see cref="UIElementExtensions.FindControl{T}(UIElement, string)"/>.
    /// </para>
    /// <para>
    /// A scope is a lookup table, not an ownership relation: it holds ordinary references to elements that the tree
    /// owns, and lives exactly as long as the root does.
    /// </para>
    /// </remarks>
    public sealed class MarkupNameScope
    {
        /// <summary>
        /// The attached-property key the scope is stored under on its root element.
        /// </summary>
        private const string ScopeKey = "Markup.NameScope";

        private readonly Dictionary<string, UIElement> names = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the named elements in this scope.
        /// </summary>
        public IReadOnlyDictionary<string, UIElement> Names => names;

        /// <summary>
        /// Gets the name scope attached to <paramref name="root"/>, if it has one.
        /// </summary>
        /// <param name="root">The element to read the scope from.</param>
        /// <returns>The attached scope, or <see langword="null"/> when the element isn't a markup root.</returns>
        public static MarkupNameScope? GetScope(UIElement root)
        {
            ArgumentNullException.ThrowIfNull(root);
            return AttachedProperties.GetValue<MarkupNameScope?>(root, ScopeKey, null);
        }

        /// <summary>
        /// Attaches <paramref name="scope"/> to <paramref name="root"/>.
        /// </summary>
        /// <param name="root">The root element of the tree the scope covers.</param>
        /// <param name="scope">The scope to attach.</param>
        public static void SetScope(UIElement root, MarkupNameScope scope)
        {
            ArgumentNullException.ThrowIfNull(root);
            ArgumentNullException.ThrowIfNull(scope);
            AttachedProperties.SetValue<MarkupNameScope?>(root, ScopeKey, scope);
        }

        /// <summary>
        /// Registers <paramref name="element"/> under <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name declared by <c>x:Name</c>.</param>
        /// <param name="element">The element to register.</param>
        /// <exception cref="MarkupException">A different element is already registered under this name.</exception>
        public void Register(string name, UIElement element)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(element);

            if (names.TryGetValue(name, out UIElement? existing) && !ReferenceEquals(existing, element))
                throw new MarkupException($"Duplicate x:Name '{name}' - names must be unique within a markup file.");

            names[name] = element;
        }

        /// <summary>
        /// Finds the element registered under <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name to look up.</param>
        /// <returns>The element, or <see langword="null"/> when nothing is registered under that name.</returns>
        public UIElement? Find(string name) => names.GetValueOrDefault(name);

        /// <summary>
        /// Attempts to find the element registered under <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name to look up.</param>
        /// <param name="element">The element, when this returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when an element is registered under that name.</returns>
        public bool TryFind(string name, [NotNullWhen(true)] out UIElement? element) => names.TryGetValue(name, out element);
    }
}
