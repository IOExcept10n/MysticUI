// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Styles
{
    /// <summary>
    /// Represents the interaction states a <see cref="UIElement"/> can combine at once.
    /// </summary>
    [Flags]
    public enum ControlState
    {
        /// <summary>
        /// No interaction state is active.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// The pointer is over the element.
        /// </summary>
        Hovered = 1 << 0,

        /// <summary>
        /// The element is being pressed (e.g. a mouse button is held down over it).
        /// </summary>
        Pressed = 1 << 1,

        /// <summary>
        /// The element is disabled and shouldn't respond to input.
        /// </summary>
        Disabled = 1 << 2,

        /// <summary>
        /// The element has input focus.
        /// </summary>
        Focused = 1 << 3,

        /// <summary>
        /// The element is being touched.
        /// </summary>
        Touching = 1 << 4,

        /// <summary>
        /// The element represents an "on"/checked value (e.g. a checked <see cref="Controls.ToggleButton"/>).
        /// </summary>
        Checked = 1 << 5,

        /// <summary>
        /// The element represents an expanded value (e.g. an expanded <see cref="Controls.Expander"/>).
        /// </summary>
        Expanded = 1 << 6,

        /// <summary>
        /// The element represents the currently selected item in a <see cref="Controls.Selector"/>.
        /// </summary>
        Selected = 1 << 7,

        /// <summary>
        /// The element is the currently keyboard/gamepad-highlighted item in an open <see cref="Controls.Selector"/>
        /// popup - distinct from <see cref="Selected"/>, which persists after the popup closes.
        /// </summary>
        Highlighted = 1 << 8,

        /// <summary>
        /// One of the element's bindings failed to write a value to its source (see
        /// <see cref="Data.Bindings.Binding.HasError"/>), e.g. text that isn't a number in a numeric field.
        /// </summary>
        Invalid = 1 << 9,
    }
}
