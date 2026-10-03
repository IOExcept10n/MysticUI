// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;
using Icy.Input.Gestures;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IDragEvents"/> interface: forwards the touch and left-mouse
    /// drags recognized by <see cref="IGestureEvents"/>.
    /// </summary>
    internal class DragEvens : IDragEvents
    {
        private bool active;

        /// <summary>
        /// Initializes a new instance of the <see cref="DragEvens"/> class.
        /// </summary>
        /// <param name="inputSystem">The input system whose gestures are forwarded.</param>
        public DragEvens(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragEnded;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragCanceled;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragPerforming;

        /// <inheritdoc/>
        public event EventHandler<AcceptableEventArgs<Point>>? DragStarted;

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
            gestures.DragStarted += OnGestureDragStarted;
            gestures.DragMoved += OnGestureDragMoved;
            gestures.DragCompleted += OnGestureDragCompleted;
            gestures.DragCanceled += OnGestureDragCanceled;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
        }

        // Middle-mouse drags are pans for scrolling content (see PointerKind.MouseMiddle), never control drags.
        private static bool IsControlDrag(PointerKind kind) => kind is PointerKind.Touch or PointerKind.MouseLeft;

        private void OnGestureDragStarted(object? sender, AcceptableEventArgs<DragInfo> e)
        {
            if (!IsControlDrag(e.Data.Kind))
                return;

            var args = new AcceptableEventArgs<Point> { Data = e.Data.Start };
            DragStarted?.Invoke(this, args);
            e.Cancel = args.Cancel;
            e.Handled = args.Handled;
            active = !args.Cancel;
        }

        private void OnGestureDragMoved(object? sender, GenericEventArgs<DragInfo> e)
        {
            if (active && IsControlDrag(e.Data.Kind))
                DragPerforming?.Invoke(this, e.Data.Position);
        }

        private void OnGestureDragCompleted(object? sender, GenericEventArgs<DragInfo> e)
        {
            if (!active || !IsControlDrag(e.Data.Kind))
                return;
            active = false;
            DragEnded?.Invoke(this, e.Data.Position);
        }

        private void OnGestureDragCanceled(object? sender, GenericEventArgs<DragInfo> e)
        {
            if (!active || !IsControlDrag(e.Data.Kind))
                return;
            active = false;
            DragCanceled?.Invoke(this, e.Data.Position);
        }
    }
}
