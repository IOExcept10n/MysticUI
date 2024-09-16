using AquaUI.Rendering;
using AquaUI.Rendering.Fonts;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.MonoGame.Rendering
{
    public static class MonoGameAdapters
    {
        public static ITexture Wrap(Texture2D texture) => new TextureAdapter(texture);

        public static IFont Wrap(SpriteFont font) => new FontAdapter(font);
    }
}
