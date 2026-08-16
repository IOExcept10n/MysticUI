// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Icy.Assets;
using Icy.Stride.Configuration;
using Stride.Engine;

namespace Icy.Stride.Assets
{
    /// <summary>
    /// Represents a factory that creates Stride-related asset contexts.
    /// </summary>
    /// <param name="game">An instance of the game for asset access.</param>
    internal class StrideAssetContextFactory(Game game) : IAssetContextFactory
    {
        private const string FileScheme = "file";
        private const string AssemblyResourceScheme = "icy-res";
        private const string AssetScheme = "icy-asset";

        /// <inheritdoc/>
        public bool TryCreate(Uri uri, [NotNullWhen(true)] out IAssetContext? context)
        {
            context = uri.Scheme switch
            {
                FileScheme => new FileAssetContext(uri),
                AssemblyResourceScheme => new AssemblyResourceContext(uri, game.GetIcyConfiguration().Types.AssemblyResolver),
                AssetScheme => new ContentAssetContext(uri, game.Content),
                _ => null,
            };
            return context != null;
        }

        /// <inheritdoc/>
        public IAssetContext Create(Uri uri)
        {
            if (!TryCreate(uri, out IAssetContext? context))
            {
                return ThrowHelper.ThrowFormatException<IAssetContext>("Couldn't determine Uri scheme to determine asset context to use.");
            }

            return context;
        }
    }
}
