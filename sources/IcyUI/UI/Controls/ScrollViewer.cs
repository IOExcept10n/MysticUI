// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.UI.Styles;

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
        private const float MaxFlingSpeed = 8000f;
        private const float MinFlingSpeed = 50f;
        private readonly InertiaDriver inertia;
        private float panningDeceleration = 1500f;
        private Point lastDragScreenPoint;
        private bool swallowTap;
        private float horizontalOffset;
        private Vector2? panAnchor;
        private DragAxes panAxes;
        private PanningMode panningMode = PanningMode.Auto;
        private DragAxes pendingClaim;
        private float verticalOffset;
        private IVirtualizingScrollInfo? subscribedVirtualizingContent;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrollViewer"/> class.
        /// </summary>
        public ScrollViewer()
        {
            ClipToBounds = true;
            inertia = new InertiaDriver(ApplyInertiaStep);

            // A press stops a fling; the rest of that press (including its tap) lands on this list, not on the item underneath.
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ControlState))
                    return;
                if (ControlState.HasFlag(ControlState.Pressed) && inertia.IsRunning)
                {
                    inertia.Stop();
                    swallowTap = true;
                }
                else if (!ControlState.HasFlag(ControlState.Pressed) && swallowTap)
                {
                    // Tapped is raised after the release; re-enable children once this input frame is done.
                    Dispatcher.GetCurrentThreadDispatcher().Invoke(() => swallowTap = false);
                }
            };
        }

        /// <summary>
        /// Occurs when <see cref="HorizontalOffset"/> or <see cref="VerticalOffset"/> changes.
        /// </summary>
        public event EventHandler? ScrollChanged;

        /// <summary>
        /// Gets <see cref="ContentControl.Content"/>'s full natural height, regardless of how much of it is
        /// currently visible - or, when <see cref="ContentControl.Content"/> implements
        /// <see cref="IVirtualizingScrollInfo"/>, its own reported estimate instead of a full measure.
        /// </summary>
        public float ExtentHeight => Content is IVirtualizingScrollInfo virtualizing ? virtualizing.ExtentHeight : Content?.Measure().Height ?? 0;

        /// <summary>
        /// Gets <see cref="ContentControl.Content"/>'s full natural width, regardless of how much of it is
        /// currently visible - or, when <see cref="ContentControl.Content"/> implements
        /// <see cref="IVirtualizingScrollInfo"/>, its own reported estimate instead of a full measure.
        /// </summary>
        public float ExtentWidth => Content is IVirtualizingScrollInfo virtualizing ? virtualizing.ExtentWidth : Content?.Measure().Width ?? 0;

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
        /// Gets or sets the axes this <see cref="ScrollViewer"/> pans along with touch and the middle mouse button.
        /// Defaults to <see cref="Controls.PanningMode.Auto"/>.
        /// </summary>
        /// <remarks>
        /// A pan claims a drag only along axes where the content can still move in the drag's direction, so a list that is
        /// already at its edge lets the gesture reach the next scroll area out. The left mouse button never pans.
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(PanningMode.Auto)]
        [RegisterReference]
        public PanningMode PanningMode
        {
            get => panningMode;
            set
            {
                if (SetProperty(ref panningMode, value))
                    inertia.Stop();
            }
        }

        /// <summary>
        /// Gets or sets how quickly a fling slows down, in this control's units per second squared. Defaults to <c>1500</c>;
        /// <c>0</c> disables inertia.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
        [Category("Behavior")]
        [DefaultValue(1500f)]
        [RegisterReference]
        public float PanningDeceleration
        {
            get => panningDeceleration;
            set
            {
                if (!float.IsFinite(value) || value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The deceleration must be a finite, non-negative number.");
                SetProperty(ref panningDeceleration, value);
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

        /// <summary>
        /// Stops a running fling immediately, leaving the offsets where they are.
        /// </summary>
        public void StopInertia() => inertia.Stop();

        /// <inheritdoc/>
        internal override Point RevealScreenRectangle(Rectangle target)
        {
            Rectangle viewport = GetViewportScreenBounds();
            if (viewport.Width <= 0 || viewport.Height <= 0 || ContentBounds.Width <= 0 || ContentBounds.Height <= 0)
                return Point.Empty;

            // Screen units per layout unit: the canvas scale and any LayoutScale above this viewer.
            float scaleX = viewport.Width / (float)ContentBounds.Width;
            float scaleY = viewport.Height / (float)ContentBounds.Height;
            float beforeX = HorizontalOffset;
            float beforeY = VerticalOffset;

            int dx = RevealDelta(target.Left, target.Right, viewport.Left, viewport.Right);
            int dy = RevealDelta(target.Top, target.Bottom, viewport.Top, viewport.Bottom);
            if (dx != 0)
                HorizontalOffset += dx / scaleX;
            if (dy != 0)
                VerticalOffset += dy / scaleY;

            return new Point((int)MathF.Round((HorizontalOffset - beforeX) * scaleX), (int)MathF.Round((VerticalOffset - beforeY) * scaleY));
        }

        /// <summary>Gets the visible content area (the viewport) in screen space.</summary>
        /// <returns>The viewport's screen rectangle, or an empty one while detached.</returns>
        internal Rectangle GetViewportScreenBounds()
        {
            if (Canvas == null)
                return Rectangle.Empty;

            var origin = new Vector2(ContentBounds.X - ActualBounds.X, ContentBounds.Y - ActualBounds.Y);
            Point topLeft = PointToScreen(origin);
            Point bottomRight = PointToScreen(origin + new Vector2(ContentBounds.Width, ContentBounds.Height));
            return Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }

        /// <inheritdoc/>
        protected internal override bool OnScroll(ScrollInfo info)
        {
            base.OnScroll(info);
            inertia.Stop();
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
        protected internal override DragAxes GetDragAxes(in DragClaimContext context)
        {
            pendingClaim = DragAxes.None;
            if (context.Kind is not (PointerKind.Touch or PointerKind.MouseMiddle))
                return DragAxes.None;

            DragAxes allowed = PanningMode switch
            {
                PanningMode.None => DragAxes.None,
                PanningMode.Vertical => DragAxes.Vertical,
                PanningMode.Horizontal => DragAxes.Horizontal,
                PanningMode.Both => DragAxes.Both,
                _ => (ExtentWidth > ViewportWidth ? DragAxes.Horizontal : DragAxes.None) | (ExtentHeight > ViewportHeight ? DragAxes.Vertical : DragAxes.None),
            };

            // Content follows the finger, so the offset moves opposite to the drag direction.
            if (allowed.HasFlag(DragAxes.Horizontal) && CanMove(horizontalOffset, ExtentWidth - ViewportWidth, -context.LocalDirection.X))
                pendingClaim |= DragAxes.Horizontal;
            if (allowed.HasFlag(DragAxes.Vertical) && CanMove(verticalOffset, ExtentHeight - ViewportHeight, -context.LocalDirection.Y))
                pendingClaim |= DragAxes.Vertical;
            return pendingClaim;
        }

        /// <inheritdoc/>
        protected internal override void OnDragStarted(Point screenPoint)
        {
            base.OnDragStarted(screenPoint);
            inertia.Stop();
            swallowTap = false;
            panAxes = pendingClaim;
            panAnchor = null;
        }

        /// <inheritdoc/>
        protected internal override void OnDragFling(Vector2 screenVelocity)
        {
            base.OnDragFling(screenVelocity);
            if (panningDeceleration <= 0)
                return;

            // The linear part of the screen-to-local transform maps the velocity through DPI, canvas zoom and RenderScale.
            Point origin = lastDragScreenPoint;
            Vector2 local = PointToLocal(new Point(origin.X + (int)screenVelocity.X, origin.Y + (int)screenVelocity.Y)) - PointToLocal(origin);
            Vector2 offsetVelocity = new(panAxes.HasFlag(DragAxes.Horizontal) ? -local.X : 0f, panAxes.HasFlag(DragAxes.Vertical) ? -local.Y : 0f);

            float speed = offsetVelocity.Length();
            if (speed < MinFlingSpeed)
                return;
            if (speed > MaxFlingSpeed)
            {
                offsetVelocity *= MaxFlingSpeed / speed;
                speed = MaxFlingSpeed;
            }

            float seconds = speed / panningDeceleration;
            inertia.Start(offsetVelocity * (seconds / 2f), TimeSpan.FromSeconds(seconds));
        }

        /// <inheritdoc/>
        protected internal override void OnDragPerforming(Point screenPoint)
        {
            base.OnDragPerforming(screenPoint);
            Vector2 local = PointToLocal(screenPoint);

            // The first position only anchors the pan, so the content doesn't jump by the drag threshold.
            if (panAnchor is { } anchor)
            {
                Vector2 moved = local - anchor;
                if (panAxes.HasFlag(DragAxes.Horizontal))
                    HorizontalOffset -= moved.X;
                if (panAxes.HasFlag(DragAxes.Vertical))
                    VerticalOffset -= moved.Y;
            }

            panAnchor = local;
            lastDragScreenPoint = screenPoint;
        }

        /// <inheritdoc/>
        protected internal override void OnDragEnded(Point screenPoint)
        {
            base.OnDragEnded(screenPoint);
            panAnchor = null;
            panAxes = DragAxes.None;
        }

        /// <inheritdoc/>
        protected override bool CanHitTestChildren() => !inertia.IsRunning && !swallowTap;

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            inertia.Stop();
            base.OnDetached();
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            EnsureVirtualizingSubscription();

            if (Content is IVirtualizingScrollInfo virtualizingContent)
            {
                // Virtualizing content lays itself out within the viewport directly (each realized item
                // positions itself via its own OnViewportChanged call) - it must never be grown to its own full
                // extent the way the non-virtualizing branch below does, or virtualization is defeated entirely.
                //
                // Chrome.Arrange must run BEFORE OnViewportChanged, not after: it's what cascades down and sets
                // Content's own ActualBounds/ContentBounds for this frame (Chrome is a Border whose Child is
                // Content - Border.ArrangeContent arranges its Child during this call). OnViewportChanged's
                // realize walk reads Content's ContentBounds to position each realized container - calling it
                // first would position everything against last frame's (or, on the very first layout pass ever,
                // a still-default/empty) bounds instead of this frame's real ones.
                HorizontalOffset = horizontalOffset;
                VerticalOffset = verticalOffset;

                Chrome.InvalidateArrange();
                Chrome.Arrange(ActualBounds);

                virtualizingContent.OnViewportChanged(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);
                return;
            }

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

        private static bool CanMove(float offset, float maxOffset, float offsetDirection)
        {
            if (maxOffset <= 0)
                return false;
            if (offsetDirection > 0)
                return offset < maxOffset;
            if (offsetDirection < 0)
                return offset > 0;
            return true;
        }

        private static int RevealDelta(int start, int end, int viewStart, int viewEnd)
        {
            if (end - start > viewEnd - viewStart || start < viewStart)
                return start - viewStart;
            return end > viewEnd ? end - viewEnd : 0;
        }

        private bool ApplyInertiaStep(Vector2 step)
        {
            float beforeHorizontal = horizontalOffset;
            float beforeVertical = verticalOffset;
            HorizontalOffset += step.X;
            VerticalOffset += step.Y;
            return horizontalOffset != beforeHorizontal || verticalOffset != beforeVertical;
        }

        private void UpdateContentOffset()
        {
            EnsureVirtualizingSubscription();

            if (Content is IVirtualizingScrollInfo virtualizingContent)
            {
                virtualizingContent.OnViewportChanged(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);
                return;
            }

            if (Content != null)
                Content.LayoutOffset = new Vector2(-HorizontalOffset, -VerticalOffset);
        }

        /// <summary>
        /// Keeps this control subscribed to whatever <see cref="ContentControl.Content"/> currently implements
        /// <see cref="IVirtualizingScrollInfo"/>, so a <see cref="IVirtualizingScrollInfo.VerticalOffsetCorrectionRequested"/> or
        /// <see cref="IVirtualizingScrollInfo.ScrollToVerticalOffsetRequested"/>
        /// it raises actually reaches <see cref="VerticalOffset"/> - the value the scrollbar/user read.
        /// </summary>
        private void EnsureVirtualizingSubscription()
        {
            if (ReferenceEquals(subscribedVirtualizingContent, Content))
                return;

            if (subscribedVirtualizingContent != null)
            {
                subscribedVirtualizingContent.VerticalOffsetCorrectionRequested -= OnVerticalOffsetCorrectionRequested;
                subscribedVirtualizingContent.ScrollToVerticalOffsetRequested -= OnScrollToVerticalOffsetRequested;
            }

            subscribedVirtualizingContent = Content as IVirtualizingScrollInfo;

            if (subscribedVirtualizingContent != null)
            {
                subscribedVirtualizingContent.VerticalOffsetCorrectionRequested += OnVerticalOffsetCorrectionRequested;
                subscribedVirtualizingContent.ScrollToVerticalOffsetRequested += OnScrollToVerticalOffsetRequested;
            }
        }

        private void OnVerticalOffsetCorrectionRequested(object? sender, float delta) => VerticalOffset += delta;

        private void OnScrollToVerticalOffsetRequested(object? sender, float offset) => VerticalOffset = offset;
    }
}
