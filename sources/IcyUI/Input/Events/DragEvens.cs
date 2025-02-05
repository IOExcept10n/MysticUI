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
        private Point lastMousePosition;
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
                var state = InputSystem.Mouse.MouseInfo;
                var delta = new Point(state.Position.X - lastMousePosition.X, state.Position.Y - lastMousePosition.Y);
                lastMousePosition = state.Position;
                DragPerforming?.Invoke(this, delta);
            }
        }

        private void BeginDrag(Point position)
        {
            var args = new AcceptableEventArgs<Point>() { Data = position };
            DragStarted?.Invoke(this, args);
            if (args.Cancel)
                return;
            lastMousePosition = position;
        }

        private void EndDrag(Point position)
        {
            state = DragState.None;
            lastMousePosition = default;
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
                DragPerforming?.Invoke(this, e.Data.DeltaTranslation.ToPoint());
            }
            else
            {
                EndDrag((e.Data.TranslationStart.ToVector() + e.Data.TotalTranslation).ToPoint());
            }
        }
    }
}
