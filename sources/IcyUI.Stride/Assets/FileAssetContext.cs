// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Stride.Assets
{
    /// <summary>
    /// An asset context for the computer files.
    /// </summary>
    /// <param name="rootPath">Root path to locate files in.</param>
    internal class FileAssetContext(Uri rootPath) : StrideAssetContext(rootPath)
    {
        /// <inheritdoc/>
        public override StrideAssetContext Combine(string relativePath) => new FileAssetContext(new(RootPath, relativePath));

        /// <inheritdoc/>
        public override bool IsAvailable(string? relativePath = null) => File.Exists(GetPath(relativePath));

        /// <inheritdoc/>
        public override Stream OpenStream(string? relativePath = null) => File.OpenRead(GetPath(relativePath));

        /// <inheritdoc/>
        public override Task<Stream> OpenStreamAsync(string? relativePath = null) => Task.FromResult<Stream>(File.OpenRead(GetPath(relativePath)));

        /// <summary>
        /// Gets the path for the file to access.
        /// </summary>
        /// <param name="relativePath">Path part relative to the current root path.</param>
        /// <returns>Full path to access the file.</returns>
        private string GetPath(string? relativePath = null) => new Uri(RootPath, relativePath).LocalPath;
    }
}
