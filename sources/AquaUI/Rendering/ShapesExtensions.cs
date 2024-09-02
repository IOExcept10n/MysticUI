// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Drawing;
using System.Numerics;
using AquaUI.Rendering.Brushes;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Provides extension methods for shapes drawing.
    /// </summary>
    public static class ShapesExtensions
    {
        private static readonly SimpleBrush White = new();

        /// <summary>
        /// Draws an arc.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="center">Center of an arc.</param>
        /// <param name="radius">Radius of the arc.</param>
        /// <param name="sides">Count of sides to make polygons.</param>
        /// <param name="color">Color of the lines.</param>
        /// <param name="startAngle">Angle of the arc beginning.</param>
        /// <param name="endAngle">Angle of the arc ending.</param>
        /// <param name="thickness">Thickness of arc stroke.</param>
        public static void DrawArc(this IRenderContext context, Vector2 center, float radius, int sides, Color color, float startAngle, float endAngle, float thickness = 1f)
        {
            using var arcMemory = MemoryPool<Vector2>.Shared.Rent(sides);
            var span = arcMemory.Memory.Span;
            CreateArc(span, radius, sides, startAngle, endAngle);
            DrawPolygon(context, center, span, color, thickness);

            static void CreateArc(Span<Vector2> points, float radius, int sides, float startAngle, float endAngle)
            {
                float max = Math.Max(endAngle - startAngle, 0);
                float step = max / sides;
                float theta = startAngle;
                FillArc(points, radius, sides, step, theta);
            }
        }

        /// <summary>
        /// Draws a circle.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="center">Center of the circle.</param>
        /// <param name="radius">Radius of the circle.</param>
        /// <param name="sides">Count of sides to make polygons.</param>
        /// <param name="color">Color of the circle.</param>
        /// <param name="thickness">Thickness of circle stroke.</param>
        public static void DrawCircle(this IRenderContext context, Vector2 center, float radius, int sides, Color color, float thickness = 1f)
        {
            using var circleMemory = MemoryPool<Vector2>.Shared.Rent(sides);
            var span = circleMemory.Memory.Span;
            CreateCircle(span, sides, radius);
            DrawPolygon(context, center, span, color, thickness);

            static void CreateCircle(Span<Vector2> points, int sides, float radius)
            {
                const float max = 2.0f * MathF.PI;
                float step = max / sides;
                FillArc(points, radius, sides, step);
            }
        }

        /// <summary>
        /// Draws a circle.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="x">X coordinate of the circle center.</param>
        /// <param name="y">Y coordinate of the circle center.</param>
        /// <param name="radius">Circle radius.</param>
        /// <param name="sides">Count of sides to make polygon.</param>
        /// <param name="color">Color of the circle stroke.</param>
        /// <param name="thickness">Thickness of the circle stroke.</param>
        public static void DrawCircle(this IRenderContext context, float x, float y, float radius, int sides, Color color, float thickness = 1f) =>
            DrawCircle(context, new(x, y), radius, sides, color, thickness);

        /// <summary>
        /// Draws a line.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="point">Point of the line beginning.</param>
        /// <param name="length">Length of the line to draw.</param>
        /// <param name="rotation">Angle to rotate line.</param>
        /// <param name="origin">Origin of the rotation to apply.</param>
        /// <param name="color">Color of the line.</param>
        /// <param name="thickness">Thickness of the line.</param>
        public static void DrawLine(this IRenderContext context, Vector2 point, float length, float rotation, Vector2 origin, Color color, float thickness = 1f)
        {
            Rectangle rect = new((int)point.X, (int)point.Y, (int)length, (int)thickness);
            context.Draw(White, rect, null, color, rotation, origin);
        }

        /// <summary>
        /// Draws a line between two points.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="point1">The first point of the line.</param>
        /// <param name="point2">The second point of the line.</param>
        /// <param name="color">Color to draw line with.</param>
        /// <param name="thickness">Thickness of the line.</param>
        public static void DrawLine(this IRenderContext context, Vector2 point1, Vector2 point2, Color color, float thickness = 1f)
        {
            // calculate the distance between the two vectors
            var distance = Vector2.Distance(point1, point2);

            // calculate the angle between the two vectors
            var angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);

            DrawLine(context, point1, distance, angle, Vector2.Zero, color, thickness);
        }

        /// <summary>
        /// Draws a line with specified edge positions.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="x1">X component of the first line edge coordinate.</param>
        /// <param name="y1">Y component of the first line edge coordinate.</param>
        /// <param name="x2">X component of the second line edge coordinate.</param>
        /// <param name="y2">Y component of the second line edge coordinate.</param>
        /// <param name="color">Color to draw line with.</param>
        /// <param name="thickness">Thickness of the line stroke.</param>
        public static void DrawLine(this IRenderContext context, float x1, float y1, float x2, float y2, Color color, float thickness = 1f) =>
            DrawLine(context, new(x1, y1), new(x2, y2), color, thickness);

        /// <summary>
        /// Draws a square point at the specified position. The center of the point will be at the <paramref name="location"/>.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="location">Position to draw point at.</param>
        /// <param name="color">Color to draw point with.</param>
        /// <param name="size">Size of the point to draw.</param>
        public static void DrawPoint(this IRenderContext context, Vector2 location, Color color, float size = 1f)
        {
            var offset = new Vector2(0.5f) - new Vector2(size * 0.5f);
            location += offset;
            context.Draw(White, new Rectangle((int)location.X, (int)location.Y, (int)size, (int)size), color);
        }

        /// <summary>
        /// Draws a polygon.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="offset">Value to offset all polygon points.</param>
        /// <param name="points">Set of points to draw a polygon.</param>
        /// <param name="color">Colors to draw lines with.</param>
        /// <param name="thickness">Thickness of lines to draw with.</param>
        public static void DrawPolygon(this IRenderContext context, Vector2 offset, Vector2[] points, Color color, float thickness = 1f) =>
            DrawPolygon(context, offset, points.AsSpan(), color, thickness);

        /// <summary>
        /// Draws a polygon.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="offset">Value to offset all polygon points.</param>
        /// <param name="points">Pointer to the set of points to draw a polygon.</param>
        /// <param name="color">Colors to draw lines with.</param>
        /// <param name="thickness">Thickness of lines to draw with.</param>
        public static void DrawPolygon(this IRenderContext context, Vector2 offset, ReadOnlySpan<Vector2> points, Color color, float thickness = 1f)
        {
            if (points.Length == 0)
                return;

            if (points.Length == 1)
            {
                context.DrawPoint(points[0], color, (int)thickness);
                return;
            }

            for (var i = 0; i < points.Length - 1; i++)
                DrawLine(context, points[i] + offset, points[i + 1] + offset, color, thickness);

            // Close the polygon
            DrawLine(context, points[^1] + offset, points[0] + offset, color, thickness);
        }

        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="rectangle">Rectangle to draw.</param>
        /// <param name="color">Color to draw rectangle with.</param>
        /// <param name="thickness">Thickness of the rectangle stroke.</param>
        public static void DrawRectangle(this IRenderContext context, Rectangle rectangle, Color color, float thickness = 1f)
        {
            var t = (int)thickness;

            // Top
            context.Draw(White, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, t), color);

            // Bottom
            context.Draw(White, new Rectangle(rectangle.X, rectangle.Bottom - t, rectangle.Width, t), color);

            // Left
            context.Draw(White, new Rectangle(rectangle.X, rectangle.Y, t, rectangle.Height), color);

            // Right
            context.Draw(White, new Rectangle(rectangle.Right - t, rectangle.Y, t, rectangle.Height), color);
        }

        /// <summary>
        /// Draws a rectangle.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="location">Coordinates of the top-left rectangle corner.</param>
        /// <param name="size">Size of the rectangle.</param>
        /// <param name="color">Color to draw rectangle with.</param>
        /// <param name="thickness">Thickness of the rectangle stroke.</param>
        public static void DrawRectangle(this IRenderContext context, Vector2 location, Vector2 size, Color color, float thickness = 1f) =>
            DrawRectangle(context, new Rectangle((int)location.X, (int)location.Y, (int)size.X, (int)size.Y), color, thickness);

        /// <summary>
        /// Draws a filled rectangle.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="destination">Rectangle to draw.</param>
        /// <param name="color">Color of the rectangle to fill.</param>
        public static void FillRectangle(this IRenderContext context, Rectangle destination, Color color) =>
            context.Draw(White, destination, color);

        /// <summary>
        /// Draws a filled rectangle.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="location">Coordinates of the upper-left side of the rectangle.</param>
        /// <param name="size">Size of the rectangle.</param>
        /// <param name="color">Color to apply.</param>
        public static void FillRectangle(this IRenderContext context, Vector2 location, Vector2 size, Color color) =>
            FillRectangle(context, new Rectangle((int)location.X, (int)location.Y, (int)size.X, (int)size.Y), color);

        /// <summary>
        /// Draws a filled rectangle.
        /// </summary>
        /// <param name="context">Instance of the <see cref="IRenderContext"/> to draw.</param>
        /// <param name="x">Position of the left rectangle side.</param>
        /// <param name="y">Position of the top rectangle side.</param>
        /// <param name="width">Width of the rectangle.</param>
        /// <param name="height">Height of the rectangle.</param>
        /// <param name="color">Color to apply for a rectangle.</param>
        public static void FillRectangle(this IRenderContext context, float x, float y, float width, float height, Color color) =>
            FillRectangle(context, new Rectangle((int)x, (int)y, (int)width, (int)height), color);

        private static void FillArc(Span<Vector2> points, float radius, int sides, float step, float theta = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                points[i] = new(radius * MathF.Cos(theta), radius * MathF.Sin(theta));
                theta += step;
            }
        }

        // TODO: make FillSegment and FillCircle methods
        private class SimpleBrush : IBrush
        {
            void IBrush.Draw<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer, Rectangle destination, Rectangle? source, Color color, float rotation, Vector2 origin, float depth)
            {
                var white = TTexture.GetWhite(renderer.GraphicsDevice);
                renderer.Draw(white, destination, source, color, rotation, origin, depth);
            }
        }
    }
}