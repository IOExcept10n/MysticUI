// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data.Markup.Attributes;
using Icy.Input.Events;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Clips <see cref="ContentControl.Content"/> to this control's bounds and lets it be scrolled within that
    /// viewport via <see cref="HorizontalOffset"/>/<see cref="VerticalOffset"/> - settable directly, or driven by
    /// the mouse wheel/touch-swipe (see <see cref="IScrollEvents"/>).
    /// </summary>
    /// <remarks>
    /// No visible scrollbar thumbs are drawn in v1 - this is purely the viewport/offset/clipping model. A visual
    /// scrollbar can be layered on top later using <see cref="ExtentWidth"/>/<see cref="ExtentHeight"/>/
    /// <see cref="ViewportWidth"/>/<see cref="ViewportHeight"/> to size a thumb.
    /// </remarks>
    public class ScrollViewer : ContentControl
    {
        private float horizontalOffset;
        private float verticalOffset;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrollViewer"/> class.
        /// </summary>
        public ScrollViewer()
        {
            ClipToBounds = true;
        }

        /// <summary>
        /// Occurs when <see cref="HorizontalOffset"/> or <see cref="VerticalOffset"/> changes.
        /// </summary>
        public event EventHandler? ScrollChanged;

        /// <summary>
        /// Gets <see cref="ContentControl.Content"/>'s full natural height, regardless of how much of it is currently visible.
        /// </summary>
        public float ExtentHeight => Content?.Measure().Height ?? 0;

        /// <summary>
        /// Gets <see cref="ContentControl.Content"/>'s full natural width, regardless of how much of it is currently visible.
        /// </summary>
        public float ExtentWidth => Content?.Measure().Width ?? 0;

        /// <summary>
        /// Gets or sets how far <see cref="ContentControl.Content"/> is scrolled horizontally, clamped to
        /// <c>[0, <see cref="ExtentWidth"/> - <see cref="ViewportWidth"/>]</c>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0f)]
        [RegisterReference]
        public float HorizontalOffset
        {
            get => horizontalOffset;
            set
            {
                float clamped = float.Clamp(value, 0, Math.Max(0, ExtentWidth - ViewportWidth));
                if (SetProperty(ref horizontalOffset, clamped))
                {
                    UpdateContentOffset();
                    ScrollChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the visible viewport's height (this control's content area, inset by <see cref="Control.BorderThickness"/>).
        /// </summary>
        public float ViewportHeight => Math.Max(0, ContentBounds.Height - BorderThickness.Height);

        /// <summary>
        /// Gets the visible viewport's width (this control's content area, inset by <see cref="Control.BorderThickness"/>).
        /// </summary>
        public float ViewportWidth => Math.Max(0, ContentBounds.Width - BorderThickness.Width);

        /// <summary>
        /// Gets or sets how far <see cref="ContentControl.Content"/> is scrolled vertically, clamped to
        /// <c>[0, <see cref="ExtentHeight"/> - <see cref="ViewportHeight"/>]</c>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0f)]
        [RegisterReference]
        public float VerticalOffset
        {
            get => verticalOffset;
            set
            {
                float clamped = float.Clamp(value, 0, Math.Max(0, ExtentHeight - ViewportHeight));
                if (SetProperty(ref verticalOffset, clamped))
                {
                    UpdateContentOffset();
                    ScrollChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <inheritdoc/>
        protected internal override void OnScroll(ScrollInfo info)
        {
            base.OnScroll(info);
            if (info.ScrollOrientation == Orientation.Vertical)
                VerticalOffset -= info.Delta;
            else
                HorizontalOffset -= info.Delta;
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            // Chrome must be given room to grow to Content's full natural size along the scrollable axes, not
            // shrunk to fit ContentBounds the way the standard Arrange()/CalculateOverflow flow would (a child can
            // never overflow its parent there) - that's the entire point of scrolling. ClipToBounds on this
            // control (not Chrome) still crops the overflow visually.
            Size contentDesired = Content?.Measure() ?? Size.Empty;
            Rectangle chromeRect = new(
                ContentBounds.X,
                ContentBounds.Y,
                Math.Max(ContentBounds.Width, contentDesired.Width + BorderThickness.Width),
                Math.Max(ContentBounds.Height, contentDesired.Height + BorderThickness.Height));

            Chrome.InvalidateArrange();
            Chrome.Arrange(chromeRect);

            // Re-clamp now that Extent/Viewport are up to date post-arrange (e.g. the viewport just shrank).
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
        }

        private void UpdateContentOffset()
        {
            if (Content != null)
                Content.LayoutOffset = new Vector2(-HorizontalOffset, -VerticalOffset);
        }
    }
}
