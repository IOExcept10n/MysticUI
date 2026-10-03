// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Input;
using Icy.Input.Devices;
using Microsoft.Xna.Framework.Input.Touch;

namespace Icy.MonoGame.Input.Devices
{
    /// <summary>
    /// MonoGame touch device listener: reports raw contacts from <see cref="TouchPanel.GetState()"/>.
    /// </summary>
    /// <remarks>
    /// Gestures are recognized in core (<see cref="Icy.Input.Gestures.IGestureEvents"/>), not by XNA's gesture recognizer, so
    /// <see cref="TouchPanel.EnabledGestures"/> is left unused. <see cref="TouchPanel.GetState()"/> reports every active
    /// contact each frame, which satisfies <see cref="ITouchInput.Contacts"/>'s snapshot contract directly.
    /// </remarks>
    internal class TouchInput : ITouchInput, IUpdateableInput
    {
        private readonly List<TouchContact> contacts = [];

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
                return;

            foreach (TouchLocation location in TouchPanel.GetState())
            {
                TouchContactState? state = location.State switch
                {
                    TouchLocationState.Pressed => TouchContactState.Pressed,
                    TouchLocationState.Moved => TouchContactState.Moved,
                    TouchLocationState.Released => TouchContactState.Released,
                    _ => null,
                };

                if (state is { } contactState)
                    contacts.Add(new TouchContact(location.Id, new Point((int)location.Position.X, (int)location.Position.Y), contactState));
            }
        }
    }
}
