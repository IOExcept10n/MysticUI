// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;

namespace AquaUI.Assets
{
    /// <summary>
    /// Represents unified UI localization service.
    /// </summary>
    public interface ILocalizer
    {
        public CultureInfo TargetCulture { get; }

        public string Localize(FormattableString value);
    }
}