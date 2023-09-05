using MysticUI.Extensions;
using Stride.Core.Mathematics;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a rectangle with the indication that fills according to <see cref="RangeBase.Value"/> change.
    /// </summary>
    public class ProgressBar : RangeBase
    {
        private float animationProgress;
        private float delayProgress;

        /// <summary>
        /// Speed of the <see cref="GlowRect"/> moving animation.
        /// </summary>
        /// <remarks>
        /// Default animation with the <see cref="AnimationSpeed"/> modifier set to <see langword="0"/> is performed within <see langword="3"/> seconds
        /// <see langword="2"/> of which are for the animation and <see langword="1"/> is for the delay between two animation iterations.
        /// </remarks>
        [Category("Animation")]
        public float AnimationSpeed { get; set; } = 1;

        /// <summary>
        /// Indication foreground image to show <see cref="ProgressBar"/> value.
        /// </summary>
        [Category("Appearance")]
        public IImage? Indicator { get; set; }

        /// <summary>
        /// Animation glowing rectangle image to display progress animation.
        /// </summary>
        [Category("Appearance")]
        public IImage? GlowRect { get; set; }

        /// <summary>
        /// Gets or sets the filling mode for the <see cref="Indicator"/>.
        /// </summary>
        public ProgressBarFillMode FillMode { get; set; }

        /// <summary>
        /// Gets or sets the value indicating if the <see cref="ProgressBar"/> should show generic progress without explicit values.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the <see cref="ProgressBar"/> ignores the value and shows always full-filled value, <see langword="false"/> otherwise.
        /// </value>
        public bool IsIntermediate { get; set; }

        /// <summary>
        /// Gets or sets the value indicating if the <see cref="ProgressBar"/> direction is reversed.
        /// </summary>
        public bool ReverseDirection { get; set; }

        /// <summary>
        /// Gets or sets the orientation of the <see cref="ProgressBar"/>
        /// </summary>
        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            if (Indicator == null) return;
            int animationSize = Orientation == Orientation.Horizontal ? ActualBounds.Width : ActualBounds.Height;
            // Draw indicator value.
            if (!IsIntermediate)
            {
                double delta = Maximum - Minimum;
                if (MathUtil.IsZero((float)delta)) return;
                double filledPart = Value / delta;
                if (MathUtil.IsZero((float)filledPart)) return;
                int partValue = animationSize = Orientation == Orientation.Horizontal ? (int)(filledPart * ActualBounds.Width) : (int)(filledPart * ActualBounds.Height);
                Rectangle fillArea = Orientation == Orientation.Horizontal ?
                    new(ActualBounds.X, ActualBounds.Y, partValue, ActualBounds.Height) :
                    new(ActualBounds.X, ActualBounds.Y, ActualBounds.Width, partValue);
                if (ReverseDirection)
                {
                    fillArea = new Point(fillArea.Width, fillArea.Height).Align(new(ActualBounds.Width, ActualBounds.Height),
                        Orientation == Orientation.Horizontal ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                        Orientation == Orientation.Vertical ? VerticalAlignment.Bottom : VerticalAlignment.Top);
                    fillArea.Offset(ActualBounds.Location);
                }
                Color foreground = GetCurrentForegroundColor();
                switch (FillMode)
                {
                    case ProgressBarFillMode.Part:
                        {
                            if (Orientation == Orientation.Horizontal)
                            {
                                Rectangle partRect = new(ActualBounds.X, ActualBounds.Y, Indicator.Size.X, ActualBounds.Height);
                                for (int i = 0; i < partValue; i += Indicator.Size.X)
                                {
                                    Indicator.Draw(context, partRect, foreground);
                                    partRect.X = i;
                                }
                            }
                            else
                            {
                                Rectangle partRect = new(ActualBounds.X, ActualBounds.Y, ActualBounds.Width, Indicator.Size.Y);
                                for (int i = 0; i < partValue; i += Indicator.Size.Y)
                                {
                                    Indicator.Draw(context, partRect, foreground);
                                    partRect.Y = i;
                                }
                            }
                            break;
                        }
                    case ProgressBarFillMode.Clip:
                        {
                            if (EnvironmentSettings.DebugOptions.DisableClipping)
                                goto case ProgressBarFillMode.Stretch;
                            Point areaScissor = Orientation == Orientation.Horizontal ?
                                new((int)(Indicator.Size.X * filledPart), fillArea.Height) :
                                new(Indicator.Size.X, (int)(Indicator.Size.Y * filledPart));
                            Indicator.Draw(context, areaScissor, fillArea, foreground);
                            break;
                        }
                    case ProgressBarFillMode.Stretch:
                        {
                            Indicator.Draw(context, fillArea, foreground);
                            break;
                        }
                }
            }
            // If the ProgressBar is intermediate, draw generic progress indicator parts.
            else
            {
                Color foreground = GetCurrentForegroundColor();
                if (FillMode == ProgressBarFillMode.Part)
                {
                    if (Orientation == Orientation.Horizontal)
                    {
                        Rectangle partRect = new(ActualBounds.X, ActualBounds.Y, Indicator.Size.X, ActualBounds.Height);
                        for (int i = 0; i < animationSize; i += Indicator.Size.X)
                        {
                            Indicator.Draw(context, partRect, foreground);
                            partRect.X = i;
                        }
                    }
                    else
                    {
                        Rectangle partRect = new(ActualBounds.X, ActualBounds.Y, ActualBounds.Width, Indicator.Size.Y);
                        for (int i = 0; i < animationSize; i += Indicator.Size.Y)
                        {
                            Indicator.Draw(context, partRect, foreground);
                            partRect.Y = i;
                        }
                    }
                }
                else
                {
                    Indicator.Draw(context, ActualBounds, foreground);
                }
            }
            // Update animation state
            if (GlowRect == null) return;
            // Default animation speed is 3 seconds so 2 of them are for animation and 1 for delay.
            float deltaTime = (float)DeltaTime.TotalSeconds * AnimationSpeed;
            animationProgress += deltaTime;
            // Don't draw animation when delay.
            if (animationProgress > 1)
            {
                delayProgress += deltaTime * 2;
                if (delayProgress > 1)
                {
                    animationProgress = 0;
                    delayProgress = 0;
                }
                return;
            }
            // Draw animation square
            int offset = (int)(animationSize * animationProgress);
            Rectangle glowRectArea;
            Point scissor;
            int scissorSize = animationSize - offset;
            if (Orientation == Orientation.Horizontal)
            {
                int width = Math.Min(GlowRect.Size.X, scissorSize);
                glowRectArea = ReverseDirection ?
                    new(ActualBounds.X + ActualBounds.Width - offset - width, ActualBounds.Y, width, ActualBounds.Height) :
                    new(ActualBounds.X + offset, ActualBounds.Y, width, ActualBounds.Height);
                scissor = new(scissorSize, ActualBounds.Height);
            }
            else
            {
                int height = Math.Min(GlowRect.Size.Y, scissorSize);
                glowRectArea = ReverseDirection ?
                    new(ActualBounds.X, ActualBounds.Y + ActualBounds.Height - offset - height, ActualBounds.Width, height) :
                    new(ActualBounds.X, ActualBounds.Y + offset, ActualBounds.Width, height);
                scissor = new(ActualBounds.Width, scissorSize);
            }
            glowRectArea = Rectangle.Intersect(glowRectArea, ActualBounds);
            GlowRect.Draw(context, scissor, glowRectArea, Color.White);
        }

        /// <summary>
        /// Defines the mode of <see cref="ProgressBar"/> <see cref="Indicator"/> render mode.
        /// </summary>
        public enum ProgressBarFillMode
        {
            /// <summary>
            /// The whole indicator will be stretch aligned into the render area.
            /// </summary>
            Stretch,

            /// <summary>
            /// The indicator should be a small rectangle to split it and draw by parts according to fill amount.
            /// </summary>
            Part,

            /// <summary>
            /// The indicator will be clipped without scaling to save proportions when drawing.
            /// </summary>
            Clip,
        }
    }
}