using MysticUI.Extensions;
using Stride.Core.Mathematics;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a control that provides a scrollable unbounded viewport to store controls bigger than available size.
    /// Note that measures are presented in available size so scrolls are available only when measure fails.
    /// </summary>
    public class ScrollViewer : ContentControl
    {
        private ScrollbarVisibility horizontalScrollbarVisibility;
        private ScrollbarVisibility verticalScrollbarVisibility;
        private Orientation scrollbarOrientation;
        internal bool horizontalScrollingOn;
        internal bool verticalScrollingOn;
        internal Rectangle horizontalScrollbarFrame;
        private Rectangle horizontalScrollbarThumb;
        internal Rectangle verticalScrollbarFrame;
        private Rectangle verticalScrollbarThumb;
        private int thumbMaximumX;
        private int thumbMaximumY;
        private int? startBoundsPosition;

        /// <summary>
        /// Gets the maximal size of both scrolls.
        /// </summary>
        [Browsable(false), XmlIgnore, JsonIgnore]
        public Point ScrollMaximum
        {
            get
            {
                if (Child == null)
                {
                    return Point.Zero;
                }

                Point result = new(Child.Bounds.Width - ActualBounds.Width + VerticalScrollbarWidth, Child.Bounds.Height - ActualBounds.Height + HorizontalScrollbarHeight);
                result.X = Math.Max(result.X, 0);
                result.Y = Math.Max(result.Y, 0);
                return result;
            }
        }

        /// <summary>
        /// Gets or sets the current scroll position.
        /// </summary>
        [Browsable(false), XmlIgnore, JsonIgnore]
        public Point ScrollPosition
        {
            get
            {
                if (Child == null) return Point.Zero;
                return new(-Child.X, -Child.Y);
            }
            set
            {
                if (Child == null) return;
                Child.Location = (Point)(-(Vector2)value);
                Scroll?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(ScrollPosition));
            }
        }

        /// <summary>
        /// Gets or sets the size-independent scroll values.
        /// </summary>
        internal Point ThumbPosition
        {
            get
            {
                var result = Point.Zero;
                if (ScrollMaximum.X > 0)
                {
                    result.X = ScrollPosition.X * thumbMaximumX / ScrollMaximum.X;
                }
                if (ScrollMaximum.Y > 0)
                {
                    result.Y = ScrollPosition.Y * thumbMaximumY / ScrollMaximum.Y;
                }
                return result;
            }
        }

        /// <summary>
        /// Background texture of the horizontal scroll.
        /// </summary>
        [Category("Appearance")]
        public IBrush? HorizontalScrollBackground { get; set; }

        /// <summary>
        /// Texture of the horizontal scroll knob.
        /// </summary>
        [Category("Appearance")]
        public IBrush? HorizontalScrollKnob { get; set; }

        /// <summary>
        /// Background texture of the vertical scroll.
        /// </summary>
        [Category("Appearance")]
        public IBrush? VerticalScrollBackground { get; set; }

        /// <summary>
        /// Texture of the vertical scroll knob.
        /// </summary>
        [Category("Appearance")]
        public IBrush? VerticalScrollKnob { get; set; }

        /// <summary>
        /// Gets or sets the visibility of the horizontal scroll bar.
        /// </summary>
        [Category("Behavior")]
        public ScrollbarVisibility HorizontalScrollbarVisibility
        {
            get => horizontalScrollbarVisibility;
            set
            {
                if (value == horizontalScrollbarVisibility) return;
                horizontalScrollbarVisibility = value;
                InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the visibility of the vertical scroll bar.
        /// </summary>
        [Category("Behavior")]
        public ScrollbarVisibility VerticalScrollbarVisibility
        {
            get => verticalScrollbarVisibility;
            set
            {
                if (value == verticalScrollbarVisibility) return;
                verticalScrollbarVisibility = value;
                InvalidateArrange();
            }
        }

        /// <inheritdoc/>
        public override Desktop? Desktop
        {
            get => base.Desktop;
            set
            {
                if (value == base.Desktop) return;
                if (base.Desktop != null)
                {
                    base.Desktop.TouchMove -= DesktopTouchMovedHandler!;
                    base.Desktop.TouchUp -= DesktopTouchUpHandler!;
                }
                base.Desktop = value;
                if (base.Desktop != null)
                {
                    base.Desktop.TouchMove += DesktopTouchMovedHandler!;
                    base.Desktop.TouchUp += DesktopTouchUpHandler!;
                }
            }
        }

        [Category("Layout")]
        internal int HorizontalScrollbarHeight
        {
            get
            {
                int result = 0;
                if (HorizontalScrollBackground is IImage image)
                    result = image.Size.Y;
                if (HorizontalScrollKnob is IImage image2 && image2.Size.Y > result)
                    result = image2.Size.Y;
                return result;
            }
        }

        [Category("Layout")]
        internal int VerticalScrollbarWidth
        {
            get
            {
                int result = 0;
                if (HorizontalScrollBackground is IImage image)
                    result = image.Size.X;
                if (HorizontalScrollKnob is IImage image2 && image2.Size.X > result)
                    result = image2.Size.X;
                return result;
            }
        }

        [XmlIgnore, JsonIgnore, Browsable(false)]
        internal bool ShowHorizontalScrollbar { get; private set; }

        [XmlIgnore, JsonIgnore, Browsable(false)]
        internal bool ShowVerticalScrollbar { get; private set; }

        /// <summary>
        /// Occurs when the scroll value changes.
        /// </summary>
        public event EventHandler? Scroll;

        /// <summary>
        /// Creates a new instance of the <see cref="ScrollViewer"/> class.
        /// </summary>
        public ScrollViewer()
        {
            ClipToBounds = true;
            CaptureMouseRoll = true;
            AcceptFocus = false;
            horizontalScrollingOn = verticalScrollingOn == false;
            ShowVerticalScrollbar = ShowHorizontalScrollbar == true;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }

        /// <inheritdoc/>
        protected internal override void OnContentChanged()
        {
            base.OnContentChanged();
            ResetScroll();
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchUp()
        {
            startBoundsPosition = null;
            return base.OnTouchUp();
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            base.OnTouchDown();
            if (Desktop == null) return false;
            var touchPosition = ToLocal(Desktop.TouchPosition);
            Rectangle r = verticalScrollbarThumb;
            Point thumbPosition = ThumbPosition;
            r.Y += thumbPosition.Y;
            if (ShowVerticalScrollbar && verticalScrollingOn && r.Contains(touchPosition))
            {
                startBoundsPosition = Desktop.TouchPosition.Y;
                scrollbarOrientation = Orientation.Vertical;
            }

            r = horizontalScrollbarThumb;
            r.X += thumbPosition.X;
            if (ShowHorizontalScrollbar && horizontalScrollingOn && r.Contains(touchPosition))
            {
                startBoundsPosition = Desktop.TouchPosition.X;
                scrollbarOrientation = Orientation.Horizontal;
            }
            return true;
        }

        /// <inheritdoc/>
        protected internal override void OnMouseWheel(float delta)
        {
            base.OnMouseWheel(delta);
            int step;
            if (Desktop?.IsShiftPressed == true && horizontalScrollingOn)
            {
                step = 10 * ScrollMaximum.X / thumbMaximumX;
                scrollbarOrientation = Orientation.Horizontal;
            }
            else if (verticalScrollingOn)
            {
                step = 10 * ScrollMaximum.Y / thumbMaximumY;
                scrollbarOrientation = Orientation.Vertical;
            }
            else return;
            if (delta > 0) step = -step;
            MoveThumb(step);
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            if (Child == null || !Child.Visible) return;
            base.RenderInternal(context);

            if (horizontalScrollingOn && ShowHorizontalScrollbar)
            {
                HorizontalScrollBackground?.Draw(context, horizontalScrollbarFrame);
                var r = horizontalScrollbarThumb;
                r.X += ThumbPosition.X;
                HorizontalScrollKnob?.Draw(context, r);
            }

            if (verticalScrollingOn && ShowVerticalScrollbar)
            {
                VerticalScrollBackground?.Draw(context, verticalScrollbarFrame);
                var r = verticalScrollbarThumb;
                r.Y += ThumbPosition.Y;
                VerticalScrollKnob?.Draw(context, r);
            }
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            if (Child == null) return Point.Zero;
            var measureSize = Child.Measure(availableSize);
            bool outOfXBounds = HorizontalScrollbarVisibility != ScrollbarVisibility.Hidden && measureSize.X > availableSize.X;
            bool outOfYBounds = VerticalScrollbarVisibility != ScrollbarVisibility.Hidden && measureSize.Y > availableSize.Y;
            ShowHorizontalScrollbar = HorizontalScrollbarVisibility == ScrollbarVisibility.Visible ||
                                      HorizontalScrollbarVisibility == ScrollbarVisibility.Disabled ||
                                      outOfXBounds;
            ShowVerticalScrollbar = VerticalScrollbarVisibility == ScrollbarVisibility.Visible ||
                                    VerticalScrollbarVisibility == ScrollbarVisibility.Disabled ||
                                    outOfYBounds;
            if (outOfXBounds)
            {
                measureSize.Y += HorizontalScrollbarHeight;
            }
            else if (outOfYBounds)
            {
                measureSize.X += VerticalScrollbarWidth;
            }
            return measureSize;
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            if (Child == null) return;
            base.ArrangeInternal();
            Rectangle bounds = ActualBounds;
            Point availableSize = new(Width, Height);
            Point oldMeasureSize = Child.TestMeasure();
            horizontalScrollingOn = oldMeasureSize.X > bounds.Width && HorizontalScrollbarVisibility != ScrollbarVisibility.Disabled;
            verticalScrollingOn = oldMeasureSize.Y > bounds.Height && VerticalScrollbarVisibility != ScrollbarVisibility.Disabled;
            if (oldMeasureSize.X > bounds.Width || oldMeasureSize.Y > bounds.Height)
            {
                if (horizontalScrollingOn && ShowHorizontalScrollbar)
                {
                    availableSize.Y -= HorizontalScrollbarHeight;
                    if (availableSize.Y < 0) availableSize.Y = 0;
                }
                if (verticalScrollingOn && ShowVerticalScrollbar)
                {
                    availableSize.X -= VerticalScrollbarWidth;
                    if (availableSize.X < 0) availableSize.X = 0;
                }

                Point measureSize = Child.Measure(availableSize);
                int boundsWidth = bounds.Width;
                if (verticalScrollingOn && ShowVerticalScrollbar) boundsWidth -= VerticalScrollbarWidth;
                horizontalScrollbarFrame = new Rectangle(bounds.Left, bounds.Bottom - HorizontalScrollbarHeight, boundsWidth, HorizontalScrollbarHeight);
                int measureWidth = measureSize.X;
                if (measureWidth == 0) measureWidth++;
                Point horizontalKnobSize = (HorizontalScrollKnob as IImage)?.Size ?? new Point();
                horizontalScrollbarThumb = new Rectangle(
                    bounds.Left,
                    bounds.Bottom - HorizontalScrollbarHeight,
                    Math.Max(horizontalKnobSize.X, boundsWidth * boundsWidth / measureWidth),
                    horizontalKnobSize.Y);

                int boundsHeight = bounds.Height;
                if (horizontalScrollingOn && ShowHorizontalScrollbar) boundsHeight -= HorizontalScrollbarHeight;
                verticalScrollbarFrame = new Rectangle(bounds.Right - VerticalScrollbarWidth, bounds.Top, VerticalScrollbarWidth, boundsHeight);
                int measureHeight = measureSize.Y;
                if (measureHeight == 0) measureHeight++;
                Point verticalKnobSize = (VerticalScrollKnob as IImage)?.Size ?? new Point();
                verticalScrollbarThumb = new Rectangle(bounds.Left + bounds.Width - VerticalScrollbarWidth,
                                                       bounds.Top,
                                                       verticalKnobSize.X,
                                                       Math.Max(verticalKnobSize.Y, boundsHeight * boundsHeight / measureHeight));

                thumbMaximumX = boundsWidth - horizontalScrollbarThumb.Width;
                thumbMaximumY = boundsHeight - verticalScrollbarThumb.Height;

                if (thumbMaximumX == 0) thumbMaximumX++;
                if (thumbMaximumY == 0) thumbMaximumY++;

                if (horizontalScrollingOn && ShowHorizontalScrollbar)
                {
                    bounds.Width = measureSize.X;
                }
                else if (oldMeasureSize.X > bounds.Width)
                {
                    bounds.Width = oldMeasureSize.X;
                }
                else
                {
                    bounds.Width = availableSize.X;
                }

                if (verticalScrollingOn && ShowVerticalScrollbar)
                {
                    bounds.Height = measureSize.Y;
                }
                else if (oldMeasureSize.Y > bounds.Height)
                {
                    bounds.Height = oldMeasureSize.Y;
                }
                else
                {
                    bounds.Height = availableSize.Y;
                }
            }

            Child.Arrange(bounds);

            var scrollPosition = ScrollPosition;
            if (scrollPosition.X > ScrollMaximum.X)
            {
                scrollPosition.X = ScrollMaximum.X;
            }
            if (scrollPosition.Y > ScrollMaximum.Y)
            {
                scrollPosition.Y = ScrollMaximum.Y;
            }
            ScrollPosition = scrollPosition;
        }

        /// <summary>
        /// Resets the scroll value to zero.
        /// </summary>
        public void ResetScroll()
        {
            ScrollPosition = Point.Zero;
        }

        private void DesktopTouchMovedHandler(object sender, EventArgs e)
        {
            if (!startBoundsPosition.HasValue || Desktop == null) return;
            int delta;
            if (scrollbarOrientation == Orientation.Horizontal)
            {
                delta = (int)Math.Round((Desktop.TouchPosition.X - startBoundsPosition.Value) * (float)ScrollMaximum.X / thumbMaximumX);
                startBoundsPosition = Desktop.TouchPosition.X;
            }
            else
            {
                delta = (int)Math.Round((Desktop.TouchPosition.Y - startBoundsPosition.Value) * (float)ScrollMaximum.Y / thumbMaximumY);
                startBoundsPosition = Desktop.TouchPosition.Y;
            }
            MoveThumb(delta);
        }

        private void DesktopTouchUpHandler(object sender, EventArgs e)
        {
            startBoundsPosition = null;
        }

        private void MoveThumb(int delta)
        {
            var newPosition = ScrollPosition;
            if (scrollbarOrientation == Orientation.Horizontal && horizontalScrollingOn)
            {
                newPosition.X = Math.Clamp(ScrollPosition.X + delta, 0, ScrollMaximum.X);
            }
            else if (verticalScrollingOn)
            {
                newPosition.Y = Math.Clamp(ScrollPosition.Y + delta, 0, ScrollMaximum.Y);
            }
            else return;
            ScrollPosition = newPosition;
        }
    }
}