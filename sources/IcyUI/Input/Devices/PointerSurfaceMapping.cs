// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Input.Devices
{
    /// <summary>
    /// Maps pointer positions reported against a pointer surface onto the back-buffer viewport, for engine connectors whose
    /// pointer surface and presented image differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pointer device may measure positions against its own surface - for a touch screen, the whole touch area - while the
    /// game presents its back buffer aspect-fit and centered on that surface: letterboxed (bars above and below) when the
    /// surface is taller than the back buffer, pillarboxed (bars left and right) when it is wider. Stride does this in
    /// fullscreen when the back buffer keeps its windowed aspect ratio.
    /// </para>
    /// <para>
    /// The mapping works on normalized positions and aspect ratios only, so the surface size may be in any unit. When the
    /// aspect ratios match (e.g. in windowed mode), it reduces to a plain scale.
    /// </para>
    /// </remarks>
    public static class PointerSurfaceMapping
    {
        /// <summary>
        /// Converts a normalized pointer position on a pointer surface into back-buffer pixels, assuming the back buffer is
        /// presented aspect-fit and centered on that surface.
        /// </summary>
        /// <param name="normalized">The pointer position on the surface, where <c>(0, 0)</c> is the top-left and <c>(1, 1)</c> the bottom-right corner.</param>
        /// <param name="surfaceSize">The pointer surface size, in any unit.</param>
        /// <param name="viewportSize">The back-buffer size, in physical pixels.</param>
        /// <returns>
        /// The position in back-buffer pixels. Points over the black bars map outside the viewport. A degenerate surface or
        /// viewport size falls back to scaling <paramref name="normalized"/> by <paramref name="viewportSize"/>.
        /// </returns>
        public static Point ToViewport(Vector2 normalized, SizeF surfaceSize, Size viewportSize)
        {
            Vector2 content = normalized;
            if (surfaceSize.Width > 0 && surfaceSize.Height > 0 && viewportSize.Width > 0 && viewportSize.Height > 0)
            {
                float surfaceAspect = surfaceSize.Width / surfaceSize.Height;
                float viewportAspect = (float)viewportSize.Width / viewportSize.Height;
                if (surfaceAspect > viewportAspect)
                {
                    // Pillarbox: the image spans the full height and a fraction of the width.
                    float fraction = viewportAspect / surfaceAspect;
                    content.X = (normalized.X - ((1f - fraction) / 2f)) / fraction;
                }
                else if (surfaceAspect < viewportAspect)
                {
                    // Letterbox: the image spans the full width and a fraction of the height.
                    float fraction = surfaceAspect / viewportAspect;
                    content.Y = (normalized.Y - ((1f - fraction) / 2f)) / fraction;
                }
            }

            return new Point((int)MathF.Round(content.X * viewportSize.Width), (int)MathF.Round(content.Y * viewportSize.Height));
        }
    }
}
