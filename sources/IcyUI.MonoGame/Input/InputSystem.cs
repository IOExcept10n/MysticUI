// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Icy.Data;
using Icy.Input;
using Icy.Input.Clipboard;
using Icy.Input.Devices;
using Icy.MonoGame.Input.Devices;
using Icy.MonoGame.Input.Events;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Input
{
    /// <summary>
    /// Represents an instance of the input system that listens for MonoGame input devices.
    /// </summary>
    public class InputSystem : IInputSystem, IUpdateableInput
    {
        private readonly HashSet<IInputDeviceListener> devices;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSystem"/> class.
        /// </summary>
        /// <param name="game">An instance of the game to access input API.</param>
        public InputSystem(Game game)
        {
            Mouse = new MouseInput();
            Keyboard = new KeyboardInput(game);
            Gamepad = new GamepadInput();
            Touch = new TouchInput();
            Events = new InputEventSystem(this, text: new TextInputEvents(this, game));
            Clipboard = Clipboards.GetClipboard();
            devices = [Mouse, Keyboard, Gamepad, Touch];
        }

        /// <inheritdoc/>
        public IClipboard Clipboard { get; }

        /// <inheritdoc/>
        public IInputEventSystem Events { get; }

        /// <inheritdoc/>
        public IGamepadInput? Gamepad { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public IKeyboardInput? Keyboard { get; }

        /// <inheritdoc/>
        public IMouseInput Mouse { get; }

        /// <inheritdoc/>
        public ITouchInput? Touch { get; }

        /// <inheritdoc/>
        public IEnumerator<IInputDeviceListener> GetEnumerator()
        {
            return devices.GetEnumerator();
        }

        /// <inheritdoc/>
        public T GetInputDevice<T>()
            where T : IInputDeviceListener => devices.OfType<T>().First();

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            foreach (var device in devices.OfType<IInitializable>())
                device.Initialize();
            Events.Initialize();
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public bool IsDeviceAvailable<T>()
            where T : IInputDeviceListener => devices.OfType<T>().Any();

        /// <inheritdoc/>
        public bool TryGetInputDevice<T>([NotNullWhen(true)] out T? device)
            where T : IInputDeviceListener
        {
            device = devices.OfType<T>().FirstOrDefault();
            return device != null;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan elapsed)
        {
            // TODO
            foreach (var device in devices.OfType<IUpdateableInput>())
            {
                device.Update(elapsed);
            }

            Events.Update(elapsed);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)devices).GetEnumerator();
        }
    }
}