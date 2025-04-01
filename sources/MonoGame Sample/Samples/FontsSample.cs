using Icy.Configuration;
using Icy.Data;
using Icy.Input.Devices;
using Icy.Rendering;
using Icy.Rendering.Fonts;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    internal class FontsSample : SampleBase
    {
        private IFont font;

        private int fontSize = 24;

        public FontsSample(Game game, IcyConfiguration configuration) : base(game, configuration, "Fonts sample")
        {
        }

        protected override void LoadContent()
        {
            //var context = UIConfiguration.Assets.DefaultAssetContext.Combine("Resources/Fonts/");
            UIConfiguration.Input.Events.RegisterCommand(new RelayCommand(x => fontSize++), KeyGesture.Parse("Ctrl++", null));
            UIConfiguration.Input.Events.RegisterCommand(new RelayCommand(x => fontSize--), KeyGesture.Parse("Ctrl+-", null));
            base.LoadContent();
        }

        public override void Draw(GameTime gameTime)
        {
            font = UIConfiguration.Fonts.GetOrLoad(new FontInfo("Verdana", fontSize, FontStyle.Regular));
            var options = default(FontRenderingOptions) with { Position = new(50, 100), Color = System.Drawing.Color.Black };
            UIConfiguration.RenderContext.Begin();
            string Text = $"""
Hello, world!
Here's an example of the multiline text printed with dynamic {font.Info.Family} font of size {fontSize}.
Also I can write here numbers: 0123456789, and even punctuation!
Best wishes - IOExcept10n!
И немного текста на русском для проверки.
ちょっと日本語もいいですよね！
""";
            font.DrawString(UIConfiguration.RenderContext, Text, options);
            //UIConfiguration.RenderContext.Draw(fontAtlas, new(new(50, 100, 256, 256)));
            //var glyphs = font.GetRenderGlyphs(Text, options);
            //foreach (var glyph in glyphs)
            //{
            //    UIConfiguration.RenderContext.DrawRectangle(glyph.Bounds, System.Drawing.Color.Red);
            //}
            var bounds = font.CalculateBounds(Text, options);
            UIConfiguration.RenderContext.DrawRectangle(bounds, System.Drawing.Color.Blue);
            UIConfiguration.RenderContext.End();
            base.Draw(gameTime);
        }
    }
}
