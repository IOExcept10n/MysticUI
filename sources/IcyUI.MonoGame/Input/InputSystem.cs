// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input;
using Icy.Input.Clipboard;
using Icy.Input.Devices;
using Icy.MonoGame.Input.Devices;
using Icy.MonoGame.Input.Events;
using Microsoft.Xna.Framework;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Icy.MonoGame.Input
{
    public class InputSystem : IInputSystem, IUpdateableInput
    {
        private readonly HashSet<IInputDeviceListener> devices;

        public IInputEventSystem Events { get; }

        public IKeyboardInput? Keyboard { get; }

        public IMouseInput Mouse { get; }

        public IGamepadInput? Gamepad { get; }

        public ITouchInput? Touch { get; }

        public IClipboard Clipboard { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

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

        public void Update(TimeSpan elapsed)
        {
            // TODO
            foreach (var device in devices)
            {
                if (device is IUpdateableInput updateable)
                {
                    updateable.Update(elapsed);
                }
            }
            Events.Update(elapsed);
        }

        public IEnumerator<IInputDeviceListener> GetEnumerator()
        {
            return devices.GetEnumerator();
        }

        public T GetInputDevice<T>() where T : IInputDeviceListener
        {
            return devices.OfType<T>().First();
        }

        public bool IsDeviceAvailable<T>() where T : IInputDeviceListener
        {
            return devices.OfType<T>().Any();
        }

        public bool TryGetInputDevice<T>([NotNullWhen(true)] out T? device) where T : IInputDeviceListener
        {
            device = devices.OfType<T>().FirstOrDefault();
            return device != null;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            foreach (var device in devices.OfType<IInitializable>())
                device.Initialize();
            Events.Initialize();
            IsInitialized = true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)devices).GetEnumerator();
        }
    }
}
