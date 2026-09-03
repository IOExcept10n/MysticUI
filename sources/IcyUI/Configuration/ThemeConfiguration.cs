// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents configuration for the default visual theme applied to controls in this application.
    /// </summary>
    public class ThemeConfiguration
    {
        /// <summary>
        /// Gets or sets the <see cref="ResourceDictionary"/> whose implicit (keyless, per-type) <see cref="Icy.UI.Styles.Style"/>
        /// entries a <see cref="Canvas"/> built against this configuration exposes to every element attached to it -
        /// see <see cref="Canvas.Resources"/>.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> (the default) applies no theme - every control keeps its bare, undecorated look
        /// unless the application supplies its own <see cref="ResourceDictionary"/> here, or merges one manually
        /// some other way. Call <see cref="BuildingExtensions.UseDefaultTheme(IcyConfiguration)"/> on a built
        /// <see cref="IcyConfiguration"/> to load IcyUI's own bundled default theme into this property.
        /// </remarks>
        public ResourceDictionary? Theme { get; set; }
    }
}
