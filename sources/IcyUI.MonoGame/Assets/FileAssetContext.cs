// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.MonoGame.Assets
{
    internal class FileAssetContext(Uri rootPath) : MonoGameAssetContext(rootPath)
    {
        public override MonoGameAssetContext Combine(string relativePath) => new FileAssetContext(new(RootPath, relativePath));

        public override bool IsAvailable(string? relativePath = null) => File.Exists(GetPath(relativePath));

        public override Stream OpenStream(string? relativePath = null) => File.OpenRead(GetPath(relativePath));

        public override Task<Stream> OpenStreamAsync(string? relativePath = null) => Task.FromResult<Stream>(File.OpenRead(GetPath(relativePath)));

        private string GetPath(string? relativePath = null) => new Uri(RootPath, relativePath).AbsolutePath;
    }
}