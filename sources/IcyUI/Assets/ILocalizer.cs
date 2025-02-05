// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;

namespace Icy.Assets
{
    /// <summary>
    /// Represents a UI localization service.
    /// </summary>
    public interface ILocalizer
    {
        /// <summary>
        /// Gets the info about culture to which current localization service performs translation.
        /// </summary>
        public CultureInfo TargetCulture { get; }

        /// <summary>
        /// Performs localization for the specified string.
        /// </summary>
        /// <param name="value">An instance of the <see cref="FormattableString"/> to localize.</param>
        /// <returns>A string with the localization result or the original string in case when the localization fails (e.g. when key to localize not found).</returns>
        public string Localize(FormattableString value);
    }
}
