using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Rendering.Brushes
{
    public class ImageBrush : IImage
    {
        public ITexture Source { get; private set; }

        public Rectangle DrawArea { get; }

        public Size Size => DrawArea.Size;

        public ImageBrush(ITexture texture, Rectangle source)
        {
            Source = texture;
            DrawArea = source;
        }

        public ImageBrush(ITexture texture)
            : this(texture, new(Point.Empty, texture.Size))
        {
        }

        public ImageBrush(ImageBrush other, Rectangle source)
            : this(other.Source, other.DrawArea.Cut(source))
        {
        }

        void IBrush.Draw<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer, Rectangle destination, Rectangle? source, Color color, float rotation, float depth)
        {
            if (Source is not TTexture texture)
            {
                texture = RecreateTexture(renderer);
            }

            renderer.Draw(texture, destination, source == null ? DrawArea : DrawArea.Cut(source.Value), color, rotation, depth);
        }

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
            public readonly byte R;
            public readonly byte G;
            public readonly byte B;

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
