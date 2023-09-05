using MysticUI.Controls;
using Stride.Core.Mathematics;

namespace MysticUI.Extensions
{
    /// <summary>
    /// Provides the extension methods for the control styles.
    /// </summary>
    public static class StyleUtils
    {
        /// <summary>
        /// Adds a default style to the control.
        /// </summary>
        /// <typeparam name="T">Type of the control to get the style for.</typeparam>
        /// <param name="target">Target control to apply the style to.</param>
        /// <returns>A control instance to make any other operations with it.</returns>
        public static T WithDefaultStyle<T>(this T target) where T : Control
        {
            target.SetStyle(target.GetType().Name + "Style");
            return target;
        }

        /// <summary>
        /// Tests the measure for the control using very big bounds to test if it's out of bounds.
        /// </summary>
        /// <typeparam name="T">Type of the target control.</typeparam>
        /// <param name="target">Target control to measure.</param>
        /// <returns>The results of the measuring process.</returns>
        public static Point TestMeasure<T>(this T target) where T : Control
        {
            return target.Measure(new Point(10000, 10000));
        }
    }
}