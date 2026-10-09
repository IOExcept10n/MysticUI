// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Markup;

namespace Icy.UI.Styles
{
    /// <summary>
    /// Represents a named, mutually-exclusive set of <see cref="VisualState"/>s - at most one state within a group
    /// is active on a given element at a time.
    /// </summary>
    /// <param name="name">The name of this group.</param>
    [ContentProperty(nameof(States))]
    public class VisualStateGroup(string name)
    {
        /// <summary>
        /// Gets the name of this group.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the states within this group.
        /// </summary>
        public List<VisualState> States { get; } = [];
    }
}
