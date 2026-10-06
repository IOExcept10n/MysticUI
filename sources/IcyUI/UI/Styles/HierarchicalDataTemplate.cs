// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using Icy.Data.Bindings;

namespace Icy.UI.Styles
{
    /// <summary>
    /// A <see cref="DataTemplate"/> that also says where an item's children are, for a <see cref="Controls.TreeView"/>.
    /// </summary>
    /// <remarks>
    /// <code language="xml">
    /// &lt;TreeView.ItemTemplate&gt;
    ///   &lt;HierarchicalDataTemplate ChildrenPath="Children"&gt;
    ///     &lt;TextBlock Text="{Binding Name}"/&gt;
    ///   &lt;/HierarchicalDataTemplate&gt;
    /// &lt;/TreeView.ItemTemplate&gt;
    /// </code>
    /// <para>
    /// <see cref="ChildrenPath"/> is resolved against each item's runtime type. A value that isn't an
    /// <see cref="IEnumerable"/>, or is a <see cref="string"/>, means "no children".
    /// </para>
    /// </remarks>
    public class HierarchicalDataTemplate : DataTemplate
    {
        private string? childrenPath;
        private DynamicPropertyPath? path;

        /// <summary>
        /// Gets or sets the property path, relative to an item, of the item's children collection; or
        /// <see langword="null"/> for items without children.
        /// </summary>
        public string? ChildrenPath
        {
            get => childrenPath;
            set
            {
                childrenPath = value;
                path = string.IsNullOrEmpty(value) ? null : new DynamicPropertyPath(value);
            }
        }

        /// <summary>
        /// Gets <paramref name="item"/>'s children through <see cref="ChildrenPath"/>.
        /// </summary>
        /// <param name="item">The data item.</param>
        /// <returns>The children, or <see langword="null"/> when the path is unset, can't be resolved, or isn't a collection.</returns>
        public IEnumerable? GetChildren(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return path?.GetValue(item) is IEnumerable children and not string ? children : null;
        }
    }
}
