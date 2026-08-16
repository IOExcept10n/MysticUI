// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Icy.Assets;
using Icy.Data;

namespace Icy.Stride.Assets
{
    /// <summary>
    /// An <see cref="IAssetContext"/> for the embedded assembly resources.
    /// </summary>
    internal class AssemblyResourceContext : StrideAssetContext
    {
        private readonly Assembly sourceAssembly;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyResourceContext"/> class.
        /// </summary>
        /// <param name="rootPath">A <see cref="Uri"/> to determine resource identifier.</param>
        /// <param name="resolver">An instance of the assembly resolver to access external assemblies if requested.</param>
        public AssemblyResourceContext(Uri rootPath, IAssemblyResolver resolver)
            : base(rootPath)
        {
            AssemblyResolver = resolver;
            sourceAssembly = !string.IsNullOrWhiteSpace(RootPath.Authority)
                ? resolver.FindAssembly(RootPath.Authority) ?? resolver.DefaultAssembly
                : resolver.DefaultAssembly;
        }

        /// <summary>
        /// Gets an instance of the assembly resolver for types access.
        /// </summary>
        public IAssemblyResolver AssemblyResolver { get; }

        /// <inheritdoc/>
        public override StrideAssetContext Combine(string relativePath) => new AssemblyResourceContext(new(RootPath, relativePath), AssemblyResolver);

        /// <inheritdoc/>
        public override bool IsAvailable(string? relativePath = null) =>
            sourceAssembly.GetManifestResourceInfo(GetResourceName(relativePath)) != null;

        /// <inheritdoc/>
        public override Stream OpenStream(string? relativePath = null) =>
            sourceAssembly.GetManifestResourceStream(GetResourceName(relativePath)) ??
            ThrowAssetNotFoundException("Couldn't open a stream to the specified relative path.");

        /// <inheritdoc/>
        public override Task<Stream> OpenStreamAsync(string? relativePath = null) => Task.FromResult(OpenStream(relativePath));

        /// <summary>
        /// Throws an <see cref="AssetNotFoundException"/>.
        /// </summary>
        /// <param name="message">Message to pass to an exception.</param>
        /// <returns>Nothing.</returns>
        /// <exception cref="AssetNotFoundException">Occurs always.</exception>
        [DoesNotReturn]
        private static Stream ThrowAssetNotFoundException(string message)
            => throw new AssetNotFoundException(message);

        private string GetResourceName(string? relativePath = null)
        {
            Uri target = new(RootPath, relativePath);
            return $"{sourceAssembly.GetName().Name}{target.LocalPath.Replace('/', '.')}";
        }
    }
}
