// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Assets;

namespace Icy.MonoGame.Assets
{
    public abstract class MonoGameAssetContext(Uri rootPath) : IAssetContext<MonoGameAssetContext>
    {
        private const string FileScheme = "file";
        private const string AssemblyResourceScheme = "icy-res";
        private const string AssetScheme = "icy-asset";

        public Uri RootPath { get; } = rootPath;

        public static MonoGameAssetContext Create(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return uri.Scheme switch
            {
                FileScheme => new FileAssetContext(uri),
                AssemblyResourceScheme => new AssemblyResourceContext(uri),
                AssetScheme => new FrameworkAssetContext(uri),
                _ => ThrowHelper.ThrowFormatException<MonoGameAssetContext>("Specified Uri is not in correct format so the context can't be determined."),
            };
        }

        public abstract MonoGameAssetContext Combine(string relativePath);

        public virtual string GetAbsolutePath(string? relativePath = null) => new Uri(RootPath, relativePath).AbsolutePath;

        public virtual string? GetDataFormat(string? relativePath = null) => new AssetContext(RootPath).GetDataFormat(relativePath);

        public abstract bool IsAvailable(string? relativePath = null);

        public abstract Stream OpenStream(string? relativePath = null);

        public abstract Task<Stream> OpenStreamAsync(string? relativePath = null);
    }
}