using System.Drawing;
using System.Numerics;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.Rendering.Fonts;
using Xunit;

namespace Icy.Tests.Rendering.Fonts
{
    public class SpriteFontTests : IDisposable
    {
        private readonly MockSpriteFont font;
        private readonly FontRenderingOptions defaultOptions;

        public SpriteFontTests()
        {
            // Create a mock font with some basic glyphs
            var glyphs = new Dictionary<int, FontGlyph>
            {
                // Basic Latin characters with consistent metrics:
                // - Capital letters: 10 units high, sitting on baseline (bearing.Y = 10)
                // - Lowercase letters: 7 units high, sitting on baseline (bearing.Y = 7)
                // - Descenders: 13 units high, extending 6 units below baseline (bearing.Y = 7)
                ['A'] = new FontGlyph('A', 0, 0, 8, new Vector2(0, 10), new Size(8, 10), new Rectangle(0, 0, 8, 10)),
                ['B'] = new FontGlyph('B', 0, 0, 8, new Vector2(0, 10), new Size(8, 10), new Rectangle(8, 0, 8, 10)),
                ['g'] = new FontGlyph('g', 0, 0, 8, new Vector2(0, 7), new Size(8, 13), new Rectangle(16, 0, 8, 13)), // Descender
                ['j'] = new FontGlyph('j', 0, 0, 4, new Vector2(0, 7), new Size(4, 13), new Rectangle(24, 0, 4, 13)), // Descender
                [' '] = new FontGlyph(' ', 0, 0, 4, Vector2.Zero, new Size(4, 0), Rectangle.Empty), // Space
            };

            var kernings = new Dictionary<MockSpriteFont.CodepointsPair, int>
            {
                [new('A', 'B')] = -1, // Kerning pair for "AB"
            };

            // Glyph dictionary keys must use the same FontInfo.Size/Style as the font itself - SupportsCharacter
            // looks glyphs up via GetStyledGlyph(codepoint), which builds a StyledGlyphDefinition from Info.Size/
            // Info.Style, so a mismatched size here means every character silently reports as unsupported.
            var info = new FontInfo("TestFont", 12, FontStyle.Regular);
            font = new MockSpriteFont(
                info,
                glyphs.ToDictionary(p => new StyledGlyphDefinition(p.Key, info.Size, info.Style), p => p.Value),
                kernings);

            defaultOptions = new FontRenderingOptions(
                Vector2.Zero,
                null,
                0f,
                Vector2.Zero,
                0f,
                0f,
                Color.White,
                0f,
                null);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        [Fact]
        public void MeasureString_EmptyText_ReturnsEmptyBounds()
        {
            var result = font.MeasureString(string.Empty, defaultOptions);
            Assert.Equal(Vector2.Zero, result);
        }

        [Fact]
        public void MeasureString_SingleLine_CorrectBounds()
        {
            var result = font.MeasureString("AB", defaultOptions);
            // Two characters of width 8 each, with -1 kerning between them
            Assert.Equal(15, result.X); // 8 + 8 - 1
            // Height is Size.Height (10) + Bearing.Y (10) = 20
            Assert.Equal(20, result.Y);
        }

        [Fact]
        public void MeasureString_WithDescenders_IncludesFullHeight()
        {
            var result = font.MeasureString("Ag", defaultOptions);
            // Height is max(Size.Height + Bearing.Y) = 20
            Assert.Equal(20, result.Y);
        }

        [Fact]
        public void MeasureString_MultiLine_CorrectBounds()
        {
            var result = font.MeasureString("A\nB", defaultOptions);
            Assert.Equal(8, result.X); // Width of single character

            // Two lines of height 20 each, minus 2 units overlap, plus line spacing
            var expectedHeight = 38 + defaultOptions.LineSpacing;
            Assert.Equal(expectedHeight, result.Y);
        }

        [Fact]
        public void GetRenderGlyphs_ReturnsCorrectGlyphSequence()
        {
            var glyphs = font.GetRenderGlyphs("AB", defaultOptions);
            Assert.Equal(2, glyphs.Count);
            Assert.Equal('A', glyphs[0].Codepoint);
            Assert.Equal('B', glyphs[1].Codepoint);
        }

        [Fact]
        public void GetRenderGlyphs_WithKerning_CorrectPositioning()
        {
            var glyphs = font.GetRenderGlyphs("AB", defaultOptions);
            // Second glyph should be positioned taking kerning into account
            Assert.Equal(7, glyphs[1].Bounds.X); // 8 - 1 (kerning)
        }

        [Fact]
        public void CalculateBounds_WithControlChars_CorrectBounds()
        {
            var bounds = font.CalculateBounds("A\nB\rA", defaultOptions);
            
            // Width should be max of any line (single character width)
            Assert.Equal(8, bounds.Width);

            // Two lines of height 20 each, minus 2 units overlap, plus line spacing
            var expectedHeight = 38 + defaultOptions.LineSpacing;
            Assert.Equal(expectedHeight, bounds.Height);
        }

        [Fact]
        public void CalculateBounds_VerifyLineHeightComponents()
        {
            // Verify our font metrics are calculated correctly
            Assert.Equal(10f, font.Metrics.Ascent); // Max bearing Y
            Assert.Equal(-6f, font.Metrics.Descent); // Min (bearing Y - height) = 7 - 13
            Assert.Equal(2f, font.Metrics.LineGap); // Set in constructor

            // Verify line height calculation
            var bounds = font.CalculateBounds("A\nB", defaultOptions);
            // Two lines of height 20 each, minus 2 units overlap, plus line spacing
            var expectedHeight = 38 + defaultOptions.LineSpacing;
            Assert.Equal(expectedHeight, bounds.Height);
        }

        [Fact]
        public void DrawString_WithTransform_AppliesTransform()
        {
            var options = new FontRenderingOptions(
                new Vector2(10, 10),
                new Vector2(2, 2),
                0f,
                Vector2.Zero,
                0f,
                0f,
                Color.White,
                0f,
                null);

            var glyphs = font.GetRenderGlyphs("A", options);
            Assert.Equal(16, glyphs[0].Bounds.Width); // 8 * 2
            Assert.Equal(20, glyphs[0].Bounds.Height); // 10 * 2
        }

        private class MockSpriteFont : SpriteFont
        {
            private readonly Dictionary<StyledGlyphDefinition, FontGlyph> glyphs;
            private readonly Dictionary<CodepointsPair, int> kernings;

            public MockSpriteFont(
                FontInfo info,
                Dictionary<StyledGlyphDefinition, FontGlyph> glyphs,
                Dictionary<CodepointsPair, int> kernings)
                : base(info, new MockFontAtlas(glyphs), null)
            {
                this.glyphs = glyphs;
                this.kernings = kernings;

                // Calculate metrics from glyphs
                float maxAscent = glyphs.Values.Max(g => g.Bearing.Y);
                float minDescent = glyphs.Values.Min(g => g.Bearing.Y - g.Size.Height);
                float lowercaseHeight = glyphs.Values
                    .Where(g => char.IsLower((char)g.Codepoint))
                    .Max(g => g.Size.Height);
                float capitalHeight = glyphs.Values
                    .Where(g => char.IsUpper((char)g.Codepoint))
                    .Max(g => g.Size.Height);

                Metrics = new FontMetrics(maxAscent, minDescent, 2, lowercaseHeight, capitalHeight);
            }

            public override bool SupportsCharacter(int codepoint) => glyphs.ContainsKey(GetStyledGlyph(codepoint));

            protected override float GetKerning(FontGlyph current, FontGlyph previous)
            {
                if (kernings.TryGetValue(new(previous.Codepoint, current.Codepoint), out int kerning))
                    return kerning;
                return 0;
            }

            protected override void Prepare(ReadOnlySpan<char> text, in FontRenderingOptions options, out int baseline, out int lineHeight)
            {
                baseline = (int)(Metrics.Ascent + options.Position.Y);
                lineHeight = (int)(Metrics.Ascent - Metrics.Descent + Metrics.LineGap);
            }

            public record struct CodepointsPair(int PreviousCodepoint, int NextCodepoint);
        }

        private class MockFontAtlas : IFontAtlas
        {
            private readonly Dictionary<StyledGlyphDefinition, FontGlyph> glyphs;

            public MockFontAtlas(Dictionary<StyledGlyphDefinition, FontGlyph> glyphs)
            {
                this.glyphs = glyphs;
            }

            public int PageCount => 1;

            public IReadOnlyDictionary<StyledGlyphDefinition, FontGlyph> Glyphs => glyphs;

            public IReadOnlyCollection<ITexture> Textures => [];

            public FontGlyph? GetGlyph(StyledGlyphDefinition glyphDefinition)
            {
                return glyphs.GetValueOrDefault(glyphDefinition);
            }

            public ITexture GetGlyphPage(in FontGlyph glyph)
            {
                return Textures.First();
            }
        }
    }
} 