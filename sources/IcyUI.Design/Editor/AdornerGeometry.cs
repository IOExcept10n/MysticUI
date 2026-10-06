// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// Where adorners go, in surface units (the space canvas overlays are laid out and drawn in).
    /// </summary>
    internal static class AdornerGeometry
    {
        private static readonly (ResizeHandle Handle, float X, float Y)[] HandleAnchors =
        [
            (ResizeHandle.TopLeft, 0, 0),
            (ResizeHandle.Top, 0.5f, 0),
            (ResizeHandle.TopRight, 1, 0),
            (ResizeHandle.Right, 1, 0.5f),
            (ResizeHandle.BottomRight, 1, 1),
            (ResizeHandle.Bottom, 0.5f, 1),
            (ResizeHandle.BottomLeft, 0, 1),
            (ResizeHandle.Left, 0, 0.5f),
        ];

        /// <summary>
        /// Gets an element's outline in surface units: the axis-aligned bounds of its four corners after every transform.
        /// </summary>
        public static RectangleF SurfaceBounds(UIElement element)
        {
            Rectangle bounds = element.ActualBounds;
            return Bounds(
                element.PointToSurface(Vector2.Zero),
                element.PointToSurface(new Vector2(bounds.Width, 0)),
                element.PointToSurface(new Vector2(0, bounds.Height)),
                element.PointToSurface(new Vector2(bounds.Width, bounds.Height)));
        }

        /// <summary>
        /// Maps a rectangle in a container's local space (a placement indicator) into surface units.
        /// </summary>
        public static RectangleF ToSurface(UIElement container, RectangleF local) => Bounds(
            container.PointToSurface(new Vector2(local.Left, local.Top)),
            container.PointToSurface(new Vector2(local.Right, local.Top)),
            container.PointToSurface(new Vector2(local.Left, local.Bottom)),
            container.PointToSurface(new Vector2(local.Right, local.Bottom)));

        /// <summary>
        /// Converts a rectangle in screen pixels into surface units.
        /// </summary>
        public static RectangleF ScreenToSurface(Rectangle screen, float scale) =>
            new(screen.X / scale, screen.Y / scale, screen.Width / scale, screen.Height / scale);

        /// <summary>
        /// Converts a point in screen pixels into surface units.
        /// </summary>
        public static Vector2 ScreenToSurface(Point screen, float scale) => new(screen.X / scale, screen.Y / scale);

        /// <summary>
        /// Gets the eight resize handles of an outline: squares of <paramref name="size"/> centered on its corners and
        /// edge midpoints.
        /// </summary>
        public static IReadOnlyList<(ResizeHandle Handle, RectangleF Area)> Handles(RectangleF bounds, float size)
        {
            var handles = new (ResizeHandle, RectangleF)[HandleAnchors.Length];
            for (int i = 0; i < HandleAnchors.Length; i++)
            {
                (ResizeHandle handle, float x, float y) = HandleAnchors[i];
                float centerX = bounds.X + (bounds.Width * x);
                float centerY = bounds.Y + (bounds.Height * y);
                handles[i] = (handle, new RectangleF(centerX - (size / 2), centerY - (size / 2), size, size));
            }

            return handles;
        }

        /// <summary>
        /// Gets the handle under a surface point, or <see cref="ResizeHandle.None"/>.
        /// </summary>
        public static ResizeHandle HandleAt(RectangleF bounds, float size, Vector2 surfacePoint)
        {
            foreach ((ResizeHandle handle, RectangleF area) in Handles(bounds, size))
            {
                if (surfacePoint.X >= area.Left && surfacePoint.X <= area.Right && surfacePoint.Y >= area.Top && surfacePoint.Y <= area.Bottom)
                    return handle;
            }

            return ResizeHandle.None;
        }

        private static RectangleF Bounds(Point a, Point b, Point c, Point d)
        {
            int left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            int top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            int right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            int bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            return RectangleF.FromLTRB(left, top, right, bottom);
        }
    }
}
