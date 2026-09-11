using Icy.Rendering.Fonts;
using Xunit;

namespace Icy.Tests.Rendering.Fonts
{
    /// <summary>
    /// Covers <see cref="StbTrueTypeRasterizer.GetGlyphMetrics"/> - specifically the "hinting padding" applied to
    /// inked glyphs at font sizes &lt;= 16.
    /// </summary>
    public class StbTrueTypeRasterizerTests
    {
        [Fact]
        public void GetGlyphMetrics_Bearing_IsNotShiftedByTheHintingPadding()
        {
            // Regression: Bearing used to be (leftSideBearing + padding, y0 + padding) for any inked glyph at
            // size <= 16 - as if RasterizeGlyph's padded pixel buffer centered the glyph's ink with an empty
            // margin on every side. It doesn't: stbtt_MakeGlyphBitmapSubpixel always rasterizes starting at the
            // buffer's own (0,0) - the padding only adds blank, unused room on the buffer's right/bottom. So
            // Bearing must equal the font's true, unpadded left-side bearing/y0, not that plus 1 - getting this
            // wrong rendered every padded glyph (any inked glyph at a size <= 16, the common case for UI text)
            // 1px right and 1px down from where DrawString/the caret's own advance-based math agree it should be.
            //
            // Verified via proportional scaling: leftSideBearing/y0 scale linearly with font size, so requesting
            // the same glyph at a size just ABOVE 16 (where the padding hack does NOT apply) and dividing its
            // Bearing by the size ratio predicts what the padding-free Bearing at size 16 should be. The old,
            // buggy Bearing was consistently ~1px higher on both axes than this prediction; the fixed one matches
            // within normal float-rounding tolerance.
            byte[] fontData = File.ReadAllBytes("Resources/Airfool.otf");
            var rasterizer = new StbTrueTypeRasterizer(fontData);

            const float paddedSize = 16f;
            const float unpaddedSize = 17f;

            FontGlyph padded = rasterizer.GetGlyphMetrics('H', paddedSize, FontStyle.Regular);
            FontGlyph unpadded = rasterizer.GetGlyphMetrics('H', unpaddedSize, FontStyle.Regular);

            Assert.False(padded.IsEmpty, "'H' should have ink to rasterize.");

            float scale = paddedSize / unpaddedSize;
            float predictedBearingX = unpadded.Bearing.X * scale;
            float predictedBearingY = unpadded.Bearing.Y * scale;

            Assert.True(
                MathF.Abs(padded.Bearing.X - predictedBearingX) < 1f,
                $"Bearing.X={padded.Bearing.X} should be close to the scaled, padding-free prediction {predictedBearingX} (within 1px) - not shifted by the hinting padding.");
            Assert.True(
                MathF.Abs(padded.Bearing.Y - predictedBearingY) < 1f,
                $"Bearing.Y={padded.Bearing.Y} should be close to the scaled, padding-free prediction {predictedBearingY} (within 1px) - not shifted by the hinting padding.");
        }
    }
}
