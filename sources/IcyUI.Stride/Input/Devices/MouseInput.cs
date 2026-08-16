// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using TInputManager = Stride.Input.InputManager;
using TMouseButton = Stride.Input.MouseButton;
using TVector2 = Stride.Core.Mathematics.Vector2;

namespace Icy.Stride.Input.Devices
{
    /// <summary>
    /// Stride mouse input listener, backed by the shared <see cref="TInputManager"/>.
    /// </summary>
    internal class MouseInput(TInputManager input) : IMouseInput, IUpdateableInput
    {
        private MouseButtons lastButtons;

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

            MouseButtons buttons = MouseButtons.None;
            foreach (TMouseButton button in input.DownButtons)
                buttons |= button.RemapButton();

            var pressed = ~lastButtons & buttons;
            if (pressed != MouseButtons.None)
            {
                MouseButtonPressed?.Invoke(this, pressed);
            }

            var released = lastButtons & ~buttons;
            if (released != MouseButtons.None)
            {
                MouseButtonReleased?.Invoke(this, released);
            }

            TVector2 position = input.AbsoluteMousePosition;
            MouseInfo = new(new System.Drawing.Point((int)position.X, (int)position.Y), buttons, input.MouseWheelDelta);
            lastButtons = buttons;
        }
    }
}
