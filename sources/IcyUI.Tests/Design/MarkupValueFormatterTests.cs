using System.Drawing;
using System.Globalization;
using System.Numerics;
using Icy.Data;
using Icy.Design;
using Icy.Rendering.Brushes;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Design
{
    public class MarkupValueFormatterTests
    {
        [Flags]
        private enum Sides { None = 0, Left = 1, Right = 2 }

        private static readonly MarkupValueFormatter Formatter = new(new TypeConversionManager());

        public static TheoryData<object, Type> RoundTrips => new()
        {
            { "hello", typeof(string) },
            { true, typeof(bool) },
            { 42, typeof(int) },
            { -7L, typeof(long) },
            { 0.1f, typeof(float) },
            { 1e-7f, typeof(float) },
            { 123456.789, typeof(double) },
            { HorizontalAlignment.Right, typeof(HorizontalAlignment) },
            { Sides.Left | Sides.Right, typeof(Sides) },
            { new Thickness(1, 2, 3, 4), typeof(Thickness) },
            { new Thickness(5), typeof(Thickness) },
            { Color.FromArgb(128, 10, 20, 30), typeof(Color) },
        };

        [Theory]
        [MemberData(nameof(RoundTrips))]
        public void AFormattedValue_ConvertsBackToAnEqualValue(object value, Type type)
        {
            Assert.True(Formatter.TryFormat(value, type, out string? text));
            Assert.Equal(value, new TypeConversionManager().Convert(text, type));
        }

        [Fact]
        public void ASolidBrush_FormatsAsItsColor()
        {
            Assert.True(Formatter.TryFormat(new SolidColorBrush(Color.FromArgb(255, 1, 2, 3)), typeof(IBrush), out string? text));
            Assert.Equal("#FF010203", text);
        }

        [Fact]
        public void Numbers_UseTheInvariantCulture_EvenUnderACommaCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            try
            {
                Assert.True(Formatter.TryFormat(1.5f, typeof(float), out string? text));
                Assert.Equal("1.5", text);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void AValueWithNoMarkupForm_IsRefused()
        {
            Assert.False(Formatter.TryFormat(new object(), typeof(object), out _));
            Assert.False(Formatter.TryFormat(null, typeof(int), out _));
        }

        [Fact]
        public void ANullString_FormatsAsEmpty()
        {
            Assert.True(Formatter.TryFormat(null, typeof(string), out string? text));
            Assert.Equal(string.Empty, text);
        }

        [Theory]
        [InlineData(255, 255, 255, 255)]
        [InlineData(255, 0, 0, 0)]
        [InlineData(255, 255, 0, 0)]
        public void AColorMatchingANamedColor_StillRoundTrips(int a, int r, int g, int b)
        {
            Color color = Color.FromArgb(a, r, g, b);

            Assert.True(Formatter.TryFormat(color, typeof(Color), out _));
            Assert.True(Formatter.TryFormat(new SolidColorBrush(color), typeof(IBrush), out _));
        }
    }
}
