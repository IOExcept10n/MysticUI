// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace Icy.MonoGame.Assets
{
    /// <summary>
    /// An asset context for the MonoGame platform assets.
    /// </summary>
    internal class FrameworkAssetContext : MonoGameAssetContext, IPlatformAssetReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FrameworkAssetContext"/> class.
        /// </summary>
        /// <param name="rootPath">Root path for the assets access.</param>
        /// <param name="content">Content manager to load assets from.</param>
        public FrameworkAssetContext(Uri rootPath, ContentManager content)
            : base(rootPath)
        {
            Content = content;
        }

        /// <summary>
        /// Gets an instance of the <see cref="ContentManager"/> to load assets with.
        /// </summary>
        public ContentManager Content { get; }

        /// <inheritdoc/>
        public override MonoGameAssetContext Combine(string relativePath) => new FrameworkAssetContext(new(RootPath, relativePath), Content);

        /// <inheritdoc/>
        public override bool IsAvailable(string? relativePath = null) => Path.Exists(GetAbsolutePath(relativePath));

        /// <inheritdoc/>
        public override Stream OpenStream(string? relativePath = null) => TitleContainer.OpenStream(GetAbsolutePath(relativePath));

        /// <inheritdoc/>
        public override Task<Stream> OpenStreamAsync(string? relativePath = null) => Task.FromResult(OpenStream(relativePath));

        /// <inheritdoc/>
        public override string GetAbsolutePath(string? relativePath = null)
        {
            Uri target = new(RootPath, relativePath);
            return Path.Combine(Content.RootDirectory, target.LocalPath) + ".xnb";
        }

        /// <inheritdoc/>
        public override string? GetDataFormat(string? relativePath = null) => "application/x-xnb";

        /// <inheritdoc/>
        public T Load<T>(string? assetPath = null)
        {
            Uri target = new(RootPath, assetPath);
            return Content.Load<T>(target.LocalPath.Trim('/'));
        }

        /// <inheritdoc/>
        public void Unload(string? assetPath = null)
        {
            Uri target = new(RootPath, assetPath);
            Content.UnloadAsset(target.LocalPath.Trim('/'));
        }
    }
}