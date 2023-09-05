using Stride.Core.Mathematics;

namespace MysticUI
{
    /// <summary>
    /// An interface for image brushes which have the size to draw.
    /// </summary>
    public interface IImage : IBrush
    {
        /// <summary>
        /// Size of the image.
        /// </summary>
        public Point Size { get; }

        /// <summary>
        /// Tries to get an image source.
        /// </summary>
        public string? Source { get; }

        /// <summary>
        /// Clips and draws a part of the image into the render context.
        /// </summary>
        /// <param name="context">Context to draw into.</param>
        /// <param name="scissor">Maximal size of the image part to draw.</param>
        /// <param name="target">Target rectangle.</param>
        /// <param name="color">Color to draw with.</param>
        public void Draw(RenderContext context, Point scissor, Rectangle target, Color color);
    }
}