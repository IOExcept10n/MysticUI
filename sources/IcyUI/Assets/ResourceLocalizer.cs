// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;

namespace Icy.Assets
{
    /// <summary>
    /// Represents a simple implementation of the localizing service, based on current resources registry.
    /// </summary>
    /// <param name="resources">An instance of the resources registry to get localization strings from.</param>
    public class ResourceLocalizer(IResourceRegistry resources) : ILocalizer
    {
        /// <summary>
        /// Gets an instance of the resources registry to get localization strings from.
        /// </summary>
        public IResourceRegistry Resources { get; } = resources;

        /// <inheritdoc/>
        public CultureInfo TargetCulture => Resources.PreferredCulture;

        /// <inheritdoc/>
        public string Localize(FormattableString value) => Resources.GetResource<string>(value.ToString());
    }
}
