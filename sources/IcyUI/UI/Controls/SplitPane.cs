using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data.Markup.Attributes;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Splits a region into two resizable areas (<see cref="First"/>/<see cref="Second"/>) separated by a
    /// draggable divider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Divider-grab is via <see cref="UIElement.OnDragStarted(Point)"/>/<see cref="UIElement.OnDragPerforming(Point)"/> -
    /// the same single-target "hold and move the pointer" mechanism <see cref="Slider"/>'s thumb uses, not the
    /// cross-container <see cref="Icy.Input.DragDrop.IDragSource"/>/<see cref="Icy.Input.DragDrop.IDropTarget"/>
    /// framework - a resize gesture has no drop target elsewhere.
    /// </para>
    /// <para>
    /// Exactly two panes and one <see cref="Orientation"/> per instance - a 3+ region layout is built by nesting
    /// <see cref="SplitPane"/>s, not a native N-pane mode.
    /// </para>
    /// <para>
    /// Template-safe via a named template part: a <see cref="Control.Template"/> that declares an element named
    /// <c>PART_Divider</c> (of any type assignable to <see cref="UI.Border"/>) has drag logic and
    /// <see cref="DividerBrush"/> wired to it instead of the built-in default divider - see <see cref="OnApplyTemplate"/>.
    /// </para>
    /// </remarks>
    public class SplitPane : Control
    {
        private const int DefaultDividerSize = 6;
        private const int DividerLineThickness = 2;

        private readonly Border defaultDivider;
        private readonly Border defaultDividerLine;
        private Border divider;
        private Border dividerVisual;
        private UIElement? first;
        private UIElement? second;
        private Orientation orientation = Orientation.Horizontal;
        private float splitterPosition = 0.5f;
        private float minFirstSize;
        private float minSecondSize;
        private float dividerSize = DefaultDividerSize;
        private bool isDragging;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitPane"/> class.
        /// </summary>
        public SplitPane()
        {
            defaultDividerLine = new Border
            {
                Background = new SolidColorBrush(Color.White),
            };
            defaultDivider = new Border
            {
                Background = new SolidColorBrush(Color.Transparent),
                Child = defaultDividerLine,
            };
            divider = defaultDivider;
            dividerVisual = defaultDividerLine;

            // Safe here, at construction: Chrome is always the default Border until/unless Template is set later,
            // in which case OnApplyTemplate re-wires (or re-attaches) divider/dividerVisual appropriately.
            ((Border)Chrome).Child = divider;
            ApplyDividerOrientation();
        }

        /// <summary>
        /// Occurs when <see cref="SplitterPosition"/> changes.
        /// </summary>
        public event EventHandler? SplitterPositionChanged;

        /// <summary>
        /// Gets or sets the axis <see cref="First"/>/<see cref="Second"/> are split along.
        /// </summary>
        /// <remarks>
        /// Matches <see cref="StackPanel.Orientation"/>'s own semantics: <see cref="Orientation.Horizontal"/>
        /// places <see cref="First"/>/<see cref="Second"/> side-by-side (a vertical-line divider);
        /// <see cref="Orientation.Vertical"/> stacks them (a horizontal-line divider).
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(Orientation.Horizontal)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public Orientation Orientation
        {
            get => orientation;
            set
            {
                if (SetProperty(ref orientation, value))
                {
                    ApplyDividerOrientation();
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the element displayed before the divider (left, when <see cref="Orientation"/> is
        /// <see cref="Orientation.Horizontal"/>; top, when <see cref="Orientation.Vertical"/>).
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? First
        {
            get => first;
            set
            {
                if (first == value)
                    return;
                if (first != null)
                {
                    first.Parent = null;
                    first.Canvas = null;
                }

                first = value;
                if (first != null)
                {
                    first.Parent = this;
                    first.Canvas = Canvas;
                }

                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets the element displayed after the divider (right, when <see cref="Orientation"/> is
        /// <see cref="Orientation.Horizontal"/>; bottom, when <see cref="Orientation.Vertical"/>).
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Second
        {
            get => second;
            set
            {
                if (second == value)
                    return;
                if (second != null)
                {
                    second.Parent = null;
                    second.Canvas = null;
                }

                second = value;
                if (second != null)
                {
                    second.Parent = this;
                    second.Canvas = Canvas;
                }

                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets the fraction (0-1) of available space, along <see cref="Orientation"/>'s axis, allocated
        /// to <see cref="First"/>.
        /// </summary>
        /// <remarks>
        /// Clamped to <c>[0,1]</c> only - <see cref="MinFirstSize"/>/<see cref="MinSecondSize"/> are enforced
        /// separately, at arrange/drag time, since they depend on the control's current size (see
        /// <see cref="ArrangeContent"/>).
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(0.5f)]
        [RegisterReference]
        [AffectsArrange]
        public float SplitterPosition
        {
            get => splitterPosition;
            set
            {
                if (SetProperty(ref splitterPosition, float.Clamp(value, 0, 1)))
                {
                    InvalidateArrange();
                    SplitterPositionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum size, in pixels along <see cref="Orientation"/>'s axis, <see cref="First"/>
        /// keeps regardless of <see cref="SplitterPosition"/>.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(0f)]
        [RegisterReference]
        [AffectsArrange]
        public float MinFirstSize
        {
            get => minFirstSize;
            set
            {
                if (SetProperty(ref minFirstSize, value))
                    InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the minimum size, in pixels along <see cref="Orientation"/>'s axis, <see cref="Second"/>
        /// keeps regardless of <see cref="SplitterPosition"/>.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(0f)]
        [RegisterReference]
        [AffectsArrange]
        public float MinSecondSize
        {
            get => minSecondSize;
            set
            {
                if (SetProperty(ref minSecondSize, value))
                    InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the divider's hit-test band width, in pixels along <see cref="Orientation"/>'s axis - the
        /// grabbable area is this wide even though the visible line drawn inside it is thinner (see
        /// <see cref="DividerBrush"/>).
        /// </summary>
        [Category("Layout")]
        [DefaultValue((float)DefaultDividerSize)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public float DividerSize
        {
            get => dividerSize;
            set
            {
                if (SetProperty(ref dividerSize, value))
                {
                    ApplyDividerOrientation();
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the brush used to paint the divider's visible line - the currently-live one (the default
        /// built-in line, or a <see cref="Control.Template"/>'s own <c>PART_Divider</c> once
        /// <see cref="OnApplyTemplate"/> has repointed it).
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush DividerBrush
        {
            get => dividerVisual.Background;
            set => dividerVisual.Background = value;
        }

        /// <inheritdoc/>
        protected internal override void OnDragEnded(Point screenPoint)
        {
            base.OnDragEnded(screenPoint);
            isDragging = false;
        }

        /// <inheritdoc/>
        protected internal override void OnDragPerforming(Point screenPoint)
        {
            base.OnDragPerforming(screenPoint);
            if (isDragging)
                UpdatePositionFromPoint(screenPoint);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Only starts tracking the drag when <paramref name="screenPoint"/> actually landed on the divider band -
        /// <see cref="UI.Canvas"/> bubbles a drag gesture to every ancestor of whatever was hit, so without this
        /// check a press anywhere inside <see cref="First"/>/<see cref="Second"/> (including a nested
        /// <see cref="SplitPane"/>'s own divider) would resize this <see cref="SplitPane"/> too.
        /// </remarks>
        protected internal override void OnDragStarted(Point screenPoint)
        {
            base.OnDragStarted(screenPoint);
            isDragging = IsPointOnDivider(screenPoint);
            if (isDragging)
                UpdatePositionFromPoint(screenPoint);
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            yield return Chrome;
            if (First != null)
                yield return First;
            if (Second != null)
                yield return Second;
        }

        /// <inheritdoc/>
        protected override void OnRender(Icy.Rendering.IRenderContext context)
        {
            Chrome.Draw(context);
            First?.Draw(context);
            Second?.Draw(context);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Repoints <c>divider</c>/<c>dividerVisual</c> - the elements <see cref="ArrangeContent"/>/
        /// <see cref="UpdatePositionFromPoint"/> already manipulate by reference - to whichever <see cref="UI.Border"/>
        /// is actually live right now: <see cref="Control.Template"/>'s own <c>PART_Divider</c> when one is set,
        /// falling back to the built-in default divider otherwise (mirrors <see cref="Slider.OnApplyTemplate"/> exactly).
        /// A template's <c>PART_Divider</c> with no nested <see cref="UI.Border.Child"/> of its own gets
        /// <see cref="DividerBrush"/> routed directly onto it instead of a nested line - graceful degradation, matching
        /// how a template with no <c>PART_Divider</c> at all still applies (just with drag/position wired to nothing
        /// the template actually shows, exactly as an untemplated-part <see cref="Slider"/> already behaves).
        /// </remarks>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (Template != null && GetTemplateChild<Border>("PART_Divider") is { } part)
            {
                divider = part;
                dividerVisual = part.Child as Border ?? part;
                ApplyDividerOrientation();
                return;
            }

            divider = defaultDivider;
            dividerVisual = defaultDividerLine;
            if (Template == null)
                ((Border)Chrome).Child = divider;
            ApplyDividerOrientation();
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            Rectangle content = ContentBounds;
            bool horizontal = Orientation == Orientation.Horizontal;
            float available = horizontal ? content.Width : content.Height;
            int dividerOffset = ResolveDividerOffset(available);

            if (horizontal)
            {
                First?.Arrange(new Rectangle(content.X, content.Y, dividerOffset, content.Height));
                divider.Margin = new Thickness(dividerOffset, 0, 0, 0);
                Second?.Arrange(new Rectangle(
                    content.X + dividerOffset + (int)DividerSize,
                    content.Y,
                    Math.Max(0, content.Width - dividerOffset - (int)DividerSize),
                    content.Height));
            }
            else
            {
                First?.Arrange(new Rectangle(content.X, content.Y, content.Width, dividerOffset));
                divider.Margin = new Thickness(0, dividerOffset, 0, 0);
                Second?.Arrange(new Rectangle(
                    content.X,
                    content.Y + dividerOffset + (int)DividerSize,
                    content.Width,
                    Math.Max(0, content.Height - dividerOffset - (int)DividerSize)));
            }

            Chrome.Arrange(ActualBounds);
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            Size firstSize = First?.Measure() ?? Size.Empty;
            Size secondSize = Second?.Measure() ?? Size.Empty;
            bool horizontal = Orientation == Orientation.Horizontal;

            return horizontal
                ? new Size((int)(firstSize.Width + secondSize.Width + DividerSize), Math.Max(firstSize.Height, secondSize.Height))
                : new Size(Math.Max(firstSize.Width, secondSize.Width), (int)(firstSize.Height + secondSize.Height + DividerSize));
        }

        private void UpdatePositionFromPoint(Point screenPoint)
        {
            Vector2 local = PointToLocal(screenPoint);
            bool horizontal = Orientation == Orientation.Horizontal;
            float inset = horizontal ? Padding.Left + BorderThickness.Left : Padding.Top + BorderThickness.Top;
            float localAxis = horizontal ? local.X : local.Y;
            Rectangle content = ContentBounds;
            float available = horizontal ? content.Width : content.Height;
            float roaming = Math.Max(1, available - DividerSize);

            float minRatio = float.Clamp(MinFirstSize / roaming, 0, 1);
            float maxRatio = float.Clamp(1 - (MinSecondSize / roaming), 0, 1);
            if (maxRatio < minRatio)
                (minRatio, maxRatio) = ((minRatio + maxRatio) / 2f, (minRatio + maxRatio) / 2f);

            // Centers the divider under the grab point (matches Slider.UpdateValueFromPoint's own
            // ThumbSize/2f offset) rather than snapping so the divider's leading edge aligns with the pointer.
            float rawRatio = (localAxis - inset - (DividerSize / 2f)) / roaming;
            SplitterPosition = float.Clamp(rawRatio, minRatio, maxRatio);
        }

        /// <summary>
        /// Determines whether <paramref name="screenPoint"/> falls within the divider's grabbable band.
        /// </summary>
        /// <param name="screenPoint">The point to test, in screen space.</param>
        /// <returns><see langword="true"/> when the point lands on the divider band; otherwise, <see langword="false"/>.</returns>
        private bool IsPointOnDivider(Point screenPoint)
        {
            Vector2 local = PointToLocal(screenPoint);
            bool horizontal = Orientation == Orientation.Horizontal;
            float inset = horizontal ? Padding.Left + BorderThickness.Left : Padding.Top + BorderThickness.Top;
            float axis = (horizontal ? local.X : local.Y) - inset;
            Rectangle content = ContentBounds;
            float dividerOffset = ResolveDividerOffset(horizontal ? content.Width : content.Height);
            return axis >= dividerOffset && axis <= dividerOffset + DividerSize;
        }

        private void ApplyDividerOrientation()
        {
            bool horizontal = Orientation == Orientation.Horizontal;

            divider.HorizontalAlignment = horizontal ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            divider.VerticalAlignment = horizontal ? VerticalAlignment.Stretch : VerticalAlignment.Top;
            divider.Width = horizontal ? DividerSize : float.NaN;
            divider.Height = horizontal ? float.NaN : DividerSize;

            if (dividerVisual == defaultDividerLine)
            {
                dividerVisual.HorizontalAlignment = horizontal ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
                dividerVisual.VerticalAlignment = horizontal ? VerticalAlignment.Stretch : VerticalAlignment.Center;
                dividerVisual.Width = horizontal ? DividerLineThickness : float.NaN;
                dividerVisual.Height = horizontal ? float.NaN : DividerLineThickness;
            }
        }

        /// <summary>
        /// Computes the divider's pixel offset from the start of <paramref name="available"/>, applying
        /// <see cref="SplitterPosition"/> and clamping to <see cref="MinFirstSize"/>/<see cref="MinSecondSize"/>.
        /// </summary>
        /// <param name="available">The full content extent along <see cref="Orientation"/>'s axis.</param>
        /// <returns>
        /// The pixel offset where <see cref="First"/> ends and the divider begins. Degrades gracefully (splits the
        /// midpoint of whatever range remains) when <see cref="MinFirstSize"/> + <see cref="MinSecondSize"/> +
        /// <see cref="DividerSize"/> exceeds <paramref name="available"/>, rather than throwing.
        /// </returns>
        private int ResolveDividerOffset(float available)
        {
            float roaming = Math.Max(0, available - DividerSize);
            float rawPos = SplitterPosition * roaming;
            float minPos = MinFirstSize;
            float maxPos = roaming - MinSecondSize;

            float pos = maxPos >= minPos ? float.Clamp(rawPos, minPos, maxPos) : (minPos + maxPos) / 2f;
            return (int)float.Clamp(pos, 0, roaming);
        }
    }
}
