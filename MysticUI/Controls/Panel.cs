using CommunityToolkit.Diagnostics;
using MysticUI.Extensions;
using MysticUI.Extensions.Input;
using Stride.Core.Mathematics;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// An ultimate class for panels and other container controls.
    /// </summary>
    public class Panel : Control, IContainerControl
    {
        private List<Control> childrenCopy = new();
        private bool shouldResetChildren = true;

        /// <summary>
        /// Returns the control at given position.
        /// </summary>
        public virtual Control this[int index]
        {
            get => childrenCopy[index];
        }

        /// <summary>
        /// Represents actual children list of this panel.
        /// </summary>
        [Browsable(false)]
        [Content]
        public ObservableCollection<Control> Children { get; } = new();

        /// <inheritdoc/>
        IReadOnlyCollection<Control> IContainerControl.Children
        {
            get
            {
                UpdateChildren();
                return childrenCopy;
            }
        }

        /// <inheritdoc/>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public override Desktop? Desktop
        {
            get => base.Desktop;
            set
            {
                base.Desktop = value;
                foreach (var control in Children)
                {
                    control.Desktop = value;
                }
            }
        }

        /// <inheritdoc/>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public int Count => Children.Count;

        /// <inheritdoc/>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public virtual bool IsReadOnly => false;

        /// <summary>
        /// Occurs when the content of <see cref="Children"/> property changes.
        /// </summary>
        public event NotifyCollectionChangedEventHandler? ChildrenChanged;

        /// <summary>
        /// Creates a new instance of the <see cref="Panel"/> class.
        /// </summary>
        public Panel()
        {
            Children = new ObservableCollection<Control>();
            Children.CollectionChanged += OnChildrenChanged!;
        }

        /// <inheritdoc/>
        protected void InvalidateChildren()
        {
            InvalidateMeasure();
            shouldResetChildren = true;
        }

        /// <inheritdoc/>
        protected internal override void OnMouseEntered()
        {
            base.OnMouseEntered();

            Children.ProcessMouseMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnMouseLeft()
        {
            base.OnMouseLeft();

            Children.ProcessMouseMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnMouseMove()
        {
            base.OnMouseMove();

            Children.ProcessMouseMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchEntered()
        {
            base.OnTouchEntered();

            Children.ProcessTouchMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchLeft()
        {
            base.OnTouchLeft();

            Children.ProcessTouchMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchMoved()
        {
            base.OnTouchMoved();

            Children.ProcessTouchMovement();
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            base.OnTouchDown();

            return Children.ProcessTouchDown();
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchUp()
        {
            Children.ProcessTouchUp();

            return base.OnTouchUp();
        }

        /// <inheritdoc/>
        protected internal override void OnDoubleClick()
        {
            base.OnDoubleClick();

            Children.ProcessDoubleClick();
        }

        /// <inheritdoc/>
        public override bool ProcessControls(Func<Control, bool> action)
        {
            return action(this) && Children.All(x => x.ProcessControls(action));
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            base.RenderInternal(context);

            foreach (var child in Children)
            {
                child.Render(context);
            }
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            foreach (var control in Children)
            {
                if (control.Visible)
                {
                    control.Arrange(ActualBounds);
                }
            }
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            Point result = Point.Zero;
            foreach (var control in Children)
            {
                if (control.Visible)
                {
                    Point measure = control.Measure(availableSize);
                    result.X = Math.Max(measure.X, result.X);
                    result.Y = Math.Max(measure.Y, result.Y);
                }
            }
            return result;
        }

        internal override void InvalidateTransform()
        {
            base.InvalidateTransform();

            foreach (var child in Children)
            {
                child.InvalidateTransform();
            }
        }

        /// <summary>
        /// Removes a control at a specified index in the panel.
        /// </summary>
        /// <param name="index">Index of the removed control.</param>
        public virtual void RemoveAt(int index)
        {
            Guard.IsFalse(IsReadOnly, nameof(IsReadOnly), "The panel shouldn't be read-only to modify it.");
            Children.RemoveAt(index);
            InvalidateChildren();
        }

        /// <inheritdoc/>
        public virtual void Add(Control item)
        {
            Guard.IsFalse(IsReadOnly, nameof(IsReadOnly), "The panel shouldn't be read-only to modify it.");
            Children.Add(item);
            InvalidateChildren();
        }

        /// <summary>
        /// Clears all controls from the panel.
        /// </summary>
        public virtual void Clear()
        {
            Guard.IsFalse(IsReadOnly, nameof(IsReadOnly), "The panel shouldn't be read-only to modify it.");
            Children.Clear();
            InvalidateChildren();
        }

        /// <summary>
        /// Determines whether the panel contains a control.
        /// </summary>
        /// <param name="item">A control to check.</param>
        /// <returns><see langword="true"/> if the control is presented in the panel, <see langword="false"/> otherwise.</returns>
        public bool Contains(Control item)
        {
            return Children.Contains(item);
        }

        /// <inheritdoc/>
        public virtual bool Remove(Control item)
        {
            Guard.IsFalse(IsReadOnly, nameof(IsReadOnly), "The panel shouldn't be read-only to modify it.");
            return Children.Remove(item);
        }

        /// <inheritdoc/>
        public IEnumerator<Control> GetEnumerator()
        {
            UpdateChildren();
            return childrenCopy.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Raises the <see cref="ChildrenChanged"/> event.
        /// </summary>
        protected internal virtual void OnChildrenChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            ChildrenChanged?.Invoke(this, args);
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        foreach (Control c in args.NewItems!)
                        {
                            c.Desktop = Desktop;
                            c.Parent = this;
                        }

                        break;
                    }

                case NotifyCollectionChangedAction.Remove:
                    {
                        foreach (Control c in args.OldItems!)
                        {
                            c.ForceDetach();
                        }

                        break;
                    }

                case NotifyCollectionChangedAction.Reset:
                    {
                        foreach (Control c in childrenCopy)
                        {
                            c.ForceDetach();
                        }

                        break;
                    }
            }

            InvalidateChildren();
        }

        /// <inheritdoc/>
        protected internal override void OnDetach()
        {
            base.OnDetach();
            Children.Clear();
        }

        /// <inheritdoc/>
        protected internal override void ResetDataContext()
        {
            base.ResetDataContext();
            foreach (var child in Children)
            {
                child.ResetDataContext();
            }
        }

        private void UpdateChildren()
        {
            if (!shouldResetChildren) return;
            childrenCopy = new(Children.OrderBy(x => x.ZIndex));
            shouldResetChildren = false;
        }
    }
}