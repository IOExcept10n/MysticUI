// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;
using AquaUI.Input;
using AquaUI.Input.Devices;
using AquaUI.MonoGame.Extensions;
using MKeys = Microsoft.Xna.Framework.Input.Keys;

namespace AquaUI.MonoGame.Input.Devices
{
    internal class KeyboardInput : IKeyboardInput, IUpdateableInput
    {
        private readonly HashSet<MKeys> pressedKeys;
        private readonly MKeys[] keysPool;

        public IEnumerable<Keys> KeysDown => pressedKeys.Select(InputExtensions.RemapKeys);

        public ModifierKeys ModifierKeys { get; private set; }

        public bool IsListening { get; set; } = true;

        public bool IsInitialized { get; private set; }

        public event EventHandler<GenericEventArgs<Keys>>? KeyDown;

        public event EventHandler<GenericEventArgs<Keys>>? KeyUp;

        public KeyboardInput(Microsoft.Xna.Framework.Game game)
        {
            var window = game.Window;
            window.KeyDown += Window_KeyDown;
            window.KeyUp += Window_KeyUp;
            pressedKeys = [];
            keysPool = Enum.GetValues<MKeys>();
            Array.Clear(keysPool);
        }

        private void Window_KeyUp(object? sender, Microsoft.Xna.Framework.InputKeyEventArgs e)
        {
            if (!IsListening) return;
            KeyUp?.Invoke(this, e.Key.RemapKeys());
        }

        private void Window_KeyDown(object? sender, Microsoft.Xna.Framework.InputKeyEventArgs e)
        {
            if (!IsListening) return;
            KeyDown?.Invoke(this, e.Key.RemapKeys());
        }

        public bool DisableListening()
        {
            IsListening = false;
            return true;
        }

        public bool EnableListening()
        {
            IsListening = true;
            return true;
        }

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

        public void Initialize()
        {
            IsInitialized = true;
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
    }
}