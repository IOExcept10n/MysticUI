// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using MKeys = Microsoft.Xna.Framework.Input.Keys;

namespace Icy.MonoGame.Input.Devices
{
    /// <summary>
    /// MonoGame keyboard event listener.
    /// </summary>
    internal class KeyboardInput : IKeyboardInput, IUpdateableInput
    {
        private readonly MKeys[] keysPool;
        private readonly HashSet<MKeys> pressedKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyboardInput"/> class.
        /// </summary>
        /// <param name="game">An instance of the game to access input events.</param>
        public KeyboardInput(Microsoft.Xna.Framework.Game game)
        {
            var window = game.Window;
            window.KeyDown += Window_KeyDown;
            window.KeyUp += Window_KeyUp;
            pressedKeys = [];
            keysPool = Enum.GetValues<MKeys>();
            Array.Clear(keysPool);
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Keys>>? KeyDown;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Keys>>? KeyUp;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; set; } = true;

        /// <inheritdoc/>
        public IEnumerable<Keys> KeysDown => pressedKeys.Select(InputExtensions.RemapKeys);

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
            if (!IsListening) return;

            var state = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            Array.Clear(keysPool);
            state.GetPressedKeys(keysPool);

            pressedKeys.Clear();
            pressedKeys.UnionWith(keysPool);

            UpdateModifiers(state);
        }

        private void UpdateModifiers(Microsoft.Xna.Framework.Input.KeyboardState state)
        {
            ModifierKeys modifiers = default;
            if (state.IsKeyDown(MKeys.RightAlt) || state.IsKeyDown(MKeys.LeftAlt))
                modifiers |= ModifierKeys.Alt;
            if (state.IsKeyDown(MKeys.RightControl) || state.IsKeyDown(MKeys.LeftControl))
                modifiers |= ModifierKeys.Ctrl;
            if (state.IsKeyDown(MKeys.RightShift) || state.IsKeyDown(MKeys.LeftShift))
                modifiers |= ModifierKeys.Shift;
            if (state.IsKeyDown(MKeys.RightWindows) || state.IsKeyDown(MKeys.LeftWindows))
                modifiers |= ModifierKeys.Win;
            ModifierKeys = modifiers;
        }

        private void Window_KeyDown(object? sender, Microsoft.Xna.Framework.InputKeyEventArgs e)
        {
            if (!IsListening) return;
            KeyDown?.Invoke(this, e.Key.RemapKeys());
        }

        private void Window_KeyUp(object? sender, Microsoft.Xna.Framework.InputKeyEventArgs e)
        {
            if (!IsListening) return;
            KeyUp?.Invoke(this, e.Key.RemapKeys());
        }
    }
}