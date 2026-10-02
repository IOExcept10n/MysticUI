using System.Drawing;
using System.Numerics;
using Icy.Configuration;
using Icy.Rendering;
using Icy.Rendering.Fonts;
using Icy.Tests.Input;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class DeviceGlyphTests
    {
        private static (SpriteFont Font, FakeRenderContext Context) LoadFont()
        {
            var context = new FakeRenderContext();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(context).ConfigureInput(new FakeInputSystem()).ConfigureTypes().ConfigureAssets().AddBasicFontSupport();
            IcyConfiguration configuration = builder.Build();
            configuration.Fonts.ImportFont(configuration.Assets.DefaultAssetContext, "Resources/Airfool.otf");
            var font = (SpriteFont)configuration.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular))!;
            return (font, context);
        }

        private static FontRenderingOptions Options() => default(FontRenderingOptions) with { Color = Color.White };

        private static List<Rectangle> Draw(SpriteFont font, FakeRenderContext context, float scale, string text)
        {
            context.DrawCalls.Clear();
            context.Transform = Transform2D.Create(Matrix3x2.CreateScale(scale));
            font.DrawString(context, text, Options());
            return context.DrawCalls.Select(call => call.Options.Destination).ToList();
        }

        [Theory]
        [InlineData(1f, 1f)]
        [InlineData(1.005f, 1f)]
        [InlineData(1.37f, 1.25f)]
        [InlineData(1.5f, 1.5f)]
        [InlineData(2f, 2f)]
        public void GetDeviceScale_QuantizesUniformScale(float scale, float expected) =>
            Assert.Equal(expected, SpriteFont.GetDeviceScale(Transform2D.Create(Matrix3x2.CreateScale(scale))));

        [Fact]
        public void GetDeviceScale_NonUniformOrRotated_ReturnsOne()
        {
            Assert.Equal(1f, SpriteFont.GetDeviceScale(Transform2D.Create(Matrix3x2.CreateScale(2f, 1f))));
            Assert.Equal(1f, SpriteFont.GetDeviceScale(Transform2D.Create(Matrix3x2.CreateScale(2f) * Matrix3x2.CreateRotation(0.3f))));
        }

        [Fact]
        public void DrawString_AtScale_UsesDeviceGlyphsAtLogicalPenPositions()
        {
            var (font, context) = LoadFont();
            const string text = "Hello, World";
            List<Rectangle> logical = Draw(font, context, 1f, text);
            List<Rectangle> device = Draw(font, context, 1.5f, text);
            char[] drawn = [.. text.Where(c => c != ' ')];
            var rasterizer = ((DynamicSpriteFont)font).Rasterizer;

            Assert.Equal(logical.Count, device.Count);
            for (int i = 0; i < logical.Count; i++)
            {
                // Same pen position (device units ÷ 1.5 ≈ logical units), but the bitmap rasterized at 1.5× the font size.
                // Bitmap sizes don't scale linearly (the rasterizer pads glyph bitmaps), so compare against the
                // rasterizer's own device-size metrics rather than 1.5× the logical bitmap.
                Assert.InRange(device[i].X / 1.5f, logical[i].X - 1.5f, logical[i].X + 1.5f);
                Assert.Equal(rasterizer.GetGlyphMetrics(drawn[i], 16 * 1.5f, FontStyle.Regular).Size, device[i].Size);
            }
        }

        [Fact]
        public void DrawString_AtScale_DrawsWithCompensatedTransform_AndRestoresIt()
        {
            var (font, context) = LoadFont();
            Draw(font, context, 2f, "Hi");

            Assert.All(context.DrawCalls, call => Assert.Equal(1f, call.TransformAtDrawTime.Scale.X, 3));
            Assert.Equal(2f, context.Transform.Scale.X, 3);
        }

        [Fact]
        public void DrawString_MultiLine_KeepsLinePositionsAtScale()
        {
            var (font, context) = LoadFont();
            List<Rectangle> logical = Draw(font, context, 1f, "A\nA\nA");
            List<Rectangle> device = Draw(font, context, 1.5f, "A\nA\nA");

            Assert.Equal(3, device.Count);
            for (int i = 0; i < 3; i++)
            {
                float deviceTop = device[i].Y / 1.5f;
                Assert.InRange(deviceTop, logical[i].Y - 1.5f, logical[i].Y + 1.5f);
            }
        }
    }
}
