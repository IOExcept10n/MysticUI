// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using Icy.Data;
using Icy.Markup;
using Icy.Rendering;

namespace Icy.UI
{
    /// <summary>
    /// Represents a container element that can hold other UI elements.
    /// </summary>
    /// <remarks>
    /// The <see cref="Panel" /> class is a container element that can hold other <see cref="UIElement" /> objects.
    /// It provides events and methods for managing the child elements.
    /// </remarks>
    [ContentProperty(nameof(Children))]
    public class Panel : UIElement, IContainerElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Panel" /> class.
        /// </summary>
        /// <remarks>
        /// The constructor sets up event handlers for the <see cref="Children" /> collection.
        /// </remarks>
        public Panel()
        {
            Children.CollectionChanged += OnChildrenUpdated;
            Children.ItemAdded += OnChildAdded;
            Children.ItemAdding += OnChildAdding;
            Children.ItemRemoved += OnChildRemoved;
            Children.ItemRemoving += OnChildRemoving;
            Children.Reset += OnChildrenReset;
            Children.Resetting += OnChildrenResetting;
        }

        /// <summary>
        /// Occurs when a child element is added to the <see cref="Children" /> collection.
        /// </summary>
        /// <remarks>
        /// This event is raised after a child element is added to the <see cref="Children" /> collection.
        /// </remarks>
        public event DataEventHandler<UIElement>? ChildAdded;

        /// <summary>
        /// Occurs when a child element is being added to the <see cref="Children" /> collection.
        /// </summary>
        /// <remarks>
        /// This event is raised before a child element is added to the <see cref="Children" /> collection.
        /// The event can be canceled to prevent the addition of the child element.
        /// </remarks>
        public event CancellableEventHandler<UIElement>? ChildAdding;

        /// <summary>
        /// Occurs when a child element is removed from the <see cref="Children" /> collection.
        /// </summary>
        /// <remarks>
        /// This event is raised after a child element is removed from the <see cref="Children" /> collection.
        /// </remarks>
        public event DataEventHandler<UIElement>? ChildRemoved;

        /// <summary>
        /// Occurs when a child element is being removed from the <see cref="Children" /> collection.
        /// </summary>
        /// <remarks>
        /// This event is raised before a child element is removed from the <see cref="Children" /> collection.
        /// The event can be canceled to prevent the removal of the child element.
        /// </remarks>
        public event CancellableEventHandler<UIElement>? ChildRemoving;

        /// <summary>
        /// Occurs when the <see cref="Children" /> collection is reset.
        /// </summary>
        /// <remarks>
        /// This event is raised when the entire <see cref="Children" /> collection is reset.
        /// </remarks>
        public event EventHandler? ChildrenReset;

        /// <summary>
        /// Occurs when the <see cref="Children" /> collection is about to be reset.
        /// </summary>
        /// <remarks>
        /// This event is raised before the entire <see cref="Children" /> collection is reset.
        /// The event can be canceled to prevent the reset.
        /// </remarks>
        public event CancelEventHandler? ChildrenResetting;

        /// <summary>
        /// Occurs when the <see cref="Children" /> collection is updated.
        /// </summary>
        /// <remarks>
        /// This event is raised whenever the <see cref="Children" /> collection is updated, such as when an element is added, removed, or the collection is reset.
        /// </remarks>
        public event NotifyCollectionChangedEventHandler? ChildrenUpdated;

        /// <summary>
        /// Gets the collection of child elements.
        /// </summary>
        /// <value>
        /// The collection of child elements.
        /// </value>
        public UIElementCollection Children { get; } = [];

        /// <summary>
        /// Gets the content bounds of the panel, which is the actual bounds minus the padding.
        /// </summary>
        /// <value>
        /// The content bounds of the panel.
        /// </value>
        public Rectangle ContentBounds => ActualBounds - Padding;

        /// <summary>
        /// Raises the <see cref="ChildAdded" /> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GenericEventArgs{UIElement}" /> instance containing the event data.</param>
        protected virtual void OnChildAdded(object? sender, GenericEventArgs<UIElement> e)
        {
            e.Data.Parent = this;
            e.Data.Canvas = Canvas;
            ChildAdded?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ChildAdding" /> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CancellableEventArgs{UIElement}" /> instance containing the event data.</param>
        protected virtual void OnChildAdding(object? sender, CancellableEventArgs<UIElement> e)
        {
            ChildAdding?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ChildRemoved" /> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="GenericEventArgs{UIElement}" /> instance containing the event data.</param>
        protected virtual void OnChildRemoved(object? sender, GenericEventArgs<UIElement> e)
        {
            e.Data.Parent = null;
            e.Data.Canvas = null;
            ChildRemoved?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ChildRemoving" /> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CancellableEventArgs{UIElement}" /> instance containing the event data.</param>
        protected virtual void OnChildRemoving(object? sender, CancellableEventArgs<UIElement> e)
        {
            ChildRemoving?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ChildrenReset" /> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnChildrenReset(object? sender, EventArgs e)
        {
            ChildrenReset?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ChildrenResetting" /> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CancelEventArgs" /> instance containing the event data.</param>
        protected virtual void OnChildrenResetting(object? sender, CancelEventArgs e)
        {
            ChildrenResetting?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ChildrenUpdated" /> event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnChildrenUpdated(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (UIElement child in e.OldItems!)
                {
                    child.Parent = null;
                    child.Canvas = null;
                }
            }

            InvalidateMeasure();
            ChildrenUpdated?.Invoke(this, e);
        }

        protected override void ArrangeContent()
        {
            foreach (var child in Children)
            {
                if (child.IsVisible)
                    child.Arrange();
            }
        }

        protected override Size MeasureContent()
        {
            Size result = Size.Empty;
            foreach (var child in Children)
            {
                if (child.IsVisible)
                    result = result.Max(child.DesiredSize);
            }

            return result;
        }

        protected override void OnRender(IRenderContext context)
        {
            base.OnRender(context);
            foreach (var child in GetVisualChildren())
            {
                child.Draw(context);
            }
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren() =>
            Children.OrderBy(child => child.ZIndex);
    }
}