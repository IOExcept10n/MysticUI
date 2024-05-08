using System.Drawing;
using System.Numerics;
using AquaUI.Data;
using AquaUI.Input.Devices;
using AquaUI.Input.Events;

namespace AquaUI.Input
{
    internal class MouseEvents : ITouchEvents, IDragEvents, IScrollEvents, IUpdateable
    {
        private readonly IMouseInput mouse;
        private readonly ITouchInput touch;

        private int touchCount;
        private TimeSpan touchTime;
        private bool isDragging;
        private bool isMouseDown;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public Point CursorPosition => mouse.MouseInfo.Position;

        /// <inheritdoc/>
        public TimeSpan MaxMultiTapDelay { get; set; } = TimeSpan.FromSeconds(0.5);

        /// <inheritdoc/>
        public float HoldAreaSize { get; set; } = 10;

        public MouseEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
            mouse = inputSystem.Mouse;
            mouse.MouseButtonPressed += Mouse_MouseButtonPressed;
            mouse.MouseButtonReleased += Mouse_MouseButtonReleased;
            touch = inputSystem.Touch;
            touch.Tap += Touch_Tap;
            touch.Drag += Touch_Drag;
            touch.Hold += Touch_Hold;
            touch.Flick += Touch_Flick;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>> TouchDown;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>> TouchUp;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<TouchInfo>> Tap;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>> Hold;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>> DragStarted;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>> DragPerforming;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>> DragEnded;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<ScrollInfo>> Scroll;

        /// <inheritdoc/>
        public void Update(TimeSpan elapsedTime)
        {
            // TODO: perform input events handling.
        }

        private void Touch_Tap(object? sender, GenericEventArgs<Point> e)
        {
            Tap?.Invoke(this, new TouchInfo(e.Data, ++touchCount));
        }

        private void Touch_Flick(object? sender, GenericEventArgs<TranslationInfo> e)
        {
            Scroll?.Invoke(this, new ScrollInfo(e.Data.DeltaTranslation.X, Controls.Orientation.Horizontal));
            Scroll?.Invoke(this, new ScrollInfo(e.Data.DeltaTranslation.Y, Controls.Orientation.Vertical));
        }

        private void Touch_Hold(object? sender, GenericEventArgs<Point> e)
        {
            touchCount = 0;
            Hold?.Invoke(this, CursorPosition);
        }

        private void Touch_Drag(object? sender, GenericEventArgs<TranslationInfo> e)
        {
            if (!isDragging)
            {
                isDragging = true;
                DragStarted?.Invoke(this, CursorPosition);
            }

            if (e.Data.IsPerformed)
            {
                isDragging = false;
                DragEnded?.Invoke(this, CursorPosition);
                return;
            }

            DragPerforming?.Invoke(this, CursorPosition);
        }

        private void Mouse_MouseButtonReleased(object? sender, GenericEventArgs<MouseButtons> e)
        {
            if ((e.Data & MouseButtons.LeftButton) == MouseButtons.LeftButton)
            {
                isMouseDown = false;
                TouchUp?.Invoke(this, CursorPosition);
            }
        }

        private void Mouse_MouseButtonPressed(object? sender, GenericEventArgs<MouseButtons> e)
        {
            if ((e.Data & MouseButtons.LeftButton) == MouseButtons.LeftButton)
            {
                isMouseDown = true;
                TouchDown?.Invoke(this, CursorPosition);
            }
        }
    }

    internal class TextEvents : ITextEvents, IUpdateable
    {
        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        public TextEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<ITextInputEvent>> TextInput;

        /// <inheritdoc/>
        public void Update(TimeSpan elapsedTime)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void EnableTextInput()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void DisableTextInput()
        {
            throw new NotImplementedException();
        }
    }

    internal class NavigationEvents : INavigationEvents, IUpdateable
    {
        public IInputSystem InputSystem { get; }

        public NavigationEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler NavigateBack;

        /// <inheritdoc/>
        public event EventHandler NavigateForward;

        /// <inheritdoc/>
        public event EventHandler CloseModal;

        /// <inheritdoc/>
        public event EventHandler SelectElement;

        /// <inheritdoc/>
        public event EventHandler<AcceptableEventArgs<Vector2>> FocusChanging;

        /// <inheritdoc/>
        public event EventHandler FocusNext;

        /// <inheritdoc/>
        public event EventHandler FocusPrevious;

        /// <inheritdoc/>
        public void Update(TimeSpan elapsedTime)
        {
            throw new NotImplementedException();
        }
    }

    internal class DeviceEvents : IDeviceEvents, IUpdateable
    {
        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        public DeviceEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<IInputDeviceListener>> DeviceConnected;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<IInputDeviceListener>> DeviceDisconnected;

        /// <inheritdoc/>
        public void Update(TimeSpan elapsedTime)
        {
            throw new NotImplementedException();
        }
    }
}