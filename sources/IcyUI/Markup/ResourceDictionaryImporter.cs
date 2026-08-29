// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets.Importers;
using Icy.Configuration;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// Loads a standalone <see cref="UI.ResourceDictionary"/> document through the asset pipeline, so a shared or
    /// themed dictionary file can be requested by path like any other asset - what
    /// <c>&lt;ResourceDictionary Source="Themes/Default.xml"/&gt;</c> does when it appears under
    /// <see cref="UI.ResourceDictionary.MergedDictionaries"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Register it with
    /// <see cref="BuildingExtensions.AddMarkupSupport(IAssetConfigurationBuilder, IcyConfiguration)"/>, alongside
    /// <see cref="MarkupImporter"/> - the two importers share one <see cref="MarkupLoader"/>, differing only in
    /// which root type each requires the document to declare.
    /// </para>
    /// <para>
    /// Each import produces a fresh dictionary, for the same reason <see cref="MarkupImporter"/> produces a fresh
    /// tree: two loads of the same file must not share one mutable instance.
    /// </para>
    /// </remarks>
    /// <param name="configuration">The configuration whose markup, conversion, and property services are used.</param>
    public class ResourceDictionaryImporter(IcyConfiguration configuration) : IAssetImporter<ResourceDictionary>
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
        /// <exception cref="MarkupException">
        /// The document is malformed, breaks a rule of the language, or its root is not a
        /// <see cref="ResourceDictionary"/>.
        /// </exception>
        public ResourceDictionary Import(Stream stream, IImportContext importContext)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(importContext);

            object loaded = loader.LoadObject(stream, importContext.ResourceName);
            return loaded as ResourceDictionary
                ?? throw new MarkupException(
                    $"'{importContext.ResourceName}' declares a '{loaded.GetType().Name}' as its root, but a ResourceDictionary Source requires a '{nameof(ResourceDictionary)}'.",
                    importContext.ResourceName);
        }
    }
}
