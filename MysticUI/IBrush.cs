using MysticUI.Brushes;
using MysticUI.Brushes.TextureBrushes;
using Stride.Core.Mathematics;
using System.Xml.Serialization;

namespace MysticUI
{
    /// <summary>
    /// An interface for all drawable brushes for the UI elements.
    /// </summary>
    [XmlInclude(typeof(SolidColorBrush))]
    [XmlInclude(typeof(ImageBrush))]
    public interface IBrush
    {
        /// <summary>
        /// Draws a brush into a render surface.
        /// </summary>
        /// <param name="context">Context to render with.</param>
        /// <param name="destination">Destination to draw into.</param>
        /// <param name="color">Color to apply to brush.</param>
        public void Draw(RenderContext context, Rectangle destination, Color color);
    }

    /// <summary>
    /// Provides the extension methods for drawing brushes.
    /// </summary>
    public static class DrawExtensions
    {
        /// <summary>
        /// Draws a brush with white color.
        /// </summary>
        /// <param name="brush">Brush to draw.</param>
        /// <param name="context">Context to render.</param>
        /// <param name="destination">Drawing destination.</param>
        public static void Draw(this IBrush brush, RenderContext context, Rectangle destination) =>
            brush.Draw(context, destination, Color.White);

        /// <summary>
        /// Draws an image with apply of clipping if the <paramref name="image"/> size is more than the <paramref name="destination"/> value.
        /// </summary>
        /// <param name="image">An image to draw.</param>
        /// <param name="context">A context to render into.</param>
        /// <param name="destination">Destination rectangle.</param>
        public static void DrawClipped(this IImage image, RenderContext context, Rectangle destination) =>
            image.Draw(context, new(destination.Width, destination.Height), destination, Color.White);
    }
}