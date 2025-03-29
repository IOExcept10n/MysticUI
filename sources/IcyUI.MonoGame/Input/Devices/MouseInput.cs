// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using Microsoft.Xna.Framework.Input;

namespace Icy.MonoGame.Input.Devices
{
    /// <summary>
    /// MonoGame mouse input listener.
    /// </summary>
    internal class MouseInput : IMouseInput, IUpdateableInput
    {
        private MouseState lastMouseState;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<MouseButtons>>? MouseButtonPressed;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<MouseButtons>>? MouseButtonReleased;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; private set; } = true;

        /// <inheritdoc/>
        public MouseInfo MouseInfo { get; private set; }

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
            var state = Mouse.GetState();
            var buttons = GetMouseButtons(state);
            var oldButtons = GetMouseButtons(lastMouseState);
            var pressed = ~oldButtons & buttons;
            if (pressed != MouseButtons.None)
            {
                MouseButtonPressed?.Invoke(this, pressed);
            }

            var released = oldButtons & ~buttons;
            if (released != MouseButtons.None)
            {
                MouseButtonReleased?.Invoke(this, released);
            }

            MouseInfo = new(new System.Drawing.Point(state.X, state.Y), buttons, state.ScrollWheelValue - lastMouseState.ScrollWheelValue);
            lastMouseState = state;
        }

        private static MouseButtons GetMouseButtons(MouseState mouse)
        {
            MouseButtons result = MouseButtons.None;
            if (mouse.LeftButton == ButtonState.Pressed) result |= MouseButtons.LeftButton;
            if (mouse.MiddleButton == ButtonState.Pressed) result |= MouseButtons.MiddleButton;
            if (mouse.RightButton == ButtonState.Pressed) result |= MouseButtons.RightButton;
            if (mouse.XButton1 == ButtonState.Pressed) result |= MouseButtons.ExtendedButton1;
            if (mouse.XButton2 == ButtonState.Pressed) result |= MouseButtons.ExtendedButton2;
            return result;
        }
    }
}