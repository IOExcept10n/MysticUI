using System.Drawing;
using System.Numerics;
using Icy.Configuration;
using Icy.Rendering.Fonts;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Xunit;

namespace Icy.Tests.Rendering.Fonts
{
    /// <summary>
    /// Covers a real end-to-end import + lookup of a vector (TrueType/OpenType) font through
    /// <see cref="FontSystem"/>, using the real <see cref="Icy.Assets.Importers.DynamicFonts.DynamicFontImporter"/>/
    /// <see cref="StbTrueTypeRasterizer"/> pipeline (see <c>Resources/Airfool.otf</c>) - not a hand-rolled mock like
    /// <c>SpriteFontTests</c>, since the bug this guards against lives in <see cref="FontSystem"/> itself.
    /// </summary>
    public class FontSystemTests
    {
        private static IcyConfiguration CreateConfiguration()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .AddBasicFontSupport();
            return builder.Build();
        }

        [Fact]
        public void GetOrLoad_AfterImportingVectorFont_ReturnsUsableFontAtRequestedSize()
        {
            IcyConfiguration config = CreateConfiguration();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            // Vector fonts are registered under a wildcard FontInfo (Size 0 - see DynamicFontsHelper.ReadNameTable),
            // since the file itself has no inherent pixel size. A query for a real, specific size must still find
            // that template rather than missing just because (family, size, style) was never registered exactly.
            IFont? font = config.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular));

            Assert.NotNull(font);
            Assert.Equal(16, font!.Info.Size);
        }

        [Fact]
        public void GetOrLoad_TwoDifferentSizesOfSameFamily_BothResolve()
        {
            IcyConfiguration config = CreateConfiguration();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");

            IFont? small = config.Fonts.GetOrLoad(new FontInfo("Airfool", 12, FontStyle.Regular));
            IFont? large = config.Fonts.GetOrLoad(new FontInfo("Airfool", 32, FontStyle.Regular));

            Assert.NotNull(small);
            Assert.NotNull(large);
            Assert.Equal(12, small!.Info.Size);
            Assert.Equal(32, large!.Info.Size);
        }

        [Fact]
        public void MeasureString_SpaceCharacter_ContributesItsOwnAdvanceWidth()
        {
            IcyConfiguration config = CreateConfiguration();
            config.Fonts.ImportFont(config.Assets.DefaultAssetContext, "Resources/Airfool.otf");
            IFont? font = config.Fonts.GetOrLoad(new FontInfo("Airfool", 16, FontStyle.Regular));
            Assert.NotNull(font);

            var options = new FontRenderingOptions(Vector2.Zero, null, 0, Vector2.Zero, 0, 0, Color.Black, 0, null);

            // A space glyph has no bitmap (StbTrueTypeRasterizer.RasterizeGlyph correctly returns null for it) but
            // must still contribute its own horizontal advance - if it's mistakenly treated as a missing glyph
            // (FontGlyph.None) instead of a spacing-only one, ProcessText skips it entirely and "A B" collapses
            // to the same width as "AB".
            float withSpace = font!.MeasureString("A B", options).X;
            float withoutSpace = font.MeasureString("AB", options).X;

            Assert.True(
                withSpace > withoutSpace,
                $"Expected \"A B\" ({withSpace}px) to measure wider than \"AB\" ({withoutSpace}px) - the space must contribute its own advance width.");
        }

        [Fact]
        public void GetOrLoad_UnimportedFamily_ReturnsNullRatherThanThrowing()
        {
            IcyConfiguration config = CreateConfiguration();

            IFont? font = config.Fonts.GetOrLoad(new FontInfo("NeverImported", 16, FontStyle.Regular));

            Assert.Null(font);
        }
    }
}
