using Icy.Configuration;
using Icy.MonoGame;
using Icy.Rendering;
using Icy.Rendering.Fonts;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    internal class FontsSample : SampleBase
    {
        private IFont font;

        private int fontSize = 24;
        private bool displayText = true;
        private bool displayOutline = false;
        private bool displayBoxes = false;
        private bool displayAtlases = false;

        public FontsSample(Game game, IcyConfiguration configuration) : base(game, configuration, "Fonts sample")
        {
        }

        protected override void LoadContent()
        {
            //var context = UIConfiguration.Assets.DefaultAssetContext.Combine("Resources/Fonts/");
            UIConfiguration.Fonts.EnableSystemFonts();
            UIConfiguration.Fonts.ImportFont(UIConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");
            UIConfiguration.Fonts.ImportSystemFont(new("Yu Gothic", 24, FontStyle.Regular));
            UIConfiguration.Fonts.ImportSystemFont(new("Segoe UI Emoji", 24, FontStyle.Regular));

            RegisterCommand("Ctrl++", x => fontSize++);
            RegisterCommand("Ctrl+-", x => fontSize--);
            RegisterCommand("Ctrl+Shift+T", x => displayText = !displayText);
            RegisterCommand("Ctrl+O", x => displayOutline = !displayOutline);
            RegisterCommand("Ctrl+B", x => displayBoxes = !displayBoxes);
            RegisterCommand("Ctrl+A", x => displayAtlases = !displayAtlases);
            RegisterCommand("Ctrl+Shift+C", x => UIConfiguration.Fonts.Clear());
            base.LoadContent();
        }

        public override void Draw(GameTime gameTime)
        {
            font = UIConfiguration.Fonts.GetOrLoad(new FontInfo("Airfool", fontSize, FontStyle.Regular));
            if (font == null)
                return;
            var options = default(FontRenderingOptions) with { Position = new(50, 20), Color = System.Drawing.Color.Black };
            string Text = $"""
Hello, world!
{font.Info.Family} font of size {fontSize}
Here's an example of the multiline text.
Also I can write here numbers: 0123456789, and even punctuation!
Best wishes - IOExcept10n!
И немного текста на русском для проверки.
ちょっと日本語もいいですよね！
P.S. Смайлики теперь тоже можно 🫧
""";

            UIConfiguration.RenderContext.Begin();

            if (displayText) font.DrawString(UIConfiguration.RenderContext, Text, options);

            if (displayBoxes)
            {
                var glyphs = font.GetRenderGlyphs(Text, options);
                foreach (var glyph in glyphs)
                {
                    UIConfiguration.RenderContext.DrawRectangle(glyph.Bounds, System.Drawing.Color.Red);
                }
            }
            if (displayOutline)
            {
                var bounds = font.CalculateBounds(Text, options);
                UIConfiguration.RenderContext.DrawRectangle(bounds, System.Drawing.Color.Blue);
            }
            if (displayAtlases)
            {
                Vector2 pos = new(0, 20);
                foreach (var tex in ((SpriteFont)font).Atlas.Textures)
                {
                    System.Drawing.Rectangle drawArea = new(pos.AsSystemPoint(), tex.Size);
                    //UIConfiguration.RenderContext.FillRectangle(drawArea, Color.Black.AsSystemColor());
                    UIConfiguration.RenderContext.Draw(tex, new(drawArea, null, Color.Black.AsSystemColor()));
                    pos.X += tex.Size.Width;
                }
            }

            UIConfiguration.RenderContext.End();
            base.Draw(gameTime);
        }
    }
}
