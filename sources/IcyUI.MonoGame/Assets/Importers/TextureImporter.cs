// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Frozen;
using Icy.Assets;
using Icy.Assets.Importers;
using Icy.MonoGame.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Icy.MonoGame.Assets.Importers
{
    /// <summary>
    /// An importer for the MonoGame textures using <see cref="TextureAdapter"/> as an adapter type for library access.
    /// </summary>
    /// <param name="game">An instance of the game to use for textures creation.</param>
    internal class TextureImporter(Game game) : IPlatformAssetImporter<TextureAdapter>
    {
        private static readonly FrozenSet<string> SupportedFormats = FrozenSet.ToFrozenSet(
            [
                "image/jpeg",
                "image/png",
                "image/bmp"
            ]);

        /// <inheritdoc/>
        public bool CanRead(string? format) => format != null && SupportedFormats.Contains(format);

        /// <inheritdoc/>
        public TextureAdapter Import(Stream stream, IImportContext context) => Texture2D.FromStream(game.GraphicsDevice, stream);

        /// <inheritdoc/>
        public TextureAdapter LoadAsset(IPlatformAssetReference assetReference, string? assetPath = null) => assetReference.Load<Texture2D>(assetPath);

        /// <inheritdoc/>
        public void UnloadAsset(IPlatformAssetReference assetReference, string? assetPath = null) => assetReference.Unload(assetPath);
    }
}