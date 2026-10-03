// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Input;
using Icy.Input.Devices;
using TInputManager = Stride.Input.InputManager;
using TMouseDevice = Stride.Input.IMouseDevice;
using TPointerEvent = Stride.Input.PointerEvent;
using TPointerEventType = Stride.Input.PointerEventType;

namespace Icy.Stride.Input.Devices
{
    /// <summary>
    /// Stride touch device listener: folds <see cref="TInputManager.PointerEvents"/> into raw contacts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stride reports pointer <em>events</em>, while <see cref="ITouchInput.Contacts"/> is a snapshot that lists every active
    /// contact each frame. This listener therefore keeps its own set of active contacts: an event-less contact is reported
    /// as <see cref="TouchContactState.Moved"/> at its last position. Mouse pointers are skipped, because the mouse comes
    /// through <see cref="MouseInput"/>.
    /// </para>
    /// <para>
    /// <see cref="TPointerEventType.Canceled"/> removes the contact without a <see cref="TouchContactState.Released"/>;
    /// core recognition treats the missing contact as canceled.
    /// </para>
    /// <para>
    /// Positions are mapped onto the back buffer through <see cref="PointerSurfaceMapping"/>. In fullscreen, Stride normalizes
    /// touches over the whole physical panel but still reports the back-buffer size as the pointer surface, while the back
    /// buffer is presented aspect-fit with black bars. <paramref name="touchSurfaceSize"/> supplies the real panel size in
    /// that case; when it returns <see langword="null"/>, Stride's reported surface is used, which is correct in windowed
    /// mode. Without a viewport source the raw absolute position is used.
    /// </para>
    /// </remarks>
    /// <param name="input">The Stride input manager to read pointer events from.</param>
    /// <param name="viewportSize">Returns the back-buffer size, or <see langword="null"/> to report raw absolute positions.</param>
    /// <param name="touchSurfaceSize">Returns the surface touches are normalized over, or <see langword="null"/> to use Stride's reported pointer surface.</param>
    internal class TouchInput(TInputManager input, Func<Size>? viewportSize = null, Func<Size?>? touchSurfaceSize = null) : ITouchInput, IUpdateableInput
    {
        private readonly Dictionary<int, Point> active = [];
        private readonly List<TouchContact> contacts = [];
        private Dictionary<int, Point> carriedReleases = [];
        private Dictionary<int, Point> pendingReleases = [];

        /// <inheritdoc/>
        public IReadOnlyList<TouchContact> Contacts => contacts;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; private set; } = true;

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
            contacts.Clear();
            if (!IsListening)
            {
                active.Clear();
                return;
            }

            var pressed = new HashSet<int>();
            var released = new Dictionary<int, Point>();
            foreach (var pointerEvent in input.PointerEvents)
            {
                if (pointerEvent.Pointer is TMouseDevice)
                    continue;

                int id = pointerEvent.PointerId;
                Point position = ToViewport(pointerEvent);
                switch (pointerEvent.EventType)
                {
                    case TPointerEventType.Pressed:
                        active[id] = position;
                        pressed.Add(id);
                        break;

                    case TPointerEventType.Moved:
                        if (active.ContainsKey(id))
                            active[id] = position;
                        break;

                    case TPointerEventType.Released:
                        if (active.Remove(id))
                            released[id] = position;
                        break;

                    case TPointerEventType.Canceled:
                        active.Remove(id);
                        pressed.Remove(id);
                        break;
                }
            }

            // Stride may never deliver Released/Canceled for a contact (focus loss, a lift outside the window). A stuck
            // contact would keep touch "active" forever - and with it, suppress the mouse - so when no touch device reports
            // any pointer down, the lingering contacts are dropped; core recognition then treats them as canceled. Contacts
            // pressed this frame are kept in case the device state lags its events by a frame.
            if (active.Count > 0 && !IsAnyTouchPointerDown())
            {
                foreach (int id in active.Keys.Where(id => !pressed.Contains(id)).ToList())
                    active.Remove(id);
            }

            foreach (var (id, position) in active)
                contacts.Add(new TouchContact(id, position, pressed.Contains(id) ? TouchContactState.Pressed : TouchContactState.Moved));

            // A press and release within one frame: report the press now and keep the release for the next snapshot, so the
            // tap isn't lost.
            foreach (var (id, position) in released)
            {
                if (pressed.Contains(id))
                {
                    contacts.Add(new TouchContact(id, position, TouchContactState.Pressed));
                    pendingReleases[id] = position;
                }
                else
                {
                    contacts.Add(new TouchContact(id, position, TouchContactState.Released));
                }
            }

            foreach (var (id, position) in carriedReleases)
                contacts.Add(new TouchContact(id, position, TouchContactState.Released));
            carriedReleases.Clear();
            (carriedReleases, pendingReleases) = (pendingReleases, carriedReleases);
        }

        private Point ToViewport(TPointerEvent pointerEvent)
        {
            Size viewport = viewportSize?.Invoke() ?? Size.Empty;
            if (viewport.Width <= 0 || viewport.Height <= 0)
                return new Point((int)pointerEvent.AbsolutePosition.X, (int)pointerEvent.AbsolutePosition.Y);

            SizeF surface = touchSurfaceSize?.Invoke() is { } panel
                ? panel
                : new SizeF(pointerEvent.Pointer.SurfaceSize.X, pointerEvent.Pointer.SurfaceSize.Y);
            return PointerSurfaceMapping.ToViewport(
                new System.Numerics.Vector2(pointerEvent.Position.X, pointerEvent.Position.Y),
                surface,
                viewport);
        }

        private bool IsAnyTouchPointerDown()
        {
            foreach (var device in input.Pointers)
            {
                if (device is not TMouseDevice && device.DownPointers.Count > 0)
                    return true;
            }

            return false;
        }
    }
}
