using MysticUI.Controls;
using Stride.Core.Mathematics;

namespace MysticUI.Extensions
{
    /// <summary>
    /// An utility class for points and vectors.
    /// </summary>
    public static class LayoutTools
    {
        /// <summary>
        /// Clamps the point between two values.
        /// </summary>
        /// <param name="value">A point value</param>
        /// <param name="min">Minimal value to clamp.</param>
        /// <param name="max">Maximal value to clamp.</param>
        /// <returns>New clamped point.</returns>
        public static Point Clamp(this Point value, Point min = default, Point max = default)
        {
            value.X = Math.Clamp(value.X, min.X, max.X);
            value.Y = Math.Clamp(value.Y, min.Y, max.Y);
            return value;
        }

        /// <summary>
        /// Aligns the control using alignment options.
        /// </summary>
        /// <param name="controlSize">Size of the control.</param>
        /// <param name="containerSize">size of the container.</param>
        /// <param name="horizontalAlignment">Horizontal control alignment.</param>
        /// <param name="verticalAlignment">Vertical control alignment.</param>
        /// <returns>Aligned control bounds.</returns>
        public static Rectangle Align(this Point controlSize, Point containerSize, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
        {
            var result = new Rectangle(0, 0, controlSize.X, controlSize.Y);

            switch (horizontalAlignment)
            {
                case HorizontalAlignment.Center:
                    result.X = (containerSize.X - controlSize.X) / 2;
                    break;

                case HorizontalAlignment.Right:
                    result.X = containerSize.X - controlSize.X;
                    break;

                case HorizontalAlignment.Stretch:
                    result.Width = containerSize.X;
                    break;
            }

            switch (verticalAlignment)
            {
                case VerticalAlignment.Center:
                    result.Y = (containerSize.Y - controlSize.Y) / 2;
                    break;

                case VerticalAlignment.Bottom:
                    result.Y = containerSize.Y - controlSize.Y;
                    break;

                case VerticalAlignment.Stretch:
                    result.Height = containerSize.Y;
                    break;
            }

            return result;
        }

        /// <summary>
        /// Clips a rectangle with defined size.
        /// </summary>
        /// <param name="rect">Rectangle to clip.</param>
        /// <param name="scissor">Maximal size to clip.</param>
        /// <returns>New rectangle with clipped size.</returns>
        public static Rectangle Clip(this Rectangle rect, Point scissor) => new(rect.X, rect.Y, Math.Min(rect.Width, scissor.X), Math.Min(rect.Height, scissor.Y));

        /// <summary>
        /// Counts total amount of children in given container control, including their children.
        /// </summary>
        /// <param name="control">Control to check.</param>
        /// <param name="onlyVisible">Determines to find only visible children.</param>
        /// <returns>Amount of control children, excluding itself.</returns>
        public static int TotalChildrenCount(this IContainerControl? control, bool onlyVisible = false) =>
            control == null ? 0 :
            control.Children
                   .Where(x => !(onlyVisible && !x.Visible))
                   .Sum(x => 1 + (x is IContainerControl container ? container.TotalChildrenCount(onlyVisible) : 0));

        /// <summary>
        /// Finds the child with given name or throws the exception when it doesn't exist.
        /// </summary>
        /// <param name="control">Target to find children in.</param>
        /// <param name="name">Name to find.</param>
        /// <returns>The first child with given name.</returns>
        public static T FindRequiredControlByName<T>(this Control control, string name) where T : Control
        {
            return (T)control.FindRequiredControlByName(name);
        }

        /// <summary>
        /// Finds the child with given name or throws the exception when it doesn't exist.
        /// </summary>
        /// <param name="control">Target to find children in.</param>
        /// <param name="name">Name to find.</param>
        /// <returns>The first child with given name.</returns>
        public static T? FindControlByName<T>(this Control control, string name) where T : Control
        {
            return (T?)control.FindControlByName(name);
        }

        internal static int OptionalClamp(this int value, int min = 0, int max = 0)
        {
            if (value < min && min != 0)
            {
                value = min;
            }
            else if (value > max && max != 0)
            {
                value = max;
            }
            return value;
        }
    }
}