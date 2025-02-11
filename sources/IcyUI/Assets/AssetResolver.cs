// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Icy.Assets.Importers;
using Icy.Assets.Parsers;

namespace Icy.Assets
{
    /// <summary>
    /// Represents a default engine-independent implementation of the assets resolving service.
    /// </summary>
    internal sealed class AssetResolver : IAssetResolver
    {
        private readonly ConcurrentDictionary<IAssetContext, AssetScope> cacheScopes = [];
        private readonly AssetImporterCollection importers = [];
        private readonly Dictionary<string, IAssetParser> parsers = [];
        private readonly List<object> platformImporters = [];

        /// <inheritdoc/>
        public IAssetScope CreateScope(IAssetContext context)
        {
            var result = new AssetScope(this, context);
            cacheScopes.AddOrUpdate(context, result, (scope, prev) =>
            {
                prev.Dispose();
                return result;
            });
            return result;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var scope in cacheScopes)
            {
                scope.Value.Dispose();
            }

            cacheScopes.Clear();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public T LoadAsset<T>(IAssetContext context, string path)
                    where T : class
        {
            if (TryCheckScopeAndPlatformReference<T>(context, path, out var scope, out var asset))
                return asset;

            using var stream = context.OpenStream(path);
            return ImportAsset<T>(context, path, scope, stream);
        }

        /// <inheritdoc/>
        public async ValueTask<T> LoadAssetAsync<T>(IAssetContext context, string path)
            where T : class
        {
            if (TryCheckScopeAndPlatformReference<T>(context, path, out var scope, out var asset))
                return asset;

            using var stream = await context.OpenStreamAsync(path);
            return ImportAsset<T>(context, path, scope, stream);
        }

        /// <inheritdoc/>
        public void RegisterImporter<T>(IAssetImporter<T> importer) => importers.Add(importer);

        /// <inheritdoc/>
        public void RegisterParser(IAssetParser parser) => parsers[parser.Format] = parser;

        /// <inheritdoc/>
        public void RegisterPlatformImporter<T>(IPlatformAssetImporter<T> importer) => platformImporters.Add(importer);

        private T ImportAsset<T>(IAssetContext context, string path, AssetScope? scope, Stream stream)
            where T : class
        {
            string? format = context.GetDataFormat(path);
            if (importers.TryFindImporter<T>(format, out var importer))
            {
                var ctx = new ImportContext(this, format, context, path);
                T asset = importer.Import(stream, ctx);
                scope?.RegisterAsset(asset, path);
                return asset;
            }

            if (format == null)
            {
                return ThrowHelper.ThrowFormatException<T>("Couldn't determine format to deserialize the asset.");
            }

            if (parsers.TryGetValue(path, out var parser))
            {
                T asset = parser.Parse<T>(stream);
                scope?.RegisterAsset(asset, path);
                return asset;
            }

            return ThrowHelper.ThrowInvalidOperationException<T>("Couldn't find any importers or readers for the specified asset context.");
        }

        private bool TryCheckScopeAndPlatformReference<T>(IAssetContext context, string path, out AssetScope? scope, [NotNullWhen(true)] out T? asset)
                                                    where T : class
        {
            if (cacheScopes.TryGetValue(context, out scope) && scope.IsCached(path))
            {
                asset = scope.GetAsset<T>(path);
                return true;
            }

            if (context is IPlatformAssetReference assetReference)
            {
                asset = platformImporters.OfType<IPlatformAssetImporter<T>>().FirstOrDefault()?.LoadAsset(assetReference, path) ?? assetReference.Load<T>(path);
                scope?.RegisterAsset(asset, path);
                return true;
            }

            asset = null;
            return false;
        }

        private class AssetScope(AssetResolver resolver, IAssetContext root) : IAssetScope
        {
            private readonly ConcurrentDictionary<string, object> loadedAssets = [];

            public IAssetResolver AssetResolver { get; } = resolver;

            public IAssetContext RootContext { get; } = root;

            public void Dispose()
            {
                if (RootContext is IPlatformAssetReference assetReference)
                {
                    foreach (var asset in loadedAssets.Keys)
                        assetReference.Unload(asset);
                }

                foreach (var asset in loadedAssets.Values)
                {
                    if (asset is IDisposable disposable)
                        disposable.Dispose();
                }

                // Try to remove this scope from the resolver.
                resolver.cacheScopes.TryRemove(RootContext, out _);
                loadedAssets.Clear();
                GC.SuppressFinalize(this);
            }

            public T GetAsset<T>(string path)
                where T : class
            {
                if (!loadedAssets.TryGetValue(path, out var asset))
                    return ThrowAssetNotFoundException<T>("Can't find cached asset for this scope.");
                return (T)asset;
            }

            public bool IsCached(string path) => loadedAssets.ContainsKey(path);

            public void RegisterAsset(object asset, string path)
            {
                loadedAssets[path] = asset;
            }

            [DoesNotReturn]
            private static T ThrowAssetNotFoundException<T>(string description) => throw new AssetNotFoundException(description);
        }
    }
}