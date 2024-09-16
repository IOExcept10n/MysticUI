using AquaUI.Rendering.Fonts;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.MonoGame.Rendering
{
    // HACK
    public class FontAdapter(SpriteFont font) : IFont
    {
        public string FontFamily => throw new NotImplementedException();

        public float FontSize => Font.MeasureString(" ").Y;

        public FontStyle Style => throw new NotImplementedException();

        public SpriteFont Font { get; } = font;

        public Vector2 MeasureString(string text)
        {
            return Font.MeasureString(text).AsSystemVector();
        }
    }
}
