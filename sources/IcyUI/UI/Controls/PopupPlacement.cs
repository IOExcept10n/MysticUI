// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// Placement rules shared by the controls that open a popup on the <see cref="Canvas"/>'s overlays.
    /// </summary>
    /// <remarks>
    /// Popups sit above every other control, in surface space (see <see cref="Canvas.SurfaceSize"/>), so they're fitted
    /// against the surface, never against an enclosing <see cref="ScrollViewer"/>.
    /// </remarks>
    internal static class PopupPlacement
    {
        /// <summary>
        /// Picks the popup's left edge so it stays attached to its owner and inside the surface.
        /// </summary>
        /// <param name="ownerLeft">The owner's left edge, in surface units.</param>
        /// <param name="ownerRight">The owner's right edge, in surface units.</param>
        /// <param name="popupWidth">The popup's width, in surface units.</param>
        /// <param name="surfaceWidth">The surface's width (see <see cref="Canvas.SurfaceSize"/>).</param>
        /// <returns>The popup's left edge, in surface units.</returns>
        /// <remarks>
        /// <list type="number">
        /// <item><description>The popup starts left-aligned with its owner.</description></item>
        /// <item><description>If it would cross the surface's right edge, it right-aligns with the owner instead.</description></item>
        /// <item><description>The result is clamped into the surface; a popup wider than the surface starts at <c>0</c>.</description></item>
        /// </list>
        /// </remarks>
        public static int FitHorizontally(int ownerLeft, int ownerRight, int popupWidth, int surfaceWidth)
        {
            int left = ownerLeft;
            if (left + popupWidth > surfaceWidth)
                left = ownerRight - popupWidth;
            return Math.Clamp(left, 0, Math.Max(0, surfaceWidth - popupWidth));
        }
    }
}
