// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Represents a simple brush for textures rendering.
    /// </summary>
    public class ImageBrush : IImage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush"/> class.
        /// </summary>
        /// <param name="source">Texture instance to use.</param>
        /// <param name="area">Area of the texture to draw.</param>
        public ImageBrush(ITexture source, Rectangle area)
        {
            Source = source;
            DrawArea = area;
        }

        /// <inheritdoc cref="ImageBrush(ImageBrush, Rectangle)"/>
        public ImageBrush(ITexture source)
            : this(source, new(Point.Empty, source.Size))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBrush"/> class.
        /// </summary>
        /// <param name="source">Original instance of an image brush.</param>
        /// <param name="area">New source area of an original image brush that is used for drawing.</param>
        public ImageBrush(ImageBrush source, Rectangle area)
            : this(source.Source, source.DrawArea.Cut(area))
        {
        }

        /// <summary>
        /// Gets the area of the texture that is drawn using this <see cref="ImageBrush"/> instance.
        /// </summary>
        public Rectangle DrawArea { get; }

        /// <inheritdoc/>
        public Size Size => DrawArea.Size;

        /// <summary>
        /// Gets or sets the source texture used for this brush instance.
        /// </summary>
        public ITexture Source { get; protected set; }

        /// <inheritdoc/>
        public virtual void Draw(IRenderContext context, in TextureRenderingOptions options)
        {
            context.Draw(Source, options with { Source = DrawArea.Cut(options.Source) });
        }
    }
}
