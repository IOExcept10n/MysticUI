// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;
using Icy.Rendering;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IDragEvents"/> interface.
    /// </summary>
    internal class DragEvens : IDragEvents
    {
        private DragState state;

        /// <summary>
        /// Initializes a new instance of the <see cref="DragEvens"/> class.
        /// </summary>
        /// <param name="inputSystem">An instance of the <see cref="IInputSystem"/> for this event processor.</param>
        public DragEvens(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragEnded;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragPerforming;

        /// <inheritdoc/>
        public event EventHandler<AcceptableEventArgs<Point>>? DragStarted;

        private enum DragState
        {
            None,
            MouseDrag,
            TouchDrag,
        }

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            if (InputSystem.Touch != null)
            {
                InputSystem.Touch.Drag += Touch_Drag;
            }

            InputSystem.Mouse.MouseButtonReleased += Mouse_MouseButtonReleased;
        }

        /// <inheritdoc/>
        public void OnMouseMove(Point lastCursorPosition)
        {
            if (state == DragState.None)
            {
                BeginDrag(lastCursorPosition);
                state = DragState.MouseDrag;
            }
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            if (state == DragState.MouseDrag)
            {
                // DragPerforming's documented contract (see UIElement.OnDragPerforming) is "the drag's current
                // position, in screen/window space" - the same absolute-position contract DragStarted/DragEnded
                // already follow. This used to pass the incremental delta since the last call instead, which
                // consumers like Slider.UpdateValueFromPoint (via PointToLocal, which expects a real screen
                // coordinate) treated as an absolute position - producing a near-random result every frame instead
                // of tracking the cursor, i.e. dragging would start correctly but then appear to "fail" instantly.
                DragPerforming?.Invoke(this, InputSystem.Mouse.MouseInfo.Position);
            }
        }

        private void BeginDrag(Point position)
        {
            var args = new AcceptableEventArgs<Point>() { Data = position };
            DragStarted?.Invoke(this, args);
        }

        private void EndDrag(Point position)
        {
            state = DragState.None;
            DragEnded?.Invoke(this, position);
        }

        private void Mouse_MouseButtonReleased(object? sender, GenericEventArgs<Devices.MouseButtons> e)
        {
            if (state == DragState.MouseDrag)
            {
                EndDrag(InputSystem.Mouse.MouseInfo.Position);
            }
        }

        private void Touch_Drag(object? sender, GenericEventArgs<Devices.TranslationInfo> e)
        {
            if (state == DragState.None)
            {
                BeginDrag(e.Data.TranslationStart);
                state = DragState.TouchDrag;
            }
            else if (!e.Data.IsPerformed)
            {
                // Same absolute-position contract as the mouse path above - TranslationStart + TotalTranslation
                // is the gesture's current absolute position (mirrors EndDrag's own computation below).
                DragPerforming?.Invoke(this, (e.Data.TranslationStart.ToVector() + e.Data.TotalTranslation).ToPoint());
            }
            else
            {
                EndDrag((e.Data.TranslationStart.ToVector() + e.Data.TotalTranslation).ToPoint());
            }
        }
    }
}
