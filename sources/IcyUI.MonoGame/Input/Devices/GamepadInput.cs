// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;

namespace Icy.MonoGame.Input.Devices
{
    /// <summary>
    /// MonoGame gamepad input listener.
    /// </summary>
    internal class GamepadInput : IGamepadInput, IUpdateableInput
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
            if (!IsListening) return;

            var state = Microsoft.Xna.Framework.Input.GamePad.GetState(0);
            lastState = GamePadInfo;
            GamePadInfo = new GamePadState(
                GetButtons(state.Buttons),
                state.ThumbSticks.Left.AsSystemVector(),
                state.ThumbSticks.Right.AsSystemVector(),
                state.Triggers.Left,
                state.Triggers.Right);

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

        private static GamePadButtons GetButtons(Microsoft.Xna.Framework.Input.GamePadButtons buttons)
        {
            GamePadButtons result = default;
            if (TestButton(buttons.A))
                result |= GamePadButtons.A;
            if (TestButton(buttons.B))
                result |= GamePadButtons.B;
            if (TestButton(buttons.LeftShoulder))
                result |= GamePadButtons.LeftShoulder;
            if (TestButton(buttons.LeftStick))
                result |= GamePadButtons.LeftThumb;
            if (TestButton(buttons.RightShoulder))
                result |= GamePadButtons.RightShoulder;
            if (TestButton(buttons.RightStick))
                result |= GamePadButtons.RightThumb;
            if (TestButton(buttons.X))
                result |= GamePadButtons.X;
            if (TestButton(buttons.Y))
                result |= GamePadButtons.Y;
            return result;
        }

        private static bool TestButton(Microsoft.Xna.Framework.Input.ButtonState state) => state == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
    }
}