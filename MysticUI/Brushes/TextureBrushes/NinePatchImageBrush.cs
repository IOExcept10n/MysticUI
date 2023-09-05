// Code here is based on Myra project: https://github.com/rds1983/Myra
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace MysticUI.Brushes.TextureBrushes
{
    /// <summary>
    /// Represents an image brush with the ability to keep proportions on its edges using the nine patch techinque.
    /// </summary>
    public class NinePatchImageBrush : ImageBrush
    {
        private ImageBrush? topLeft,
            top,
            topRight,
            left,
            center,
            right,
            bottomLeft,
            bottom,
            bottomRight;

        private Thickness patchSplitter;

        /// <summary>
        /// Provides the splitter for the image patches.
        /// </summary>
        public Thickness PatchSplitter
        {
            get => patchSplitter;
            set
            {
                patchSplitter = value;
                var bounds = Bounds;
                int centerWidth = bounds.Width - patchSplitter.Width,
                centerHeight = bounds.Height - patchSplitter.Height;
                int y = bounds.Y;

                if (patchSplitter.Top > 0)
                {
                    if (patchSplitter.Left > 0)
                    {
                        topLeft = new ImageBrush(Texture,
                            new Rectangle(bounds.X,
                                y,
                                patchSplitter.Left,
                                patchSplitter.Top));
                    }

                    if (centerWidth > 0)
                    {
                        top = new ImageBrush(Texture,
                            new Rectangle(bounds.X + patchSplitter.Left,
                                y,
                                centerWidth,
                                patchSplitter.Top));
                    }

                    if (patchSplitter.Right > 0)
                    {
                        topRight = new ImageBrush(Texture,
                            new Rectangle(bounds.X + patchSplitter.Left + centerWidth,
                                y,
                                patchSplitter.Right,
                                patchSplitter.Top));
                    }
                }

                y += patchSplitter.Top;
                if (centerHeight > 0)
                {
                    if (patchSplitter.Left > 0)
                    {
                        left = new ImageBrush(Texture,
                            new Rectangle(bounds.X,
                                y,
                                patchSplitter.Left,
                                centerHeight));
                    }

                    if (centerWidth > 0)
                    {
                        center = new ImageBrush(Texture,
                            new Rectangle(bounds.X + patchSplitter.Left,
                                y,
                                centerWidth,
                                centerHeight));
                    }

                    if (patchSplitter.Right > 0)
                    {
                        right = new ImageBrush(Texture,
                            new Rectangle(bounds.X + patchSplitter.Left + centerWidth,
                                y,
                                patchSplitter.Right,
                                centerHeight));
                    }
                }

                y += centerHeight;
                if (patchSplitter.Bottom > 0)
                {
                    if (patchSplitter.Left > 0)
                    {
                        bottomLeft = new ImageBrush(Texture,
                            new Rectangle(bounds.X,
                                y,
                                patchSplitter.Left,
                                patchSplitter.Bottom));
                    }

                    if (centerWidth > 0)
                    {
                        bottom = new ImageBrush(Texture,
                            new Rectangle(bounds.X + patchSplitter.Left,
                                y,
                                centerWidth,
                                patchSplitter.Bottom));
                    }

                    if (patchSplitter.Right > 0)
                    {
                        bottomRight = new ImageBrush(Texture,
                            new Rectangle(bounds.X + patchSplitter.Left + centerWidth,
                                y,
                                patchSplitter.Right,
                                patchSplitter.Bottom));
                    }
                }
            }
        }

        /// <summary>
        /// A serialization constructor.
        /// </summary>
        public NinePatchImageBrush() : base()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="NinePatchImageBrush"/> class.
        /// </summary>
        /// <param name="texture">Texture for the splitting.</param>
        /// <param name="bounds">Bounds of the texture to get its part.</param>
        /// <param name="patchSplitter">Splitter to get the texture.</param>
        public NinePatchImageBrush(Texture texture, Rectangle bounds, Thickness patchSplitter) : base(texture, bounds)
        {
            PatchSplitter = patchSplitter;
        }

        /// <inheritdoc/>
        public override void Draw(RenderContext context, Rectangle destination, Color color)
        {
            int y = destination.Y;

            var left = Math.Min(PatchSplitter.Left, destination.Width);
            var top = Math.Min(PatchSplitter.Top, destination.Height);
            var right = Math.Min(PatchSplitter.Right, destination.Width);
            var bottom = Math.Min(PatchSplitter.Bottom, destination.Height);

            var centerWidth = destination.Width - left - right;
            if (centerWidth < 0)
            {
                centerWidth = 0;
            }

            var centerHeight = destination.Height - top - bottom;
            if (centerHeight < 0)
            {
                centerHeight = 0;
            }

            topLeft?.Draw(context, new(destination.X, y, left, top), color);
            if (centerWidth > 0) this.top?.Draw(context, new(destination.X + left, y, centerWidth, top), color);
            topRight?.Draw(context, new(destination.X + PatchSplitter.Left + centerWidth, y, right, top), color);
            y += top;
            if (centerHeight > 0)
            {
                this.left?.Draw(context, new(destination.X, y, left, centerHeight), color);
                if (centerWidth > 0) center?.Draw(context, new(destination.X + left, y, centerWidth, centerHeight), color);
                this.right?.Draw(context, new(destination.X + PatchSplitter.Left + centerWidth, y, right, centerHeight), color);
            }
            y += centerHeight;
            bottomLeft?.Draw(context, new(destination.X, y, left, bottom), color);
            if (centerWidth > 0) this.bottom?.Draw(context, new(destination.X + left, y, centerWidth, bottom), color);
            bottomRight?.Draw(context, new(destination.X + PatchSplitter.Left + centerWidth, y, right, bottom), color);
        }

        /// <inheritdoc/>
        protected override void OnTextureChanged()
        {
            // Reset all patch texture parts.
            PatchSplitter = patchSplitter;
        }
    }
}