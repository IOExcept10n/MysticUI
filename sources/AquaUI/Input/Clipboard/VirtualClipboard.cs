using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Input.Clipboard
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
