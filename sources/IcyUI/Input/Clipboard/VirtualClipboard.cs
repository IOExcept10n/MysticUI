// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;

namespace Icy.Input.Clipboard
{
    /// <summary>
    /// Represents cross-platform basic internal implementation of the clipboard.
    /// </summary>
    /// <remarks>
    /// This class doesn't access platform clipboards, it just provides in-game clipboard to handle cases when there are no clipboards available.
    /// </remarks>
    public class VirtualClipboard : IClipboard
    {
        /// <inheritdoc/>
        [DisallowNull]
        public string? Text { get; set; }
    }
}