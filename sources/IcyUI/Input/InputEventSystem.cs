// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Input.Gestures;

namespace Icy.Input
{
    /// <summary>
    /// Represents the default event system implementation.
    /// It uses default handlers for all event types.
    /// </summary>
    public class InputEventSystem : IInputEventSystem
    {
        private readonly KeyboardListener listener;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputEventSystem"/> class.
        /// </summary>
        /// <param name="inputSystem">An instance of the <see cref="IInputSystem"/> to initialize all internal event listeners.</param>
        /// <param name="devices">Implementation of the events processor for the devices update.</param>
        /// <param name="drag">Implementation of the events processor for the drag and drop events.</param>
        /// <param name="navigation">Implementation of the events processor for the navigation events.</param>
        /// <param name="scroll">Implementation of the events processor for the scroll events.</param>
        /// <param name="text">Implementation of the events processor for the text events.</param>
        /// <param name="touch">Implementation of the events processor for the touch events.</param>
        /// <param name="gestures">The gesture recognizer, or <see langword="null"/> for the built-in one.</param>
        public InputEventSystem(
            IInputSystem inputSystem,
            IDeviceEvents? devices = null,
            IDragEvents? drag = null,
            INavigationEvents? navigation = null,
            IScrollEvents? scroll = null,
            ITextEvents? text = null,
            ITouchEvents? touch = null,
            IGestureEvents? gestures = null)
        {
            InputSystem = inputSystem;
            listener = new(inputSystem);
            Devices = devices ?? new DeviceEvents(InputSystem);
            Drag = drag ?? new DragEvens(InputSystem);
            Navigation = navigation ?? new NavigationEvents(InputSystem);
            Scroll = scroll ?? new ScrollEvents(InputSystem);
            Text = text ?? new TextEvents(InputSystem);
            Touch = touch ?? new TouchEvents(InputSystem);
            Gestures = gestures ?? new GestureRecognizer(InputSystem);
        }

        /// <inheritdoc/>
        public IDeviceEvents Devices { get; }

        /// <inheritdoc/>
        public IDragEvents Drag { get; }

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public INavigationEvents Navigation { get; }

        /// <inheritdoc/>
        public IScrollEvents Scroll { get; }

        /// <inheritdoc/>
        public ITextEvents Text { get; }

        /// <inheritdoc/>
        public ITouchEvents Touch { get; }

        /// <inheritdoc/>
        public IGestureEvents Gestures { get; }

        /// <summary>
        /// Initializes the current input event system instance with the provided input event listeners.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;
            listener.Initialize();
            Gestures.Initialize();
            Devices.Initialize();
            Drag.Initialize();
            Touch.Initialize();
            Text.Initialize();
            Navigation.Initialize();
            Scroll.Initialize();
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public void RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null, bool handlesGesture = false) =>
            listener.Commands.Add(command, gesture, argument, handlesGesture);

        /// <inheritdoc/>
        public void UnregisterCommand(KeyGesture gesture) => listener.Commands.RemoveGesture(gesture);

        /// <inheritdoc/>
        public void UnregisterCommand(ICommand command, KeyGesture gesture) => listener.Commands.Remove(command, gesture);

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            Gestures.Update(deltaTime);
            Drag.Update(deltaTime);
            Touch.Update(deltaTime);
            Text.Update(deltaTime);
            Navigation.Update(deltaTime);
            Scroll.Update(deltaTime);
            Devices.Update(deltaTime);
        }

        private class KeyboardListener
        {
            private readonly IInputSystem input;

            private IKeyboardInput? keyboard;

            public KeyboardListener(IInputSystem inputSystem)
            {
                input = inputSystem;
            }

            public KeyCommandTable Commands { get; } = new();

            public void Initialize()
            {
                input.Events.Devices.DeviceConnected += OnDeviceConnected;
                ReconnectKeyboard();
            }

            private void OnDeviceConnected(object? sender, Data.GenericEventArgs<IInputDeviceListener> e)
            {
                if (e.Data is IKeyboardInput)
                {
                    ReconnectKeyboard();
                }
            }

            private void OnKeyDown(object? sender, Data.GenericEventArgs<Keys> e)
            {
                if (keyboard == null)
                    return;

                Commands.Dispatch(new KeyGesture(e.Data, keyboard.ModifierKeys));
            }

            private void ReconnectKeyboard()
            {
                if (keyboard != null)
                    keyboard.KeyDown -= OnKeyDown;
                keyboard = input.Keyboard;
                if (keyboard != null)
                    keyboard.KeyDown += OnKeyDown;
            }
        }
    }
}