using FontStashSharp.RichText;
using MysticUI.Brushes.TextureBrushes;
using Stride.Core.Mathematics;

namespace MysticUI.Extensions
{
    /// <summary>
    /// Provides the decorative image class for the <see cref="IRenderable"/> interface.
    /// </summary>
    public class RenderableImage : IRenderable
    {
        /// <summary>
        /// An image to convert the image.
        /// </summary>
        public ImageBrush? Image { get; set; }

        /// <summary>
        /// The size of an image.
        /// </summary>
        public Point Size => (Image as IImage)?.Size ?? Point.Zero;

        /// <summary>
        /// Draws an image using the font rendering context.
        /// </summary>
        /// <param name="context">A context to draw with.</param>
        /// <param name="position">Position to draw in.</param>
        /// <param name="color">A color to apply.</param>
        public void Draw(FSRenderContext context, Vector2 position, Color color)
        {
            if (Image != null)
            {
                context.DrawImage(Image.Texture, Image.Bounds, position, Vector2.One, color);
            }
        }
    }
}