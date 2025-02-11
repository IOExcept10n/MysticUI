// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Represents the solid brush with one color to draw.
    /// </summary>
    public class SolidColorBrush : IBrush
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SolidColorBrush"/> class.
        /// </summary>
        /// <param name="color">Color of the brush.</param>
        public SolidColorBrush(Color color)
        {
            Color = color;
        }

        /// <summary>
        /// Gets the color of the brush.
        /// </summary>
        public Color Color { get; }

        /// <inheritdoc/>
        public void Draw(IRenderContext context, in TextureRenderingOptions options)
        {
            var mixed = (Color.AsVector() * options.Color.AsVector()).AsColor();
            context.Draw(context.WhiteTexture, options with { Color = mixed });
        }
    }
}