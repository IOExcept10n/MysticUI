// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Represents an image brush specialized for the controls drawing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When drawing, the image is split into 9 patches by <see cref="PatchSplitPadding"/>: the corners keep their size, the
    /// edges stretch along one axis, and the center stretches along both. That lets one small image frame a control of any
    /// size, such as a button background with rounded corners.
    /// </para>
    /// <para>
    /// The padding is measured in source pixels. The destination patches are recomputed on every draw, so the brush follows
    /// the element as it moves or resizes.
    /// </para>
    /// </remarks>
    public class NinePatchImageBrush : ImageBrush
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NinePatchImageBrush"/> class over a whole texture.
        /// </summary>
        /// <param name="source">The texture to draw.</param>
        /// <param name="patchSplit">The width of each border band, in source pixels.</param>
        public NinePatchImageBrush(ITexture source, Thickness patchSplit)
            : base(source)
        {
            PatchSplitPadding = patchSplit;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NinePatchImageBrush"/> class over a region of a texture.
        /// </summary>
        /// <param name="source">The texture to draw.</param>
        /// <param name="area">The region of <paramref name="source"/> holding the image.</param>
        /// <param name="patchSplit">The width of each border band, in source pixels.</param>
        public NinePatchImageBrush(ITexture source, Rectangle area, Thickness patchSplit)
            : base(source, area)
        {
            PatchSplitPadding = patchSplit;
        }

        /// <summary>
        /// Gets or sets the width of each border band, in source pixels: the left, top, right and bottom bands keep their
        /// size when drawn, and the center stretches.
        /// </summary>
        public Thickness PatchSplitPadding { get; set; }

        /// <inheritdoc/>
        public override void Draw(IRenderContext context, in TextureRenderingOptions options)
        {
            Rectangle source = DrawArea.Cut(options.Source);
            Rectangle destination = options.Destination;
            int left = (int)PatchSplitPadding.Left;
            int top = (int)PatchSplitPadding.Top;
            int right = (int)PatchSplitPadding.Right;
            int bottom = (int)PatchSplitPadding.Bottom;

            // Column and row bounds: [start, start + border, end - border, end], in source and destination space.
            Span<int> sourceColumns = [source.Left, source.Left + left, source.Right - right, source.Right];
            Span<int> sourceRows = [source.Top, source.Top + top, source.Bottom - bottom, source.Bottom];
            Span<int> destinationColumns = [destination.Left, destination.Left + left, destination.Right - right, destination.Right];
            Span<int> destinationRows = [destination.Top, destination.Top + top, destination.Bottom - bottom, destination.Bottom];

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    var patch = Rectangle.FromLTRB(destinationColumns[column], destinationRows[row], destinationColumns[column + 1], destinationRows[row + 1]);
                    var slice = Rectangle.FromLTRB(sourceColumns[column], sourceRows[row], sourceColumns[column + 1], sourceRows[row + 1]);
                    context.Draw(Source, options with { Destination = patch, Source = slice });
                }
            }
        }
    }
}
