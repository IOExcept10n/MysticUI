// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Frozen;
using System.Net.Mime;
using Icy.Assets;
using Icy.Assets.Importers;
using Icy.Stride.Rendering;
using Stride.Engine;
using Stride.Graphics;

namespace Icy.Stride.Assets.Importers
{
    /// <summary>
    /// An importer for Stride textures using <see cref="TextureAdapter"/> as an adapter type for library access.
    /// </summary>
    /// <param name="game">An instance of the game to use for textures creation.</param>
    internal class TextureImporter(Game game) : IPlatformAssetImporter<TextureAdapter>
    {
        private static readonly FrozenSet<string> SupportedFormats = FrozenSet.ToFrozenSet(
            [
                MediaTypeNames.Image.Jpeg,
                MediaTypeNames.Image.Png,
                MediaTypeNames.Image.Bmp,
            ]);

        /// <inheritdoc/>
        public bool CanRead(string? format) => format != null && SupportedFormats.Contains(format);

        /// <inheritdoc/>
        public TextureAdapter Import(Stream stream, IImportContext context) => Texture.Load(game.GraphicsDevice, stream);

        /// <inheritdoc/>
        public TextureAdapter LoadAsset(IPlatformAssetReference assetReference, string? assetPath = null) => assetReference.Load<Texture>(assetPath);

        /// <inheritdoc/>
        public void UnloadAsset(IPlatformAssetReference assetReference, string? assetPath = null) => assetReference.Unload(assetPath);
    }
}
