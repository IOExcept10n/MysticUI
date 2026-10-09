// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input.Devices;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// Options for <see cref="EditorOverlay.Attach"/>.
    /// </summary>
    public sealed class EditorOverlayOptions
    {
        /// <summary>
        /// Gets or sets the part of the UI to edit (see <see cref="EditorSession.Scope"/>), or <see langword="null"/> (the
        /// default) for the whole canvas.
        /// </summary>
        public UIElement? Scope { get; set; }

        /// <summary>
        /// Gets or sets the key that shows and hides the overlay. Defaults to F4; <see langword="null"/> binds no key, for a
        /// game that toggles the overlay from its own input.
        /// </summary>
        public KeyGesture? ToggleKey { get; set; } = new(Keys.F4);

        /// <summary>
        /// Gets or sets the width of each side dock, in surface units. Defaults to 280.
        /// </summary>
        public float DockWidth { get; set; } = 280;

        /// <summary>
        /// Gets or sets the folder holding the source markup files, or <see langword="null"/> to keep the session's
        /// current resolver. See <see cref="DesignSession.UseSourceRoot(string)"/>.
        /// </summary>
        public string? SourceRoot { get; set; }
    }
}
