// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using TGamePadDevice = Stride.Input.IGamePadDevice;
using TInputManager = Stride.Input.InputManager;

namespace Icy.Stride.Input.Devices
{
    /// <summary>
    /// Stride gamepad input listener, backed by the shared <see cref="TInputManager"/>.
    /// </summary>
    /// <remarks>
    /// Reads the first connected gamepad only - matching <see cref="IGamepadInput"/>'s single-gamepad contract.
    /// </remarks>
    internal class GamepadInput(TInputManager input) : IGamepadInput, IUpdateableInput
    {
        private GamePadState lastState;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<GamePadButtons>>? ButtonPressed;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<GamePadButtons>>? ButtonReleased;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<float>>? LeftShoulderUpdate;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Vector2>>? LeftStickMove;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<float>>? RightShoulderUpdate;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Vector2>>? RightStickMove;

        /// <inheritdoc/>
        public GamePadState GamePadInfo { get; private set; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; private set; } = true;

        /// <inheritdoc/>
        public bool DisableListening()
        {
            IsListening = false;
            return true;
        }

        /// <inheritdoc/>
        public bool EnableListening()
        {
            IsListening = true;
            return true;
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            if (!IsListening)
                return;

            TGamePadDevice? pad = input.GamePads.FirstOrDefault();
            lastState = GamePadInfo;
            GamePadInfo = pad == null
                ? default
                : new GamePadState(
                    pad.State.Buttons.RemapButtons(),
                    pad.State.LeftThumb.AsSystemVector(),
                    pad.State.RightThumb.AsSystemVector(),
                    pad.State.LeftTrigger,
                    pad.State.RightTrigger);

            if (lastState.LeftThumb != GamePadInfo.LeftThumb)
                LeftStickMove?.Invoke(this, GamePadInfo.LeftThumb);
            if (lastState.RightThumb != GamePadInfo.RightThumb)
                RightStickMove?.Invoke(this, GamePadInfo.RightThumb);

            if (lastState.LeftTrigger != GamePadInfo.LeftTrigger)
                LeftShoulderUpdate?.Invoke(this, GamePadInfo.LeftTrigger);
            if (lastState.RightTrigger != GamePadInfo.RightTrigger)
                RightShoulderUpdate?.Invoke(this, GamePadInfo.RightTrigger);

            var newButtons = ~lastState.Buttons & GamePadInfo.Buttons;
            var oldButtons = lastState.Buttons & ~GamePadInfo.Buttons;
            if (newButtons != default)
                ButtonPressed?.Invoke(this, newButtons);
            else if (oldButtons != default)
                ButtonReleased?.Invoke(this, oldButtons);
        }
    }
}
