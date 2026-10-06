// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Writes layout values the way people write them in markup.
    /// </summary>
    public static class MarkupValues
    {
        /// <summary>
        /// Formats a whole number in the invariant culture.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The markup text.</returns>
        public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Formats a thickness in its shortest form: <c>"4"</c>, <c>"4,8"</c> (horizontal, vertical) or <c>"1,2,3,4"</c>.
        /// </summary>
        /// <param name="value">The thickness.</param>
        /// <returns>The markup text.</returns>
        public static string Format(Thickness value)
        {
            if (value.Left == value.Right && value.Top == value.Bottom)
                return value.Left == value.Top ? Format(value.Left) : $"{Format(value.Left)},{Format(value.Top)}";
            return $"{Format(value.Left)},{Format(value.Top)},{Format(value.Right)},{Format(value.Bottom)}";
        }
    }
}
