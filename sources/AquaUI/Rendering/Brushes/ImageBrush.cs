// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace AquaUI.Rendering.Brushes
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
        public virtual void Draw<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer, Rectangle destination, Rectangle? source, Color color, float rotation, Vector2 origin, float depth)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class
        {
            if (Source is not TTexture texture)
            {
                texture = RecreateTexture(renderer);
            }

            renderer.Draw(texture, destination, source == null ? DrawArea : DrawArea.Cut(source.Value), color, rotation, origin, depth);
        }

        /// <summary>
        /// Creates copy of the current <see cref="ITexture"/> instance
        /// set as <see cref="Source"/> in this <see cref="ImageBrush"/> instance.
        /// Copy will be of <typeparamref name="TTexture"/> type.
        /// </summary>
        /// <typeparam name="TTexture">Type of the target texture prepared for drawing in this <paramref name="renderer"/> instance.</typeparam>
        /// <typeparam name="TGraphics">Type of the graphics device used in this rendering system.</typeparam>
        /// <param name="renderer">Instance of the <see cref="ITextureRenderer{TTexture, TGraphics}"/> to access graphics device.</param>
        /// <returns>Copy of the <see cref="Source"/> texture prepared for the rendering in given <paramref name="renderer"/> instance.</returns>
        protected TTexture RecreateTexture<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class
        {
            TTexture texture;
            Pixel[] buffer = new Pixel[Source.Size.Width * Source.Size.Height];
            Source.GetTextureData(buffer);
            Source = texture = TTexture.CreateTexture(renderer.GraphicsDevice, Source.Size.Width, Source.Size.Height, buffer);
            return texture;
        }

        private readonly struct Pixel
        {
            public readonly byte A;
            public readonly byte B;
            public readonly byte G;
            public readonly byte R;

            public Pixel(byte a, byte r, byte g, byte b)
            {
                A = a;
                R = r;
                G = g;
                B = b;
            }
        }
    }
}