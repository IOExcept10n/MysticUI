using FontStashSharp.RichText;
using Stride.Core.Mathematics;

namespace MysticUI.Extensions
{
    /// <summary>
    /// Provides the utilities to convert colors.
    /// </summary>
    public static class ColorTools
    {
        /// <summary>
        /// Parses the color from its string representation.
        /// </summary>
        /// <param name="value">string value of the color.</param>
        /// <returns>Color equivalent for the name.</returns>
        public static Color ParseColor(string value)
        {
            if (value == null) return default;
            if (value.StartsWith("#"))
            {
                // Remove the pound before string.
                string hexValue = value[1..];
                // Retrieve the length of the string.
                // 3 characters, every represents its own color.
                if (hexValue.Length == 3)
                {
                    byte r = Convert.ToByte(hexValue[0].ToString() + hexValue[0].ToString(), 16);
                    byte g = Convert.ToByte(hexValue[1].ToString() + hexValue[1].ToString(), 16);
                    byte b = Convert.ToByte(hexValue[2].ToString() + hexValue[2].ToString(), 16);
                    return new Color(r, g, b);
                }
                // 4 characters (with alpha).
                if (hexValue.Length == 4)
                {
                    byte a = Convert.ToByte(hexValue[0].ToString() + hexValue[0].ToString(), 16);
                    byte r = Convert.ToByte(hexValue[1].ToString() + hexValue[1].ToString(), 16);
                    byte g = Convert.ToByte(hexValue[2].ToString() + hexValue[2].ToString(), 16);
                    byte b = Convert.ToByte(hexValue[3].ToString() + hexValue[3].ToString(), 16);
                    return new Color(r, g, b, a);
                }
                // 6 chars, standard HEX format.
                if (hexValue.Length == 6)
                {
                    byte r = Convert.ToByte(hexValue[0..2], 16);
                    byte g = Convert.ToByte(hexValue[2..4], 16);
                    byte b = Convert.ToByte(hexValue[4..6], 16);
                    return new Color(r, g, b);
                }
                // 8 chars, ARGB-format.
                if (hexValue.Length == 8)
                {
                    byte a = Convert.ToByte(hexValue[0..2], 16);
                    byte r = Convert.ToByte(hexValue[2..4], 16);
                    byte g = Convert.ToByte(hexValue[4..6], 16);
                    byte b = Convert.ToByte(hexValue[6..8], 16);
                    return new Color(r, g, b, a);
                }
                return default;
            }
            // Parse the color from its name.
            var testColor = ColorStorage.FromName(value);
            if (testColor != null) return testColor.Value;
            else
            {
                var c = System.Drawing.Color.FromName(value);
                return new Color(c.R, c.G, c.B, c.A);
            }
        }
    }
}