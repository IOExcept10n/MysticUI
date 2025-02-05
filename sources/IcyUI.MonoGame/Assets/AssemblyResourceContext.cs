using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Icy.Assets;
using Icy.Data;

namespace Icy.MonoGame.Assets
{
    internal class AssemblyResourceContext : MonoGameAssetContext
    {
        private readonly Assembly sourceAssembly;

        public AssemblyResourceContext(Uri rootPath)
            : base(rootPath)
        {
            if (!string.IsNullOrWhiteSpace(RootPath.Authority))
            {
                sourceAssembly = AssemblyResolver.FindAssembly(RootPath.Authority);
            }
            else
            {
                sourceAssembly = AssemblyResolver.DefaultAssembly;
            }
        }

        // TODO: it requires to resolve assemblies for accessing resources.
        // For now I'm still not sure how to pass the resolver here
        public IAssemblyResolver AssemblyResolver { get; }

        public override MonoGameAssetContext Combine(string relativePath) => new AssemblyResourceContext(new(RootPath, relativePath));

        public override bool IsAvailable(string? relativePath = null) => 
            sourceAssembly.GetManifestResourceInfo(GetResourceName(relativePath)) != null;

        public override Stream OpenStream(string? relativePath = null) =>
            sourceAssembly.GetManifestResourceStream(GetResourceName(relativePath)) ??
            ThrowAssetNotFoundException("Couldn't open a stream to the specified relative path.");

        public override Task<Stream> OpenStreamAsync(string? relativePath = null) => Task.FromResult(OpenStream(relativePath));

        private string GetResourceName(string? relativePath = null)
        {
            Uri target = new(RootPath, relativePath);
            return $"{sourceAssembly.GetName().Name}{target.LocalPath.Replace('/', '.')}";
        }

        [DoesNotReturn]
        private static Stream ThrowAssetNotFoundException(string message)
            => throw new AssetNotFoundException(message);
    }
}
