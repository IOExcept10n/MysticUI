using MysticUI.Extensions;
using Stride.Core.Mathematics;

namespace MysticUI.Brushes
{
    /// <summary>
    /// Provides a simple solid color drawing brush.
    /// </summary>
    [Serializable]
    public class SolidColorBrush : IBrush
    {
        /// <summary>
        /// A color to draw.
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// Creates an instance of the <see cref="SolidColorBrush"/> class.
        /// </summary>
        public SolidColorBrush()
        {
            Color = Color.Transparent;
        }

        /// <summary>
        /// Creates an instance of the <see cref="SolidColorBrush"/> class.
        /// </summary>
        /// <param name="color">A color to draw with..</param>
        public SolidColorBrush(Color color)
        {
            Color = color;
        }

        /// <summary>
        /// Creates an instance of the <see cref="SolidColorBrush"/> class.
        /// </summary>
        /// <param name="colorName">Name of the color to draw with.</param>
        public SolidColorBrush(string colorName)
        {
            Color = ColorTools.ParseColor(colorName);
        }

        /// <inheritdoc/>
        public void Draw(RenderContext context, Rectangle destination, Color color)
        {
            context.FillRectangle(destination, Color * color);
        }

        /// <summary>
        /// Gets the <see cref="SolidColorBrush"/> from its color.
        /// </summary>
        /// <param name="color">A color to draw.</param>
        public static implicit operator SolidColorBrush(Color color) => new(color);

        /// <summary>
        /// Gets the color of the given <see cref="SolidColorBrush"/>.
        /// </summary>
        /// <param name="color">Value to get color from.</param>
        public static explicit operator Color(SolidColorBrush color) => color.Color;
    }
}