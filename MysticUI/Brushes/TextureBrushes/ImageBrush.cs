using MysticUI.Extensions;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace MysticUI.Brushes.TextureBrushes
{
    /// <summary>
    /// Represents a simple texture image brush.
    /// </summary>
    [Serializable]
    public class ImageBrush : IImage
    {
        private Texture texture = null!;

        /// <summary>
        /// Bounds of used image.
        /// </summary>
        public Rectangle Bounds { get; set; }

        /// <summary>
        /// Size of the image.
        /// </summary>
        public Point Size => new(Bounds.Width, Bounds.Height);

        /// <summary>
        /// Additional color to apply to a texture.
        /// </summary>
        public Color ColorModifier { get; set; } = Color.White;

        /// <summary>
        /// Image texture.
        /// </summary>
        public Texture Texture
        {
            get => texture;
            set
            {
                texture = value;
                OnTextureChanged();
            }
        }

        /// <summary>
        /// Source of a texture if available.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Serialization constructor. For internal use only.
        /// </summary>
        public ImageBrush()
        {
            Texture = null!;
        }

        /// <summary>
        /// Creates new <see cref="ImageBrush"/> without bound limiters.
        /// </summary>
        /// <param name="texture">Image texture.</param>
        public ImageBrush(Texture texture) : this(texture, new(0, 0, texture.Width, texture.Height))
        {
        }

        /// <summary>
        /// Creates new <see cref="ImageBrush"/> with bounds.
        /// </summary>
        /// <param name="texture">Texture image.</param>
        /// <param name="bounds">Bounds.</param>
        public ImageBrush(Texture texture, Rectangle bounds)
        {
            Texture = texture;
            Bounds = bounds;
        }

        /// <summary>
        /// Creates new <see cref="ImageBrush"/> from other <seealso cref="ImageBrush"/>.
        /// </summary>
        /// <param name="other">Other instance.</param>
        /// <param name="newBounds">New bounds to apply to the texture.</param>
        public ImageBrush(ImageBrush other, Rectangle newBounds)
        {
            Texture = other.Texture;
            Bounds = newBounds;
        }

        /// <inheritdoc/>
        public virtual void Draw(RenderContext context, Rectangle destination, Color color)
        {
            context.Draw(Texture, destination, Bounds, ColorModifier * color);
        }

        /// <inheritdoc/>
        public virtual void Draw(RenderContext context, Point scissor, Rectangle destination, Color color)
        {
            context.Draw(Texture, destination, Bounds.Clip(scissor), color);
        }

        /// <summary>
        /// Handles the change of the texture.
        /// </summary>
        protected virtual void OnTextureChanged()
        {
        }

        /// <summary>
        /// Creates an <see cref="ImageBrush"/> from given texture.
        /// </summary>
        /// <param name="texture">Texture to create an image brush.</param>
        public static implicit operator ImageBrush(Texture texture) => new(texture);

        /// <summary>
        /// Gets a texture from the <see cref="ImageBrush"/>.
        /// </summary>
        /// <param name="brush">Instance to get texture from.</param>
        public static explicit operator Texture(ImageBrush brush) => brush.Texture;
    }
}