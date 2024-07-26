using AquaUI.Rendering.Brushes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Provides extension methods for the <see cref="IRenderContext"/> implementations.
    /// </summary>
    public static class RenderContextExtensions
    {
        /// <inheritdoc cref="IRenderContext.Draw(IBrush, Rectangle, Rectangle?, Color, float, float)"/>
        public static void Draw(this IRenderContext context, IImage image, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation) =>
            context.Draw(image, destinationRectangle, sourceRectangle, color, rotation, 0.0f);

        /// <inheritdoc cref="IRenderContext.Draw(IBrush, Rectangle, Rectangle?, Color, float, float)"/>
        public static void Draw(this IRenderContext context, IImage image, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color) =>
            Draw(context, image, destinationRectangle, destinationRectangle, color, 0.0f);

        /// <inheritdoc cref="IRenderContext.Draw(IBrush, Rectangle, Rectangle?, Color, float, float)"/>
        public static void Draw(this IRenderContext context, IBrush brush, Rectangle destination, Color color) =>
            context.Draw(brush, destination, null, color, 0);

        /// <inheritdoc cref="IRenderContext.DrawString(IFont, string, Vector2, Color, Vector2, float, float)"/>
        public static void DrawString(this IRenderContext context, IFont font, string text, Vector2 position, Color color, Vector2 scale, float layerDepth = 0.0f) =>
            context.DrawString(font, text, position, color, scale, 0, layerDepth);

        /// <inheritdoc cref="IRenderContext.DrawString(IFont, string, Vector2, Color, Vector2, float, float)"/>
        public static void DrawString(this IRenderContext context, IFont font, string text, Vector2 position, Color color, float layerDepth = 0.0f) =>
            context.DrawString(font, text, position, color, Vector2.One, 0, layerDepth);
    }
}
