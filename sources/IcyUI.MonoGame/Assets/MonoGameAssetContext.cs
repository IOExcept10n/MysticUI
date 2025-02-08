// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Icy.Assets;

namespace Icy.MonoGame.Assets
{
    public abstract class MonoGameAssetContext(Uri rootPath) : IAssetContext, IParsable<MonoGameAssetContext>
    {
        private const string FileScheme = "file";
        private const string AssemblyResourceScheme = "icy-res";
        private const string AssetScheme = "icy-asset";

        public Uri RootPath { get; } = rootPath;

        public static MonoGameAssetContext Create(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            return TryCreate(uri) ?? ThrowHelper.ThrowFormatException<MonoGameAssetContext>("Specified Uri is not in correct format so the context can't be determined.");
        }

        public static MonoGameAssetContext? TryCreate(Uri uri) => uri.Scheme switch
        {
            FileScheme => new FileAssetContext(uri),
            AssemblyResourceScheme => new AssemblyResourceContext(uri),
            AssetScheme => new FrameworkAssetContext(uri),
            _ => null
        };

        public static MonoGameAssetContext Parse(string s, IFormatProvider? provider)
        {
            return Create(new(s));
        }

        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out MonoGameAssetContext result)
        {
            if (Uri.IsWellFormedUriString(s, UriKind.RelativeOrAbsolute))
            {
                Uri destination = new(s);
                result = TryCreate(destination);
                return result != null;
            }

            result = null;
            return false;
        }

        public abstract IAssetContext Combine(string relativePath);

        public virtual string GetAbsolutePath(string? relativePath = null) => new Uri(RootPath, relativePath).AbsolutePath;

        public virtual string? GetDataFormat(string? relativePath = null) => MimeMapping.GetMimeType(Path.GetFileName(GetAbsolutePath(relativePath)));

        public abstract bool IsAvailable(string? relativePath = null);

        public abstract Stream OpenStream(string? relativePath = null);

        public abstract Task<Stream> OpenStreamAsync(string? relativePath = null);
    }
}