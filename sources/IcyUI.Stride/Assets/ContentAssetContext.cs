// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;
using Icy.Assets;
using Stride.Core.IO;
using Stride.Core.Serialization.Contents;

namespace Icy.Stride.Assets
{
    /// <summary>
    /// An asset context for assets processed through Stride's own content pipeline (loaded via <see cref="ContentManager"/>).
    /// </summary>
    internal class ContentAssetContext : StrideAssetContext, IPlatformAssetReference
    {
        private readonly Dictionary<string, object> loaded = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentAssetContext"/> class.
        /// </summary>
        /// <param name="rootPath">Root path for the assets access.</param>
        /// <param name="content">Content manager to load assets from.</param>
        public ContentAssetContext(Uri rootPath, ContentManager content)
            : base(rootPath)
        {
            Content = content;
        }

        /// <summary>
        /// Gets an instance of the <see cref="ContentManager"/> to load assets with.
        /// </summary>
        public ContentManager Content { get; }

        /// <inheritdoc/>
        public override StrideAssetContext Combine(string relativePath) => new ContentAssetContext(new(RootPath, relativePath), Content);

        /// <inheritdoc/>
        public override bool IsAvailable(string? relativePath = null) => Content.Exists(GetUrl(relativePath));

        /// <inheritdoc/>
        public override Stream OpenStream(string? relativePath = null) => Content.OpenAsStream(GetUrl(relativePath), StreamFlags.None);

        /// <inheritdoc/>
        public override Task<Stream> OpenStreamAsync(string? relativePath = null) => Task.FromResult(OpenStream(relativePath));

        /// <inheritdoc/>
        public override string GetAbsolutePath(string? relativePath = null) => GetUrl(relativePath);

        /// <inheritdoc/>
        public override string? GetDataFormat(string? relativePath = null) => null;

        /// <inheritdoc/>
        public T Load<T>(string? assetPath = null)
        {
            // IPlatformAssetReference.Load<T> has no constraint on T, but ContentManager.Load<T> requires
            // `T : class` - every real caller loads a reference-typed asset (Texture, etc.), so route through
            // reflection to satisfy that constraint without narrowing this method's own (which, per the interface,
            // it can't).
            string url = GetUrl(assetPath);
            MethodInfo loadMethod = typeof(ContentManager)
                .GetMethod(nameof(ContentManager.Load), 1, [typeof(string), typeof(ContentManagerLoaderSettings)])!
                .MakeGenericMethod(typeof(T));
            object asset = loadMethod.Invoke(Content, [url, default(ContentManagerLoaderSettings)])!;
            loaded[url] = asset;
            return (T)asset;
        }

        /// <inheritdoc/>
        public void Unload(string? assetPath = null)
        {
            string url = GetUrl(assetPath);
            if (loaded.Remove(url, out object? asset))
                Content.Unload(asset);
        }

        private string GetUrl(string? relativePath = null) => new Uri(RootPath, relativePath).LocalPath.Trim('/');
    }
}
