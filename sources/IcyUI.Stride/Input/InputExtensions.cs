// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input.Devices;
using TGamePadButton = Stride.Input.GamePadButton;
using TKeys = Stride.Input.Keys;
using TMouseButton = Stride.Input.MouseButton;

namespace Icy.Stride.Input
{
    /// <summary>
    /// Provides extensions to help with Stride-specific input support.
    /// </summary>
    /// <remarks>
    /// Unlike MonoGame's <c>InputExtensions</c> (a hand-written ~150-entry lookup table), key and button remapping
    /// here is name-based: <see cref="Icy.Input.Devices.Keys"/>/<see cref="MouseButtons"/>/<see cref="GamePadButtons"/>
    /// share most member names with Stride's own <see cref="Stride.Input.Keys"/>/<see cref="Stride.Input.MouseButton"/>/
    /// <see cref="Stride.Input.GamePadButton"/>, so parsing by name avoids a large, easy-to-typo manual table and
    /// degrades gracefully (to "unmapped") for the handful of names that don't line up, rather than failing to compile.
    /// </remarks>
    public static class InputExtensions
    {
        /// <summary>
        /// Maps a <see cref="TKeys"/> value to a <see cref="Icy.Input.Devices.Keys"/> value by name.
        /// </summary>
        /// <param name="keys">Value to map.</param>
        /// <returns>The matching <see cref="Icy.Input.Devices.Keys"/> value, or <see cref="Icy.Input.Devices.Keys.None"/> if no member shares its name.</returns>
        public static Keys RemapKeys(this TKeys keys) =>
            Enum.TryParse(keys.ToString(), out Keys result) ? result : Keys.None;

        /// <summary>
        /// Maps a <see cref="TMouseButton"/> value to the matching <see cref="MouseButtons"/> flag by name.
        /// </summary>
        /// <param name="button">Value to map.</param>
        /// <returns>The matching <see cref="MouseButtons"/> flag, or <see cref="MouseButtons.None"/> if no member shares its name.</returns>
        public static MouseButtons RemapButton(this TMouseButton button) =>
            Enum.TryParse(button.ToString() + "Button", out MouseButtons result) ? result : MouseButtons.None;

        /// <summary>
        /// Maps a <see cref="TGamePadButton"/> flag set to the matching <see cref="GamePadButtons"/> flags.
        /// </summary>
        /// <param name="buttons">Value to map.</param>
        /// <returns>The matching <see cref="GamePadButtons"/> flags, combining every bit whose Stride name matches an Icy name.</returns>
        public static GamePadButtons RemapButtons(this TGamePadButton buttons)
        {
            var result = GamePadButtons.None;
            foreach (TGamePadButton flag in Enum.GetValues<TGamePadButton>())
            {
                if (flag != 0 && buttons.HasFlag(flag) && Enum.TryParse(flag.ToString(), out GamePadButtons mapped))
                    result |= mapped;
            }

            return result;
        }
    }
}
