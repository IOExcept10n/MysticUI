// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;
using Icy.Input.Gestures;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation of the <see cref="ITouchEvents"/> interface: forwards the presses, taps and
    /// holds recognized by <see cref="IGestureEvents"/> for touch and the left and right mouse buttons.
    /// </summary>
    internal class TouchEvents : ITouchEvents
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TouchEvents"/> class.
        /// </summary>
        /// <param name="inputSystem">The input system whose gestures are forwarded.</param>
        public TouchEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? Hold;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<TouchInfo>>? Tap;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? TouchDown;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? TouchUp;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            IGestureEvents gestures = InputSystem.Events.Gestures;
            gestures.PointerPressed += OnPointerPressed;
            gestures.PointerReleased += OnPointerReleased;
            gestures.Tapped += OnTapped;
            gestures.Held += OnHeld;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
        }

        // Middle mouse only pans; it never presses, taps or holds controls.
        private static bool IsTouchLike(PointerKind kind) => kind != PointerKind.MouseMiddle;

        private void OnPointerPressed(object? sender, GenericEventArgs<PointerInfo> e)
        {
            if (IsTouchLike(e.Data.Kind))
                TouchDown?.Invoke(this, e.Data.Position);
        }

        private void OnPointerReleased(object? sender, GenericEventArgs<PointerInfo> e)
        {
            if (IsTouchLike(e.Data.Kind))
                TouchUp?.Invoke(this, e.Data.Position);
        }

        private void OnTapped(object? sender, GenericEventArgs<TapInfo> e) =>
            Tap?.Invoke(this, new TouchInfo(e.Data.Position, e.Data.Count));

        private void OnHeld(object? sender, GenericEventArgs<PointerInfo> e) =>
            Hold?.Invoke(this, e.Data.Position);
    }
}
