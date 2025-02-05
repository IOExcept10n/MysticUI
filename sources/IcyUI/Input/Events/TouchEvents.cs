using System.Drawing;
using System.Runtime.CompilerServices;
using Icy.Data;
using Icy.Input.Devices;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation of the <see cref="ITouchEvents"/> interface.
    /// </summary>
    internal class TouchEvents : ITouchEvents
    {
        private int touchCount;
        private TimeSpan delay;
        private bool isHolding;
        private Point? pressCursorPosition;

        /// <summary>
        /// Initializes a new instance of the <see cref="TouchEvents"/> class.
        /// </summary>
        /// <param name="inputSystem">An instance of the <see cref="IInputSystem"/> for this event processor.</param>
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
        public TimeSpan MaxMultiTapDelay { get; set; } = TimeSpan.FromSeconds(0.3);

        /// <inheritdoc/>
        public TimeSpan MinHoldDelay { get; set; } = TimeSpan.FromSeconds(0.5);

        /// <inheritdoc/>
        public float HoldAreaSize { get; set; } = 10;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            delay += deltaTime;

            // If the mouse button is pressed
            if (pressCursorPosition.HasValue)
            {
                if (delay >= MinHoldDelay)
                {
                    // We suppose this is hold.
                    touchCount = 0;
                    isHolding = true;
                }

                var position = InputSystem.Mouse.MouseInfo.Position;
                if (DistanceSquared(position, pressCursorPosition.Value) > HoldAreaSize * HoldAreaSize)
                {
                    InputSystem.Events.Drag.OnMouseMove(pressCursorPosition.Value);
                    pressCursorPosition = default;
                    isHolding = false;
                    delay = default;
                    touchCount = 0;
                }
            }
            else
            {
                if (delay >= MaxMultiTapDelay)
                {
                    touchCount = 0;
                }
            }
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            if (InputSystem.Touch != null)
            {
                InputSystem.Touch.Tap += Touch_Tap;
                InputSystem.Touch.Hold += Touch_Hold;
            }

            InputSystem.Mouse.MouseButtonPressed += Mouse_MouseButtonPressed;
            InputSystem.Mouse.MouseButtonReleased += Mouse_MouseButtonReleased;

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DistanceSquared(Point p1, Point p2)
        {
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;
            return (dx * dx) + (dy * dy);
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IMouseInput mouse:
                    mouse.MouseButtonPressed -= Mouse_MouseButtonPressed;
                    mouse.MouseButtonReleased -= Mouse_MouseButtonReleased;
                    break;
                case ITouchInput touch:
                    touch.Tap -= Touch_Tap;
                    touch.Hold -= Touch_Hold;
                    break;
            }
        }

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IMouseInput mouse:
                    mouse.MouseButtonPressed += Mouse_MouseButtonPressed;
                    mouse.MouseButtonReleased += Mouse_MouseButtonReleased;
                    break;
                case ITouchInput touch:
                    touch.Tap += Touch_Tap;
                    touch.Hold += Touch_Hold;
                    break;
            }
        }

        private void Mouse_MouseButtonReleased(object? sender, GenericEventArgs<MouseButtons> e)
        {
            if (sender is not IMouseInput mouse || (e.Data != MouseButtons.LeftButton && e.Data != MouseButtons.RightButton))
                return;
            TouchUp?.Invoke(this, mouse.MouseInfo.Position);
            if (pressCursorPosition == null) return;
            delay = default;
            pressCursorPosition = null;
            if (isHolding || e.Data == MouseButtons.RightButton)
            {
                isHolding = false;
                Hold?.Invoke(this, mouse.MouseInfo.Position);
            }
            else
            {
                Tap?.Invoke(this, new TouchInfo(mouse.MouseInfo.Position, ++touchCount));
            }
        }

        private void Mouse_MouseButtonPressed(object? sender, GenericEventArgs<MouseButtons> e)
        {
            if (sender is not IMouseInput mouse)
                return;
            delay = default;
            if (e.Data == MouseButtons.LeftButton || e.Data == MouseButtons.RightButton)
                pressCursorPosition = mouse.MouseInfo.Position;
            TouchDown?.Invoke(this, mouse.MouseInfo.Position);
        }

        private void Touch_Tap(object? sender, GenericEventArgs<Point> e)
        {
            delay = default;
            Tap?.Invoke(this, new TouchInfo(e.Data, ++touchCount));
        }

        private void Touch_Hold(object? sender, GenericEventArgs<Point> e)
        {
            delay = default;
            Hold?.Invoke(this, e.Data);
        }
    }
}
