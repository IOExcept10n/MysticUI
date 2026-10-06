// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// What a placement strategy knows about a container and the element being moved or resized in it. Rectangles are in
    /// the container's local space: (0, 0) is the container's top-left corner.
    /// </summary>
    public sealed class PlacementContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlacementContext"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="element">The element being placed.</param>
        /// <param name="children">The container's other content children that have live copies, in markup order.</param>
        /// <param name="contentCount">How many content children the container has in markup, without <paramref name="element"/>.</param>
        /// <param name="elementBounds">The element's bounds when the gesture started, in the container's local space.</param>
        /// <param name="isCurrentContainer">Whether <paramref name="element"/> is already a child of <paramref name="container"/>.</param>
        /// <param name="elementIndex">
        /// The element's content index in markup when <paramref name="isCurrentContainer"/> is <see langword="true"/>, or
        /// <c>-1</c> when it isn't known; strategies then guess it from the children's positions.
        /// </param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public PlacementContext(UIElement container, UIElement element, IReadOnlyList<PlacementChild> children, int contentCount, Rectangle elementBounds, bool isCurrentContainer, int elementIndex = -1)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(children);
            Container = container;
            Element = element;
            Children = children;
            ContentCount = contentCount;
            ElementBounds = elementBounds;
            IsCurrentContainer = isCurrentContainer;
            ElementIndex = isCurrentContainer ? elementIndex : -1;
            ContainerContent = ToLocal(container, container is IContainerLayout layout ? layout.ContentBounds : container.ActualBounds);
        }

        /// <summary>Gets the container.</summary>
        public UIElement Container { get; }

        /// <summary>Gets the element being placed.</summary>
        public UIElement Element { get; }

        /// <summary>Gets the container's other content children that have live copies, in markup order.</summary>
        public IReadOnlyList<PlacementChild> Children { get; }

        /// <summary>Gets how many content children the container has in markup, without the element being placed.</summary>
        public int ContentCount { get; }

        /// <summary>Gets the element's bounds when the gesture started, in the container's local space.</summary>
        public Rectangle ElementBounds { get; }

        /// <summary>Gets the area the container lays its children out in (inside its padding), in its local space.</summary>
        public Rectangle ContainerContent { get; }

        /// <summary>Gets a value indicating whether the element already is one of the container's children.</summary>
        public bool IsCurrentContainer { get; }

        /// <summary>
        /// Gets the element's content index in the container's markup, or <c>-1</c> when it isn't a child of the container
        /// or the index isn't known. A move that keeps this index keeps the markup order.
        /// </summary>
        public int ElementIndex { get; }

        /// <summary>
        /// Converts a rectangle in layout space (an element's <see cref="UIElement.ActualBounds"/>) into
        /// <paramref name="container"/>'s local space.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="absolute">A rectangle in the same space as the container's <see cref="UIElement.ActualBounds"/>.</param>
        /// <returns>The rectangle relative to the container's top-left corner.</returns>
        public static Rectangle ToLocal(UIElement container, Rectangle absolute)
        {
            ArgumentNullException.ThrowIfNull(container);
            return absolute with { X = absolute.X - container.ActualBounds.X, Y = absolute.Y - container.ActualBounds.Y };
        }
    }
}
