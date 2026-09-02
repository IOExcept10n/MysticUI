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
        /// Gets the visible viewport's height, i.e. this control's <see cref="Control.ContentBounds"/> height
        /// (already inset by <see cref="Control.BorderThickness"/> and <see cref="Control.Padding"/>).
        /// </summary>
        public float ViewportHeight => ContentBounds.Height;

        /// <summary>
        /// Gets the visible viewport's width, i.e. this control's <see cref="Control.ContentBounds"/> width
        /// (already inset by <see cref="Control.BorderThickness"/> and <see cref="Control.Padding"/>).
        /// </summary>
        public float ViewportWidth => ContentBounds.Width;

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
        protected internal override bool OnScroll(ScrollInfo info)
        {
            base.OnScroll(info);
            if (info.ScrollOrientation == Orientation.Vertical)
            {
                float before = verticalOffset;
                VerticalOffset -= info.Delta;
                return verticalOffset != before;
            }
            else
            {
                float before = horizontalOffset;
                HorizontalOffset -= info.Delta;
                return horizontalOffset != before;
            }
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            // Chrome must be given room to grow to Content's full natural size along the scrollable axes, not
            // shrunk to fit ActualBounds the way the standard Arrange()/CalculateOverflow flow would (a child can
            // never overflow its parent there) - that's the entire point of scrolling. ClipToBounds on this
            // control (not Chrome) still crops the overflow visually. Chrome's origin stays at ActualBounds' own
            // top-left - only ever extended, never moved - so Background/BorderBrush (drawn across Chrome's own
            // full local bounds) still start exactly where this control's border box starts; Chrome's own
            // BorderThickness/Padding (see Control.ArrangeContent) then insets Content the normal way once
            // Chrome itself has room to fit it.
            Size contentDesired = Content?.Measure() ?? Size.Empty;
            Thickness inset = BorderThickness + Padding;
            Rectangle chromeRect = new(
                ActualBounds.X,
                ActualBounds.Y,
                Math.Max(ActualBounds.Width, contentDesired.Width + inset.Width),
                Math.Max(ActualBounds.Height, contentDesired.Height + inset.Height));

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
