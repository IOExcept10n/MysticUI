// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Displays <see cref="Value"/> as a proportion of <see cref="Minimum"/>/<see cref="Maximum"/>, drawn as a
    /// track (this control's inherited <see cref="Control.Background"/>/<see cref="Control.BorderBrush"/>) with a
    /// <see cref="FillBrush"/>-colored fill growing from the left.
    /// </summary>
    public class ProgressBar : Control
    {
        private readonly Border fill;
        private float maximum = 100;
        private float minimum;
        private float progressValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressBar"/> class.
        /// </summary>
        public ProgressBar()
        {
            fill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Color.DodgerBlue),
            };
            Chrome.Child = fill;
        }

        /// <summary>
        /// Gets or sets the brush used to paint the filled portion of the bar.
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush FillBrush
        {
            get => fill.Background;
            set => fill.Background = value;
        }

        /// <summary>
        /// Gets or sets the value <see cref="Value"/> represents as a full bar.
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
        /// Gets or sets the value <see cref="Value"/> represents as an empty bar.
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
        /// Gets or sets the current progress value, clamped to <see cref="Minimum"/>/<see cref="Maximum"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0f)]
        [RegisterReference]
        [AffectsArrange]
        public float Value
        {
            get => progressValue;
            set
            {
                if (SetProperty(ref progressValue, float.Clamp(value, Minimum, Maximum)))
                {
                    InvalidateArrange();
                }
            }
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            float range = Maximum - Minimum;
            float ratio = range > 0 ? float.Clamp((Value - Minimum) / range, 0, 1) : 0;
            int available = Math.Max(0, ContentBounds.Width - BorderThickness.Width);

            // Setting Width cascades InvalidateMeasure() up through Chrome to this control (and beyond) every time
            // the value changes, even though this control's own MeasureContent() below ignores it entirely - a
            // known, bounded cost (flag propagation only, not a real recompute) rather than a correctness issue.
            fill.Width = available * ratio;

            base.ArrangeContent();
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            // A progress bar's fill width is a function of its own final size, not the other way around, so
            // there's no meaningful content-derived natural size here - default to a reasonable fixed footprint.
            return new(120, 16);
        }

        private void ClampValueToRange()
        {
            float clamped = float.Clamp(progressValue, Minimum, Maximum);
            if (clamped != progressValue)
            {
                SetProperty(ref progressValue, clamped, nameof(Value));
            }
        }
    }
}
