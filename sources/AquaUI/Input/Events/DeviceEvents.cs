// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;
using AquaUI.Input.Devices;

namespace AquaUI.Input.Events
{
    /// <summary>
    /// Represents a default implementation for the <see cref="IDeviceEvents"/> interface.
    /// </summary>
    /// <remarks>
    /// Note that device connection events are related to engine so this class is just a placeholder and events here are not triggered at all.
    /// </remarks>
    internal class DeviceEvents : IDeviceEvents
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceEvents"/> class.
        /// </summary>
        /// <param name="inputSystem">An instance of the <see cref="IInputSystem"/> to initialize an object.</param>
        public DeviceEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<IInputDeviceListener>>? DeviceConnected;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<IInputDeviceListener>>? DeviceDisconnected;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            IsInitialized = true;
        }
    }
}