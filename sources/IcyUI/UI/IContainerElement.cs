// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Represents an element that contains a collection of <see cref="UIElement"/> instances.
    /// </summary>
    public interface IContainerElement : IContainerLayout
    {
        /// <summary>
        /// Gets a read-only collection of logical children of the <see cref="IContainerElement"/> instance.
        /// </summary>
        UIElementCollection Children { get; }
    }
}