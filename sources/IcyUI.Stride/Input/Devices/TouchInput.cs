// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;

namespace Icy.Stride.Input.Devices
{
    /// <summary>
    /// Stride touch device listener, backed by the shared <see cref="Stride.Input.InputManager"/>.
    /// </summary>
    /// <remarks>
    /// Unlike MonoGame (which delegates gesture recognition to <c>TouchPanel.ReadGesture()</c>), Stride's
    /// <see cref="Stride.Input.InputManager"/> only exposes raw pointer down/move/up events - it has no built-in
    /// gesture recognizer. Per the v1 scope decision, actual gesture recognition (tap/hold/swipe/drag) is deferred;
    /// this class exists so the <see cref="ITouchInput"/> contract is implemented and wired into
    /// <see cref="Stride.Input.Devices.InputSystem"/> now, ready for that work later without further architecture
    /// changes - none of its events are raised yet.
    /// </remarks>
    internal class TouchInput : ITouchInput, IUpdateableInput
    {
        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<TranslationInfo>>? Drag;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? Hold;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<TranslationInfo>>? Swipe;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? Tap;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; private set; }

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
            // Gesture recognition (tap/hold/swipe/drag) from Stride's raw pointer events is deferred - see
            // the remarks on this class. Silencing unused-event-declaration warnings until that work lands:
            _ = Drag;
            _ = Hold;
            _ = Swipe;
            _ = Tap;
        }
    }
}
