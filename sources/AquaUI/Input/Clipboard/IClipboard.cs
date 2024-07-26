// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;

namespace AquaUI.Input.Clipboard
{
    /// <summary>
    /// Represents an interface for the currently used clipboard implementation.
    /// </summary>
    public interface IClipboard
    {
        /// <summary>
        /// Gets or sets the text contents of the clipboard.
        /// </summary>
        [DisallowNull]
        string? Text { get; set; }
    }
}