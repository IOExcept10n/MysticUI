using Icy.Configuration;
using Icy.Rendering;
using Icy.Rendering.Fonts;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    internal class FontsSample : SampleBase
    {
        private IFont font;
        private ITexture fontAtlas;

        public FontsSample(Game game, IcyConfiguration configuration) : base(game, configuration, "Fonts sample")
        {
        }

        protected override void LoadContent()
        {
            var context = UIConfiguration.Assets.DefaultAssetContext.Combine("Resources/Fonts/");
            font = UIConfiguration.Assets.AssetResolver.LoadAsset<IFont>(context, "My own test Arial font.fnt");
            fontAtlas = UIConfiguration.Assets.AssetResolver.LoadAsset<ITexture>(context, "My own test Arial font_0.png");
            base.LoadContent();
        }

        public override void Draw(GameTime gameTime)
        {
            var options = default(FontRenderingOptions) with { Position = new(50, 100), Color = System.Drawing.Color.Black };
            UIConfiguration.RenderContext.Begin();
            const string Text = "abcdefghijklmnopqrstuvwxyz 1234567890";
            font.DrawString(UIConfiguration.RenderContext, Text, options);
            //UIConfiguration.RenderContext.Draw(fontAtlas, new(new(50, 100, 256, 256)));
            var glyphs = font.GetRenderGlyphs(Text, options);
            foreach (var glyph in glyphs)
            {
                UIConfiguration.RenderContext.DrawRectangle(glyph.Bounds, System.Drawing.Color.Red);
            }
            var bounds = font.CalculateBounds(Text, options);
            UIConfiguration.RenderContext.DrawRectangle(bounds, System.Drawing.Color.Blue);
            UIConfiguration.RenderContext.End();
            base.Draw(gameTime);
        }
    }
}
