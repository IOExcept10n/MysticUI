// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using AquaUI.Rendering.Brushes;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Provides extension methods for the <see cref="IRenderContext"/> implementations.
    /// </summary>
    public static class RenderContextExtensions
    {
        /// <inheritdoc cref="IRenderContext.Draw(IBrush, Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public static void Draw(this IRenderContext context, IImage image, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin) =>
            context.Draw(image, destinationRectangle, sourceRectangle, color, rotation, origin, 0.0f);

        /// <inheritdoc cref="IRenderContext.Draw(IBrush, Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public static void Draw(this IRenderContext context, IImage image, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color) =>
            Draw(context, image, destinationRectangle, sourceRectangle, color, 0.0f, Vector2.Zero);

        /// <inheritdoc cref="IRenderContext.Draw(IBrush, Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public static void Draw(this IRenderContext context, IBrush brush, Rectangle destination, Color color) =>
            context.Draw(brush, destination, null, color, 0, Vector2.Zero);

        /// <inheritdoc cref="IRenderContext.DrawString(IFont, string, Vector2, float, Color, Vector2, float, Vector2)"/>
        public static void DrawString(this IRenderContext context, IFont font, string text, Vector2 position, Color color, Vector2 scale, float layerDepth = 0.0f) =>
            context.DrawString(font, text, position, layerDepth, color, scale, 0, Vector2.Zero);

        /// <inheritdoc cref="IRenderContext.DrawString(IFont, string, Vector2, float, Color, Vector2, float, Vector2)"/>
        public static void DrawString(this IRenderContext context, IFont font, string text, Vector2 position, Color color, float layerDepth = 0.0f) =>
            context.DrawString(font, text, position, layerDepth, color, Vector2.One, 0, Vector2.Zero);
    }
}