using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Windows.Input;
using Icy.Data;
using Icy.Input;
using Icy.Input.Clipboard;
using Icy.Input.Devices;
using Icy.Input.Events;

// These fakes must satisfy full interface contracts (ITouchEvents, IDragEvents, etc.) even though most tests only
// exercise a handful of the events - unused ones are still required members, not dead code.
#pragma warning disable CS0067

namespace Icy.Tests.Input
{
    /// <summary>
    /// A minimal <see cref="IInputSystem"/> test double - just enough to construct an <see cref="Icy.Configuration.IcyConfiguration"/>
    /// for <see cref="Icy.UI.Canvas"/> tests and to synthesize aggregated input events (as the real device layer would),
    /// without any real device polling.
    /// </summary>
    public sealed class FakeInputSystem : IInputSystem
    {
        public FakeInputSystem()
        {
            Events = new FakeInputEventSystem(this);
            Mouse = new FakeMouseInput();
            Keyboard = new FakeKeyboardInput();
        }

        public FakeInputEventSystem Events { get; }

        IInputEventSystem IInputSystem.Events => Events;

        public FakeKeyboardInput Keyboard { get; }

        IKeyboardInput? IInputSystem.Keyboard => Keyboard;

        public FakeMouseInput Mouse { get; }

        IMouseInput IInputSystem.Mouse => Mouse;

        public IGamepadInput? Gamepad => null;

        public ITouchInput? Touch => null;

        public IClipboard Clipboard => throw new NotSupportedException("Clipboard isn't needed by Canvas/hit-testing tests.");

        public bool IsInitialized { get; private set; }

        public void Initialize() => IsInitialized = true;

        public bool IsDeviceAvailable<T>()
            where T : IInputDeviceListener => false;

        public T GetInputDevice<T>()
            where T : IInputDeviceListener => throw new KeyNotFoundException();

        public bool TryGetInputDevice<T>([NotNullWhen(true)] out T? device)
            where T : IInputDeviceListener
        {
            device = default;
            return false;
        }

        public IEnumerator<IInputDeviceListener> GetEnumerator()
        {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Shared boilerplate for the fake per-category event providers (<see cref="IInputEventProvider"/>).
    /// </summary>
    public abstract class FakeInputEventProviderBase : IInputEventProvider
    {
        protected FakeInputEventProviderBase(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        public IInputSystem InputSystem { get; }

        public bool IsInitialized { get; private set; }

        public void Initialize() => IsInitialized = true;

        public void Update(TimeSpan deltaTime)
        {
        }
    }

    /// <summary>
    /// A fake <see cref="ITouchEvents"/> that lets tests synthesize tap/touch-down/touch-up payloads directly,
    /// mirroring how the real device layer would raise them.
    /// </summary>
    public sealed class FakeTouchEvents(IInputSystem inputSystem) : FakeInputEventProviderBase(inputSystem), ITouchEvents
    {
        public event EventHandler<GenericEventArgs<Point>>? Hold;

        public event EventHandler<GenericEventArgs<TouchInfo>>? Tap;

        public event EventHandler<GenericEventArgs<Point>>? TouchDown;

        public event EventHandler<GenericEventArgs<Point>>? TouchUp;

        public TimeSpan MaxMultiTapDelay { get; set; }

        public TimeSpan MinHoldDelay { get; set; }

        public float HoldAreaSize { get; set; }

        public void RaiseHold(Point point) => Hold?.Invoke(this, new GenericEventArgs<Point>(point));

        public void RaiseTap(TouchInfo info) => Tap?.Invoke(this, new GenericEventArgs<TouchInfo>(info));

        public void RaiseTouchDown(Point point) => TouchDown?.Invoke(this, new GenericEventArgs<Point>(point));

        public void RaiseTouchUp(Point point) => TouchUp?.Invoke(this, new GenericEventArgs<Point>(point));
    }

    /// <summary>
    /// A fake <see cref="IDragEvents"/> that lets tests synthesize drag-sequence payloads directly.
    /// </summary>
    public sealed class FakeDragEvents(IInputSystem inputSystem) : FakeInputEventProviderBase(inputSystem), IDragEvents
    {
        public event EventHandler<AcceptableEventArgs<Point>>? DragStarted;

        public event EventHandler<GenericEventArgs<Point>>? DragPerforming;

        public event EventHandler<GenericEventArgs<Point>>? DragEnded;

        public void OnMouseMove(Point lastCursorPosition)
        {
        }

        public void RaiseDragEnded(Point point) => DragEnded?.Invoke(this, new GenericEventArgs<Point>(point));

        public void RaiseDragPerforming(Point point) => DragPerforming?.Invoke(this, new GenericEventArgs<Point>(point));

        public void RaiseDragStarted(Point point) => DragStarted?.Invoke(this, new AcceptableEventArgs<Point> { Data = point });
    }

    /// <summary>
    /// A fake <see cref="IScrollEvents"/> - not exercised by Phase 5 tests yet, but needed to satisfy
    /// <see cref="IInputEventSystem.Scroll"/> so a full <see cref="FakeInputEventSystem"/> can be constructed.
    /// </summary>
    public sealed class FakeScrollEvents(IInputSystem inputSystem) : FakeInputEventProviderBase(inputSystem), IScrollEvents
    {
        public event EventHandler<GenericEventArgs<ScrollInfo>>? Scroll;

        public TimeSpan RepeatDelay { get; set; }

        public void RaiseScroll(ScrollInfo info) => Scroll?.Invoke(this, new GenericEventArgs<ScrollInfo>(info));
    }

    /// <summary>
    /// A fake <see cref="ITextInputEventInfo"/> for synthesizing <see cref="FakeTextEvents.RaiseTextInput"/> payloads.
    /// </summary>
    public sealed class FakeTextInputEventInfo(string text, TextInputEventType type = TextInputEventType.Input) : ITextInputEventInfo
    {
        public Range CompositionRange { get; init; }

        public string Text { get; } = text;

        public TextInputEventType Type { get; } = type;
    }

    /// <summary>
    /// A fake <see cref="ITextEvents"/> that lets tests synthesize typed-character payloads directly.
    /// </summary>
    public sealed class FakeTextEvents(IInputSystem inputSystem) : FakeInputEventProviderBase(inputSystem), ITextEvents
    {
        public event EventHandler<GenericEventArgs<ITextInputEventInfo>>? TextInput;

        public event EventHandler? CopyText;

        public event EventHandler? CutText;

        public event EventHandler? PasteText;

        public TimeSpan RepeatDelay { get; set; }

        public TimeSpan RepeatStartDelay { get; set; }

        public bool IsTextInputEnabled { get; private set; }

        public void EnableTextInput() => IsTextInputEnabled = true;

        public void DisableTextInput() => IsTextInputEnabled = false;

        public void RaiseTextInput(string text) => TextInput?.Invoke(this, new GenericEventArgs<ITextInputEventInfo>(new FakeTextInputEventInfo(text)));
    }

    /// <summary>
    /// A fake <see cref="INavigationEvents"/> that lets tests synthesize focus-navigation payloads directly.
    /// </summary>
    public sealed class FakeNavigationEvents(IInputSystem inputSystem) : FakeInputEventProviderBase(inputSystem), INavigationEvents
    {
        public event EventHandler? NavigateBack;

        public event EventHandler? NavigateForward;

        public event EventHandler? CloseModal;

        public event EventHandler? SelectElement;

        public event EventHandler<AcceptableEventArgs<Keys>>? AltKeyNavigation;

        public event EventHandler<AcceptableEventArgs<Vector2>>? FocusChanging;

        public event EventHandler? FocusNext;

        public event EventHandler? FocusPrevious;

        public TimeSpan RepeatDelay { get; set; }

        public TimeSpan RepeatStartDelay { get; set; }

        public float MinimalFocusChangeDistance { get; set; }

        public void RaiseFocusNext() => FocusNext?.Invoke(this, EventArgs.Empty);

        public void RaiseFocusPrevious() => FocusPrevious?.Invoke(this, EventArgs.Empty);

        public void RaiseCloseModal() => CloseModal?.Invoke(this, EventArgs.Empty);

        public void RaiseSelectElement() => SelectElement?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A fake <see cref="IDeviceEvents"/> - not exercised by Phase 5 tests yet, but needed to satisfy
    /// <see cref="IInputEventSystem.Devices"/> so a full <see cref="FakeInputEventSystem"/> can be constructed.
    /// </summary>
    public sealed class FakeDeviceEvents(IInputSystem inputSystem) : FakeInputEventProviderBase(inputSystem), IDeviceEvents
    {
        public event EventHandler<GenericEventArgs<IInputDeviceListener>> DeviceConnected = delegate { };

        public event EventHandler<GenericEventArgs<IInputDeviceListener>> DeviceDisconnected = delegate { };
    }

    /// <summary>
    /// A fake <see cref="IMouseInput"/> whose <see cref="MouseInfo"/> tests can set directly to simulate cursor movement.
    /// </summary>
    public sealed class FakeMouseInput : IMouseInput
    {
        public bool IsListening => true;

        public bool IsInitialized { get; private set; }

        public MouseInfo MouseInfo { get; set; }

        public event EventHandler<GenericEventArgs<MouseButtons>>? MouseButtonPressed;

        public event EventHandler<GenericEventArgs<MouseButtons>>? MouseButtonReleased;

        public void Initialize() => IsInitialized = true;

        public bool DisableListening() => true;

        public bool EnableListening() => true;
    }

    /// <summary>
    /// A fake <see cref="IKeyboardInput"/> that lets tests synthesize key-press payloads directly.
    /// </summary>
    public sealed class FakeKeyboardInput : IKeyboardInput
    {
        public bool IsListening => true;

        public bool IsInitialized { get; private set; }

        public IEnumerable<Keys> KeysDown => [];

        public ModifierKeys ModifierKeys => ModifierKeys.None;

        public event EventHandler<GenericEventArgs<Keys>>? KeyDown;

        public event EventHandler<GenericEventArgs<Keys>>? KeyUp;

        public void Initialize() => IsInitialized = true;

        public bool DisableListening() => true;

        public bool EnableListening() => true;

        public void RaiseKeyDown(Keys key) => KeyDown?.Invoke(this, new GenericEventArgs<Keys>(key));

        public void RaiseKeyUp(Keys key) => KeyUp?.Invoke(this, new GenericEventArgs<Keys>(key));
    }

    /// <summary>
    /// A fake <see cref="IInputEventSystem"/> aggregating fake per-category event providers, exposing the concrete
    /// fake types (not just their interfaces) so tests can raise events directly - e.g. <c>Events.Touch.RaiseTap(...)</c>.
    /// </summary>
    public sealed class FakeInputEventSystem : IInputEventSystem
    {
        public FakeInputEventSystem(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
            Drag = new FakeDragEvents(inputSystem);
            Touch = new FakeTouchEvents(inputSystem);
            Text = new FakeTextEvents(inputSystem);
            Navigation = new FakeNavigationEvents(inputSystem);
            Scroll = new FakeScrollEvents(inputSystem);
            Devices = new FakeDeviceEvents(inputSystem);
        }

        public IInputSystem InputSystem { get; }

        public FakeDragEvents Drag { get; }

        IDragEvents IInputEventSystem.Drag => Drag;

        public FakeTouchEvents Touch { get; }

        ITouchEvents IInputEventSystem.Touch => Touch;

        public FakeTextEvents Text { get; }

        ITextEvents IInputEventSystem.Text => Text;

        public FakeNavigationEvents Navigation { get; }

        INavigationEvents IInputEventSystem.Navigation => Navigation;

        public FakeScrollEvents Scroll { get; }

        IScrollEvents IInputEventSystem.Scroll => Scroll;

        public FakeDeviceEvents Devices { get; }

        IDeviceEvents IInputEventSystem.Devices => Devices;

        public bool IsInitialized { get; private set; }

        public void Initialize() => IsInitialized = true;

        public void Update(TimeSpan deltaTime)
        {
        }

        public void RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null)
        {
        }

        public void UnregisterCommand(KeyGesture gesture)
        {
        }
    }
}
