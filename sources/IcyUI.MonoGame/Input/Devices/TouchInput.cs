// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using Microsoft.Xna.Framework.Input.Touch;

namespace Icy.MonoGame.Input.Devices
{
    /// <summary>
    /// MonoGame touch events listener.
    /// </summary>
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
            if (!IsListening) return;
            var gesture = TouchPanel.ReadGesture();
            switch (gesture.GestureType)
            {
                case GestureType.Tap:
                    Tap?.Invoke(this, gesture.Position.AsSystemPoint());
                    break;

                case GestureType.FreeDrag:
                case GestureType.DragComplete:
                    Drag?.Invoke(
                        this,
                        new TranslationInfo(
                            gesture.Position.AsSystemPoint(),
                            gesture.Delta.AsSystemVector(),
                            gesture.Delta2.AsSystemVector(),
                            gesture.GestureType == GestureType.DragComplete));
                    break;

                case GestureType.Flick:
                    Swipe?.Invoke(
                        this,
                        new TranslationInfo(
                            gesture.Position.AsSystemPoint(),
                            gesture.Delta.AsSystemVector(),
                            gesture.Delta2.AsSystemVector(),
                            true));
                    break;

                case GestureType.Hold:
                    Hold?.Invoke(this, gesture.Position.AsSystemPoint());
                    break;
            }
        }
    }
}