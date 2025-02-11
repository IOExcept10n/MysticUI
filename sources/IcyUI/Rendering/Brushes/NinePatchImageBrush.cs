// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Controls;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Represents an image brush specialized for the controls drawing.
    /// </summary>
    /// <remarks>
    /// When drawing, an image is split by 9 patches with special scaling rules depending on their location.
    /// </remarks>
    public class NinePatchImageBrush : ImageBrush
    {
        private Rectangle[] cachedPatches;
        private bool cacheIsValid;
        private Thickness patchSplitPadding;

        public NinePatchImageBrush(ITexture source, Thickness patchSplit)
            : base(source)
        {
            PatchSplitPadding = patchSplit;
            cacheIsValid = false;
        }

        public NinePatchImageBrush(ITexture source, Rectangle area, Thickness patchSplit)
            : base(source, area)
        {
            PatchSplitPadding = patchSplit;
            cacheIsValid = false;
        }

        /// <summary>
        /// Gets or sets padding for the patch splitter grid.
        /// </summary>
        public Thickness PatchSplitPadding
        {
            get => patchSplitPadding;
            set
            {
                if (patchSplitPadding != value)
                {
                    patchSplitPadding = value;
                    cacheIsValid = false; // Invalidate cache on change
                }
            }
        }

        /// <inheritdoc/>
        public override void Draw(IRenderContext context, in TextureRenderingOptions options)
        {
            // Update the cached rectangles if the cache is invalid
            if (!cacheIsValid)
            {
                CalculateCachedPatches(options.Destination);
            }

            // Define the source rectangles based on the provided source rectangle
            Rectangle sourceRect = options.Source ?? DrawArea;

            // Calculate the center point for rotation
            int centerX = options.Destination.X + (options.Destination.Width / 2);
            int centerY = options.Destination.Y + (options.Destination.Height / 2);

            // Draw each patch using the cached rectangles
            for (int i = 0; i < cachedPatches.Length; i++)
            {
                DrawPatch(context, Source, options with { Destination = cachedPatches[i], Source = sourceRect.Cut(cachedPatches[i]) });
            }
        }

        private void CalculateCachedPatches(Rectangle destination)
        {
            int leftWidth = (int)PatchSplitPadding.Left;
            int topHeight = (int)PatchSplitPadding.Top;
            int rightWidth = (int)PatchSplitPadding.Right;
            int bottomHeight = (int)PatchSplitPadding.Bottom;

            // Calculate the width and height of the center area
            int centerWidth = destination.Width - leftWidth - rightWidth;
            int centerHeight = destination.Height - topHeight - bottomHeight;

            // Initialize the cached patches array
            cachedPatches = new Rectangle[9];

            // Define the rectangles for each patch
            cachedPatches[0] = new Rectangle(destination.X, destination.Y, leftWidth, topHeight); // Top Left
            cachedPatches[1] = new Rectangle(destination.X + leftWidth, destination.Y, centerWidth, topHeight); // Top Center
            cachedPatches[2] = new Rectangle(destination.X + leftWidth + centerWidth, destination.Y, rightWidth, topHeight); // Top Right

            cachedPatches[3] = new Rectangle(destination.X, destination.Y + topHeight, leftWidth, centerHeight); // Middle Left
            cachedPatches[4] = new Rectangle(destination.X + leftWidth, destination.Y + topHeight, centerWidth, centerHeight); // Middle Center
            cachedPatches[5] = new Rectangle(destination.X + leftWidth + centerWidth, destination.Y + topHeight, rightWidth, centerHeight); // Middle Right

            cachedPatches[6] = new Rectangle(destination.X, destination.Y + topHeight + centerHeight, leftWidth, bottomHeight); // Bottom Left
            cachedPatches[7] = new Rectangle(destination.X + leftWidth, destination.Y + topHeight + centerHeight, centerWidth, bottomHeight); // Bottom Center
            cachedPatches[8] = new Rectangle(destination.X + leftWidth + centerWidth, destination.Y + topHeight + centerHeight, rightWidth, bottomHeight); // Bottom Right

            cacheIsValid = true; // Mark cache as valid
        }

        private static void DrawPatch(IRenderContext context, ITexture texture, in TextureRenderingOptions options)
        {
            context.Draw(texture, options);
        }
    }
}