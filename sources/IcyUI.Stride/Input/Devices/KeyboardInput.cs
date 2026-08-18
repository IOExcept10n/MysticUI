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
        private readonly HashSet<TKeys> downKeys = [];

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

            // input.PressedKeys/ReleasedKeys are "since last Stride frame" edge lists - a stable snapshot for the
            // whole frame, not cleared between calls. Input gets pumped twice per frame by design (once from the
            // engine's own per-frame Update phase via IcyUIGameSystem, once more from Canvas.UpdateInput during
            // Draw - MonoGame has the identical structure and is unaffected, since its KeyDown comes from a raw
            // window event hook, not from re-reading state here), so re-dispatching straight from those edge
            // lists fired KeyDown/KeyUp twice per real key press. input.DownKeys is a live/current-state query
            // instead - diffing it against our own tracked snapshot (mirroring MouseInput's DownButtons-diffing
            // in this same folder) makes this idempotent no matter how many times Update() runs before the key's
            // actual state changes.
            HashSet<TKeys> currentlyDown = [.. input.DownKeys];

            foreach (TKeys key in currentlyDown)
            {
                if (downKeys.Add(key))
                    KeyDown?.Invoke(this, key.RemapKeys());
            }

            downKeys.RemoveWhere(key =>
            {
                if (currentlyDown.Contains(key))
                    return false;
                KeyUp?.Invoke(this, key.RemapKeys());
                return true;
            });

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
