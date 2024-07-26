// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Rendering;
using Stride.Graphics;
using System.Numerics;

namespace AquaUI
{
    public class FontWrapper : IFont
    {
        public FontWrapper(SpriteFont font, FontStyle info = FontStyle.Regular)
        {
            Font = font;
            Style = info;
        }

        public SpriteFont Font { get; }

        public string FontFamily => Font.Name;

        public float FontSize => Font.Size;

        public FontStyle Style { get; }

        public Vector2 MeasureString(string text)
        {
            (float x, float y) = Font.MeasureString(text);
            return new(x, y);
        }
    }
}