// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets.Importers;
using Icy.Configuration;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// Loads markup documents through the asset pipeline, so a UI file is requested by name like any other asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Register it with
    /// <see cref="BuildingExtensions.AddMarkupSupport(IAssetConfigurationBuilder, IcyConfiguration)"/>, after which
    /// <c>assets.Load&lt;UIElement&gt;("UI/MainMenu.xml")</c> returns a built tree.
    /// </para>
    /// <para>
    /// Each import produces a fresh tree - two loads of the same file give two independent element graphs, which is
    /// what a UI asset has to do, since elements are stateful and belong to exactly one place in one tree.
    /// Whether the underlying <em>file</em> is re-read is the asset scope's business, not this importer's.
    /// </para>
    /// </remarks>
    /// <param name="configuration">The configuration whose markup, conversion, and property services are used.</param>
    public class MarkupImporter(IcyConfiguration configuration) : IAssetImporter<UIElement>
    {
        /// <summary>
        /// The MIME type markup documents are served as.
        /// </summary>
        public const string MarkupMimeType = "application/xml";

        /// <summary>
        /// The alternative MIME type XML files are commonly reported as.
        /// </summary>
        public const string TextMarkupMimeType = "text/xml";

        private readonly MarkupLoader loader = new(configuration);

        /// <inheritdoc/>
        public bool CanRead(string? format) =>
            format is MarkupMimeType or TextMarkupMimeType;

        /// <inheritdoc/>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the language.</exception>
        public UIElement Import(Stream stream, IImportContext importContext)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(importContext);

            object root = loader.Load(stream, importContext.ResourceName);
            if (root is not UIElement element)
                throw new MarkupException($"Markup imported from '{importContext.ResourceName}' must declare a UIElement root, but it declares '{root.GetType().Name}'.");
            return element;
        }
    }
}
