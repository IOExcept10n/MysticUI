using FontStashSharp.RichText;
using MysticUI.Extensions.Input;
using Stride.Core.Mathematics;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a base for controls that can keep one child inside.
    /// </summary>
    /// <typeparam name="T">Type of the control that is stored inside the container.</typeparam>
    public abstract class SingleItemControlBase<T> : Control where T : Control
    {
        private HorizontalAlignment contentHorizontalAlignment;
        private VerticalAlignment contentVerticalAlignment;
        private TextHorizontalAlignment contentTextHorizontalAlignment;
        private bool contentTextWrapping;

        /// <summary>
        /// Provides the access to a <see cref="Child"/> as an enumerable.
        /// </summary>
        protected IEnumerable<Control>? ChildProcess => Child != null ? Enumerable.Repeat(Child, 1) : null;

        /// <summary>
        /// Gets or sets content horizontal alignment.
        /// </summary>
        [Category("Layout")]
        public HorizontalAlignment ContentHorizontalAlignment
        {
            get => contentHorizontalAlignment;
            set
            {
                if (value == contentHorizontalAlignment) return;
                contentHorizontalAlignment = value;
                SetContentAlignment();
                NotifyPropertyChanged(nameof(ContentHorizontalAlignment));
            }
        }

        /// <summary>
        /// Gets or sets content vertical alignment.
        /// </summary>
        [Category("Layout")]
        public VerticalAlignment ContentVerticalAlignment
        {
            get => contentVerticalAlignment;
            set
            {
                if (value == contentVerticalAlignment) return;
                contentVerticalAlignment = value;
                SetContentAlignment();
                NotifyPropertyChanged(nameof(ContentVerticalAlignment));
            }
        }

        /// <summary>
        /// Gets or sets the text alignment for content if the content supports text alignment.
        /// </summary>
        [Category("Layout")]
        public TextHorizontalAlignment ContentTextAlignment
        {
            get => contentTextHorizontalAlignment;
            set
            {
                if (value == contentTextHorizontalAlignment) return;
                contentTextHorizontalAlignment = value;
                SetContentAlignment();
                NotifyPropertyChanged(nameof(ContentTextAlignment));
            }
        }

        /// <summary>
        /// Gets or sets the text wrapping for the content.
        /// </summary>
        [Category("Layout")]
        public bool ContentTextWrapping
        {
            get => contentTextWrapping;
            set
            {
                if (value == contentTextWrapping) return;
                contentTextWrapping = value;
                SetContentAlignment();
                NotifyPropertyChanged(nameof(ContentTextWrapping));
            }
        }

        /// <summary>
        /// Provides an access to a child element of this <see cref="ContentControl"/>.
        /// </summary>
        protected internal T Child { get; private protected set; } = null!;

        /// <inheritdoc/>
        public override Desktop? Desktop
        {
            get => base.Desktop;
            set
            {
                base.Desktop = value;
                if (Child != null) Child.Desktop = value;
            }
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            base.RenderInternal(context);
            Child?.Render(context);
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            base.ArrangeInternal();
            Child?.Arrange(ActualBounds);
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            var selfResult = base.MeasureInternal(availableSize);
            var childResult = Child?.Measure(availableSize) ?? default;
            return new(Math.Max(selfResult.X, childResult.X), Math.Max(selfResult.Y, childResult.Y));
        }

        internal override void InvalidateTransform()
        {
            base.InvalidateTransform();
            Child?.InvalidateTransform();
        }

        /// <inheritdoc/>
        protected internal override void OnMouseEntered()
        {
            base.OnMouseEntered();
            ChildProcess?.ProcessMouseMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnMouseLeft()
        {
            base.OnMouseLeft();
            ChildProcess?.ProcessMouseMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnMouseMove()
        {
            base.OnMouseMove();
            ChildProcess?.ProcessMouseMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchEntered()
        {
            base.OnTouchEntered();
            ChildProcess?.ProcessTouchMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchLeft()
        {
            base.OnTouchLeft();
            ChildProcess?.ProcessTouchMovement();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchMoved()
        {
            base.OnTouchMoved();
            ChildProcess?.ProcessTouchMovement();
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            ChildProcess?.ProcessTouchDown();
            return base.OnTouchDown();
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchUp()
        {
            ChildProcess?.ProcessTouchUp();
            return base.OnTouchUp();
        }

        /// <inheritdoc/>
        protected internal override void OnDoubleClick()
        {
            base.OnDoubleClick();
            ChildProcess?.ProcessDoubleClick();
        }

        /// <inheritdoc/>
        public override bool ProcessControls(Func<Control, bool> action)
        {
            return base.ProcessControls(action) && Child?.ProcessControls(action) != false;
        }

        /// <inheritdoc/>
        protected internal override void ResetDataContext()
        {
            base.ResetDataContext();
            Child?.ResetDataContext();
        }

        /// <inheritdoc/>
        protected internal override void OnDetach()
        {
            base.OnDetach();
            Child?.OnDetach();
        }

        /// <summary>
        /// Resets a child according to content changes.
        /// </summary>
        protected internal void ResetChild()
        {
            Child?.ForceDetach();
            ResetChildInternal();
            if (Child != null)
            {
                Child.Parent = this;
                Child.Desktop = Desktop;
                SetContentAlignment();
            }
            InvalidateArrange();
        }

        /// <summary>
        /// Resets the value of the child inside the control.
        /// </summary>
        protected internal abstract void ResetChildInternal();

        private void SetContentAlignment()
        {
            if (Child != null)
            {
                Child.HorizontalAlignment = ContentHorizontalAlignment;
                Child.VerticalAlignment = ContentVerticalAlignment;
                if (Child is TextBlock textBlock)
                {
                    textBlock.TextAlignment = ContentTextAlignment;
                    textBlock.TextWrapping = ContentTextWrapping;
                }
            }
        }
    }
}