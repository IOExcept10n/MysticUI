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