// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using TInputManager = Stride.Input.InputManager;
using TKeys = Stride.Input.Keys;

namespace Icy.Stride.Input.Devices
{
    /// <summary>
    /// Stride keyboard event listener, backed by the shared <see cref="TInputManager"/>.
    /// </summary>
    internal class KeyboardInput(TInputManager input) : IKeyboardInput, IUpdateableInput
    {
        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Keys>>? KeyDown;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Keys>>? KeyUp;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; set; } = true;

        /// <inheritdoc/>
        public IEnumerable<Keys> KeysDown => input.DownKeys.Select(InputExtensions.RemapKeys);

        /// <inheritdoc/>
        public ModifierKeys ModifierKeys { get; private set; }

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

            foreach (TKeys key in input.PressedKeys)
                KeyDown?.Invoke(this, key.RemapKeys());
            foreach (TKeys key in input.ReleasedKeys)
                KeyUp?.Invoke(this, key.RemapKeys());

            UpdateModifiers();
        }

        private void UpdateModifiers()
        {
            ModifierKeys modifiers = default;
            if (input.IsKeyDown(TKeys.LeftAlt) || input.IsKeyDown(TKeys.RightAlt))
                modifiers |= ModifierKeys.Alt;
            if (input.IsKeyDown(TKeys.LeftCtrl) || input.IsKeyDown(TKeys.RightCtrl))
                modifiers |= ModifierKeys.Ctrl;
            if (input.IsKeyDown(TKeys.LeftShift) || input.IsKeyDown(TKeys.RightShift))
                modifiers |= ModifierKeys.Shift;
            if (input.IsKeyDown(TKeys.LeftWin) || input.IsKeyDown(TKeys.RightWin))
                modifiers |= ModifierKeys.Win;
            ModifierKeys = modifiers;
        }
    }
}
