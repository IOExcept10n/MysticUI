// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;
using AquaUI.Input;
using AquaUI.Input.Devices;
using Microsoft.Xna.Framework.Input.Touch;
using System.Drawing;

namespace AquaUI.MonoGame.Input.Devices
{
    internal class TouchInput : ITouchInput, IUpdateableInput
    {
        public bool IsListening { get; private set; }

        public bool IsInitialized { get; private set; }

        public event EventHandler<GenericEventArgs<Point>>? Tap;

        public event EventHandler<GenericEventArgs<Point>>? Hold;

        public event EventHandler<GenericEventArgs<TranslationInfo>>? Swipe;

        public event EventHandler<GenericEventArgs<TranslationInfo>>? Drag;

        public bool DisableListening()
        {
            IsListening = false;
            return true;
        }

        public bool EnableListening()
        {
            IsListening = true;
            return true;
        }

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
                    Drag?.Invoke(this, new TranslationInfo(gesture.Position.AsSystemPoint(),
                                                           gesture.Delta.AsSystemVector(),
                                                           gesture.Delta2.AsSystemVector(),
                                                           gesture.GestureType == GestureType.DragComplete));
                    break;

                case GestureType.Flick:
                    Swipe?.Invoke(this, new TranslationInfo(gesture.Position.AsSystemPoint(),
                                                            gesture.Delta.AsSystemVector(),
                                                            gesture.Delta2.AsSystemVector(),
                                                            true));
                    break;

                case GestureType.Hold:
                    Hold?.Invoke(this, gesture.Position.AsSystemPoint());
                    break;
            }
        }

        public void Initialize()
        {
            IsInitialized = true;
        }
    }
}