// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Tracking;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Keeps a document's name scope in step with subtrees leaving or rejoining the page.
    /// </summary>
    internal static class LiveTree
    {
        /// <summary>
        /// Unregisters every name that belongs to <paramref name="root"/> or an element below it. Call it while the
        /// subtree is still attached, since it follows parent chains.
        /// </summary>
        /// <returns>The removed names, to register again when undoing.</returns>
        public static List<(string Name, UIElement Element)> UnregisterNames(MarkupLoadScope scope, UIElement root)
        {
            var removed = new List<(string, UIElement)>();
            if (scope.NameScope is not { } names)
                return removed;

            foreach ((string name, UIElement element) in names.Names.ToList())
            {
                if (ObjectMap.IsSelfOrDescendant(element, root))
                {
                    names.Unregister(name);
                    removed.Add((name, element));
                }
            }

            return removed;
        }

        public static void RegisterNames(MarkupLoadScope scope, IEnumerable<(string Name, UIElement Element)> entries)
        {
            if (scope.NameScope is not { } names)
                return;

            foreach ((string name, UIElement element) in entries)
                names.Register(name, element);
        }
    }
}
