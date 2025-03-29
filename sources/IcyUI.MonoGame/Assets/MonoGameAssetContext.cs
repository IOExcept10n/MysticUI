// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;

namespace Icy.MonoGame.Assets
{
    /// <summary>
    /// Represents base class for all asset context supported in core MonoGame support library.
    /// </summary>
    /// <param name="rootPath">The root path to access assets from.</param>
    public abstract class MonoGameAssetContext(Uri rootPath) : IAssetContext
    {
        /// <inheritdoc/>
        public Uri RootPath { get; } = rootPath;

        /// <inheritdoc/>
        public abstract IAssetContext Combine(string relativePath);

        /// <inheritdoc/>
        public virtual string GetAbsolutePath(string? relativePath = null) => new Uri(RootPath, relativePath).AbsolutePath;

        /// <inheritdoc/>
        public virtual string? GetDataFormat(string? relativePath = null) => MimeMapping.GetMimeType(Path.GetFileName(GetAbsolutePath(relativePath)));

        /// <inheritdoc/>
        public abstract bool IsAvailable(string? relativePath = null);

        /// <inheritdoc/>
        public abstract Stream OpenStream(string? relativePath = null);

        /// <inheritdoc/>
        public abstract Task<Stream> OpenStreamAsync(string? relativePath = null);
    }
}