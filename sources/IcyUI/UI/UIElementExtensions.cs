// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Markup;
using Icy.UI.Styles;

namespace Icy.UI
{
    /// <summary>
    /// Convenience operations over an existing <see cref="UIElement"/> tree - walking it, searching it, and looking
    /// elements up by name.
    /// </summary>
    public static class UIElementExtensions
    {
        /// <summary>
        /// Removes the <see cref="UIElement"/> instance from its parent. If the element is root, removes it from the <see cref="Canvas"/> instance.
        /// </summary>
        /// <param name="element">The element to detach.</param>
        public static void Detach(this UIElement element)
        {
            // if (element.Parent is IContainerControl container)
            //     container.Remove(element);
            // else
            //     element.Canvas?.Remove(element);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Applies a style to the UI element.
        /// </summary>
        /// <typeparam name="T">The type of the element the style is applied to.</typeparam>
        /// <param name="element">The element to style.</param>
        /// <param name="style">Style instance to apply.</param>
        /// <returns>The same element, for chaining.</returns>
        public static T WithStyle<T>(this T element, Style style)
            where T : UIElement
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Enumerates <paramref name="element"/> and every element beneath it, depth-first.
        /// </summary>
        /// <param name="element">The element to start from.</param>
        /// <returns><paramref name="element"/> first, then its whole subtree.</returns>
        /// <remarks>
        /// Walks the <em>visual</em> tree, so it also sees elements a control builds for itself - a
        /// <see cref="Controls.Control"/>'s chrome <see cref="Border"/>, for instance - not only the children an
        /// author added.
        /// </remarks>
        public static IEnumerable<UIElement> EnumerateSubtree(this UIElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            yield return element;
            foreach (UIElement descendant in element.EnumerateVisualSubtree())
            {
                yield return descendant;
            }
        }

        /// <summary>
        /// Finds every element beneath <paramref name="control"/> matching <paramref name="predicate"/>.
        /// </summary>
        /// <param name="control">The element to search under.</param>
        /// <param name="predicate">The condition an element must satisfy to be returned.</param>
        /// <returns>The matching descendants, in depth-first order. <paramref name="control"/> itself is not tested.</returns>
        public static IEnumerable<UIElement> FindChildren(this UIElement control, Func<UIElement, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(predicate);

            return control.EnumerateVisualSubtree().Where(predicate);
        }

        /// <summary>
        /// Finds the element named <paramref name="name"/> in <paramref name="element"/>'s tree.
        /// </summary>
        /// <typeparam name="T">The type the named element is expected to have.</typeparam>
        /// <param name="element">Any element in the tree to search - the search starts from its root.</param>
        /// <param name="name">The name to look up, as declared by <c>x:Name</c> or assigned to <see cref="UIElement.Name"/>.</param>
        /// <returns>The named element, or <see langword="null"/> when the tree has none, or has one of another type.</returns>
        /// <remarks>
        /// <para>
        /// A tree loaded from markup carries a <see cref="MarkupNameScope"/> on its root, and this is a dictionary
        /// lookup in it. A hand-built tree has no scope, so the whole subtree is walked and matched on
        /// <see cref="UIElement.Name"/> instead - the same call works either way, just at different cost.
        /// </para>
        /// <para>
        /// The search starts at the root of <paramref name="element"/>'s tree, not at
        /// <paramref name="element"/> itself, so any element can look up any other in the same document. An element
        /// that is not yet attached to anything is its own root.
        /// </para>
        /// </remarks>
        public static T? FindControl<T>(this UIElement element, string name)
            where T : UIElement
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentException.ThrowIfNullOrEmpty(name);

            UIElement root = element.GetRoot();
            UIElement? found = MarkupNameScope.GetScope(root) is { } scope
                ? scope.Find(name)
                : root.EnumerateSubtree().FirstOrDefault(x => x.Name == name);

            return found as T;
        }

        /// <summary>
        /// Finds the element named <paramref name="name"/> in <paramref name="element"/>'s tree, and throws when it
        /// isn't there.
        /// </summary>
        /// <typeparam name="T">The type the named element is expected to have.</typeparam>
        /// <param name="element">Any element in the tree to search - the search starts from its root.</param>
        /// <param name="name">The name to look up.</param>
        /// <returns>The named element.</returns>
        /// <exception cref="InvalidOperationException">
        /// No element in the tree is named <paramref name="name"/>, or the one that is isn't a
        /// <typeparamref name="T"/>.
        /// </exception>
        /// <remarks>
        /// Use this over <see cref="FindControl{T}(UIElement, string)"/> wherever the element is required for the
        /// code to work at all - a missing name then fails at the lookup, naming what was missing, instead of
        /// surfacing as a <see langword="null"/> somewhere later.
        /// </remarks>
        public static T FindRequiredControl<T>(this UIElement element, string name)
            where T : UIElement
        {
            return element.FindControl<T>(name)
                ?? throw new InvalidOperationException($"No element named '{name}' of type '{typeof(T).Name}' exists in this tree.");
        }

        /// <summary>
        /// Walks up to the topmost element above <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element to start from.</param>
        /// <returns>The root of <paramref name="element"/>'s tree, or <paramref name="element"/> when it is the root.</returns>
        /// <remarks>
        /// Stops at the topmost <see cref="UIElement"/>, which is the element added to a <see cref="Canvas"/> - the
        /// canvas itself is not a <see cref="UIElement"/> and is not part of the walk.
        /// </remarks>
        public static UIElement GetRoot(this UIElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            UIElement root = element;
            while (root.Parent is { } parent)
            {
                root = parent;
            }

            return root;
        }
    }
}
