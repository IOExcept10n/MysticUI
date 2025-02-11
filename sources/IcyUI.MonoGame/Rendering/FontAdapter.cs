using CommunityToolkit.Diagnostics;
using Icy.Rendering;
using Icy.Rendering.Fonts;
using System.Drawing;
using System.Numerics;
using SpriteFont = Microsoft.Xna.Framework.Graphics.SpriteFont;

namespace Icy.MonoGame.Rendering
{
    public class FontAdapter(SpriteFont font) : IFont
    {
        public FontInfo Info => throw new NotImplementedException();

        public FontMetrics Metrics => throw new NotImplementedException();

        public IReadOnlyDictionary<int, FontGlyph> Glyphs => font.Glyphs.ToDictionary(k => (int)k.Character, ToIcyGlyph);

        public Rectangle CalculateBounds(string text, in FontRenderingOptions options) => new(default, new(MeasureString(text, options).ToPoint()));

        public List<RenderGlyph> GetRenderGlyphs(string text, in FontRenderingOptions options)
        {
            throw new NotImplementedException();
        }

        public Vector2 MeasureString(string text, in FontRenderingOptions options) => font.MeasureString(text).AsSystemVector();

        void IFont.DrawString<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer, string text, in FontRenderingOptions options)
        {
            if (renderer is not MonoGameRenderer mgr)
            {
                ThrowHelper.ThrowArgumentException(nameof(renderer), "Renderer does not support font drawing.");
                return;
            }

            mgr.SpriteBatch.DrawString(font, text, options.Position, options.Color.AsEngineColor(), options.Rotation, options.Origin, (options.Scale ?? Vector2.One).AsEngineVector(), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, options.Depth);
        }

        private static FontGlyph ToIcyGlyph(SpriteFont.Glyph glyph) => new(glyph.Character, 0, 0, 0, new(glyph.RightSideBearing + glyph.LeftSideBearing, 0), glyph.BoundsInTexture.AsSystemRectangle().Size, default);
    }
}
