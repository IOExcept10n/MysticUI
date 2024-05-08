using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Input.Devices
{
    /// <summary>
    /// Represents an interface for input devices that manage event listening.
    /// </summary>
    public interface IInputDeviceListener
    {
        /// <summary>
        /// Determines whether the input device is currently listening for events.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the device is currently listening for events; otherwise, <see langword="false"/>.
        /// </value>
        bool IsListening { get; }

        /// <summary>
        /// Disables event listening for the device.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if event listening was successfully disabled; otherwise, <see langword="false"/>.
        /// </returns>
        bool DisableListening();

        /// <summary>
        /// Enables event listening for the device.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if event listening was successfully enabled; otherwise, <see langword="false"/>.
        /// </returns>
        bool EnableListening();
    }
}
