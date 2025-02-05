// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Frozen;
using Icy.Assets.Importers;
using Icy.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Icy.MonoGame.Assets.Importers
{
    internal class TextureImporter(Game game) : IAssetImporter<TextureAdapter>
    {
        private static readonly FrozenSet<string> supportedFormats = FrozenSet.ToFrozenSet(
            [
                "image/jpeg",
                "image/png",
                "image/bmp"
            ]);

        public bool CanRead(string? format) => format != null && supportedFormats.Contains(format);

        public TextureAdapter Import(Stream stream, string? format) => Texture2D.FromStream(game.GraphicsDevice, stream);
    }
}