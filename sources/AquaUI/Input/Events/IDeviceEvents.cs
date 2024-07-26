// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;
using AquaUI.Input.Devices;

namespace AquaUI.Input.Events
{
    /// <summary>
    /// Represents an interface for the input device events listener.
    /// </summary>
    public interface IDeviceEvents : IInputEventProvider
    {
        /// <summary>
        /// Occurs when the input device is connected.
        /// </summary>
        event EventHandler<GenericEventArgs<IInputDeviceListener>> DeviceConnected;

        /// <summary>
        /// Occurs when the input device is disconnected.
        /// </summary>
        event EventHandler<GenericEventArgs<IInputDeviceListener>> DeviceDisconnected;
    }
}