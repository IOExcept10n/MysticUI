using System.ComponentModel;
using System.Drawing;
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

        /// <summary>
        /// Occurs when <see cref="SplitterPosition"/> changes.
        /// </summary>
        public event EventHandler? SplitterPositionChanged;

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
    }
}
