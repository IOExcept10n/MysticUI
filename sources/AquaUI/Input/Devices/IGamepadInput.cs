// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Numerics;
using AquaUI.Data;

namespace AquaUI.Input.Devices
{
    /// <summary>
    /// Represents an interface for the gamepad input.
    /// </summary>
    public interface IGamepadInput : IInputDeviceListener, IInitializable
    {
        /// <summary>
        /// Occurs when the gamepad button is pressed.
        /// </summary>
        event EventHandler<GenericEventArgs<GamePadButtons>>? ButtonPressed;

        /// <summary>
        /// Occurs when the gamepad button is released.
        /// </summary>
        event EventHandler<GenericEventArgs<GamePadButtons>>? ButtonReleased;

        /// <summary>
        /// Occurs when the left gamepad stick moves.
        /// </summary>
        event EventHandler<GenericEventArgs<Vector2>>? LeftStickMove;

        /// <summary>
        /// Occurs when the right gamepad stick moves.
        /// </summary>
        event EventHandler<GenericEventArgs<Vector2>>? RightStickMove;

        /// <summary>
        /// Occurs when the left gamepad shoulder is updated.
        /// </summary>
        event EventHandler<GenericEventArgs<float>>? LeftShoulderUpdate;

        /// <summary>
        /// Occurs when the right gamepad shoulder is updated.
        /// </summary>
        event EventHandler<GenericEventArgs<float>>? RightShoulderUpdate;

        /// <summary>
        /// Gets the current gamepad state.
        /// </summary>
        GamePadState GamePadInfo { get; }
    }
}