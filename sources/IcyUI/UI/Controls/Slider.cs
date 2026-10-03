// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data.Markup.Attributes;
using Icy.Input.Gestures;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A horizontal slider - drag the thumb (or tap the track) to set <see cref="Value"/> between
    /// <see cref="Minimum"/> and <see cref="Maximum"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Thumb-grab is via <see cref="UIElement.OnDragStarted(Point)"/>/<see cref="UIElement.OnDragPerforming(Point)"/> -
    /// Canvas hit-tests once at drag-start and routes the rest of the sequence to whatever was hit there,
    /// regardless of where the cursor moves next (see <see cref="UI.Canvas"/>'s drag routing), so dragging below/above
    /// the track still works once a drag has started on the slider.
    /// </para>
    /// <para>
    /// Template-safe via a named template part: a <see cref="Control.Template"/> that declares an element named
    /// <c>PART_Thumb</c> (of any type assignable to <see cref="UI.Border"/>) has its drag logic wired to that
    /// element instead of the built-in default thumb - see <see cref="OnApplyTemplate"/>. A template that declares
    /// none still applies (the default thumb keeps working, just detached from what's visually shown - dragging
    /// silently does nothing until the template provides a real <c>PART_Thumb</c>).
    /// </para>
    /// </remarks>
    public class Slider : Control
    {
        private const int ThumbSize = 16;

        private readonly Border defaultThumb;
        private Border thumb;
        private float maximum = 100;
        private float minimum;
        private bool isDragging;
        private float sliderValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Slider"/> class.
        /// </summary>
        public Slider()
        {
            IsFocusable = true;
            defaultThumb = thumb = new Border
            {
                Width = ThumbSize,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Color.White),
            };

            // Safe here, at construction: Chrome is always the default Border until/unless Template is set later,
            // in which case OnApplyTemplate re-wires (or re-attaches) thumb appropriately.
            ((Border)Chrome).Child = thumb;
        }

        /// <summary>
        /// Occurs when <see cref="Value"/> changes.
        /// </summary>
        public event EventHandler? ValueChanged;

        /// <summary>
        /// Gets or sets the brush used to paint the thumb - the currently-live one (the default built-in thumb, or
        /// a <see cref="Control.Template"/>'s own <c>PART_Thumb</c> once <see cref="OnApplyTemplate"/> has repointed
        /// <c>thumb</c> to it).
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush ThumbBrush
        {
            get => thumb.Background;
            set => thumb.Background = value;
        }

        /// <summary>
        /// Gets or sets the value <see cref="Value"/> can reach at the right end of the track.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(100f)]
        [RegisterReference]
        [AffectsArrange]
        public float Maximum
        {
            get => maximum;
            set
            {
                if (SetProperty(ref maximum, value))
                {
                    ClampValueToRange();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the value <see cref="Value"/> can reach at the left end of the track.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0f)]
        [RegisterReference]
        [AffectsArrange]
        public float Minimum
        {
            get => minimum;
            set
            {
                if (SetProperty(ref minimum, value))
                {
                    ClampValueToRange();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the current value, clamped to <see cref="Minimum"/>/<see cref="Maximum"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0f)]
        [RegisterReference]
        [AffectsArrange]
        public float Value
        {
            get => sliderValue;
            set
            {
                if (SetProperty(ref sliderValue, float.Clamp(value, Minimum, Maximum)))
                {
                    InvalidateArrange();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private float Ratio
        {
            get
            {
                float range = Maximum - Minimum;
                return range > 0 ? float.Clamp((Value - Minimum) / range, 0, 1) : 0;
            }
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
                UpdateValueFromPoint(screenPoint);
        }

        /// <inheritdoc/>
        /// <remarks>A <see cref="Slider"/> is horizontal: it claims horizontal touch and left-mouse drags.</remarks>
        protected internal override DragAxes GetDragAxes(in DragClaimContext context) =>
            context.Kind is PointerKind.Touch or PointerKind.MouseLeft ? DragAxes.Horizontal : DragAxes.None;

        /// <inheritdoc/>
        protected internal override void OnDragStarted(Point screenPoint)
        {
            base.OnDragStarted(screenPoint);
            isDragging = true;
            UpdateValueFromPoint(screenPoint);
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            float roaming = Math.Max(0, ContentBounds.Width - BorderThickness.Width - ThumbSize);
            thumb.Margin = new Thickness((int)(roaming * Ratio), 0, 0, 0);
            base.ArrangeContent();
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            // Like ProgressBar, the track's natural size isn't derived from content: the thumb has an explicit
            // Width but relies on VerticalAlignment.Stretch for its height, which (see UIElement.Measure) only
            // applies during Arrange, not Measure - an empty thumb Border would otherwise measure to zero height,
            // collapsing the whole slider to nothing whenever a caller doesn't set an explicit Height.
            return new(120, ThumbSize);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Repoints <c>thumb</c> - the element <see cref="ArrangeContent"/>/<see cref="UpdateValueFromPoint"/>
        /// already manipulate by reference - to whichever <see cref="UI.Border"/> is actually live right now:
        /// <see cref="Control.Template"/>'s own <c>PART_Thumb</c> when one is set and provides it, or the built-in
        /// <c>defaultThumb</c> (re-attached to the fresh default Chrome) otherwise.
        /// </remarks>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (Template != null && GetTemplateChild<Border>("PART_Thumb") is { } part)
            {
                thumb = part;
                return;
            }

            thumb = defaultThumb;
            if (Template == null)
                ((Border)Chrome).Child = thumb;
        }

        private void ClampValueToRange()
        {
            float clamped = float.Clamp(sliderValue, Minimum, Maximum);
            if (clamped != sliderValue)
            {
                SetProperty(ref sliderValue, clamped, nameof(Value));
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateValueFromPoint(Point screenPoint)
        {
            Vector2 local = PointToLocal(screenPoint);
            float roaming = Math.Max(1, ContentBounds.Width - BorderThickness.Width - ThumbSize);
            float insetX = Padding.Left + BorderThickness.Left;
            float ratio = float.Clamp((local.X - insetX - (ThumbSize / 2f)) / roaming, 0, 1);
            Value = Minimum + (ratio * (Maximum - Minimum));
        }
    }
}
