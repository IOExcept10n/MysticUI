// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace Icy.MonoGame.Assets
{
    internal class FrameworkAssetContext : MonoGameAssetContext, INativeAssetReference
    {
        public FrameworkAssetContext(Uri rootPath) : base(rootPath)
        {
        }

        // TODO: it requires to use content manager
        // For now I'm still not sure how to pass the manager
        public ContentManager Content { get; }

        public override MonoGameAssetContext Combine(string relativePath) => new FrameworkAssetContext(new(RootPath, relativePath));

        public override bool IsAvailable(string? relativePath = null) => Path.Exists(GetAbsolutePath(relativePath));

        public override Stream OpenStream(string? relativePath = null) => TitleContainer.OpenStream(GetAbsolutePath(relativePath));

        public override Task<Stream> OpenStreamAsync(string? relativePath = null) => Task.FromResult(OpenStream(relativePath));

        public override string GetAbsolutePath(string? relativePath = null)
        {
            Uri target = new(RootPath, relativePath);
            return Path.Combine(Content.RootDirectory, target.LocalPath) + ".xnb";
        }

        public override string? GetDataFormat(string? relativePath = null) => "application/x-xnb";

        public T Load<T>(string? assetPath = null)
        {
            Uri target = new(RootPath, assetPath);
            return Content.Load<T>(target.LocalPath.Trim('/'));
        }

        public void Unload(string? assetPath = null)
        {
            Uri target = new(RootPath, assetPath);
            Content.UnloadAsset(target.LocalPath.Trim('/'));
        }
    }
}