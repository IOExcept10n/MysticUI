// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;

namespace Icy.Input.Devices
{
    /// <summary>
    /// Represents a touch device listener that reports raw touch contacts.
    /// </summary>
    public interface ITouchInput : IInputDeviceListener, IInitializable
    {
        /// <summary>
        /// Gets the touch contacts of the current input update.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The snapshot is refreshed once per input update. Every active contact appears in every snapshot - as
        /// <see cref="TouchContactState.Moved"/> when it didn't move - until the single snapshot that reports it
        /// <see cref="TouchContactState.Released"/>. A tracked contact missing from a snapshot is treated as canceled.
        /// </para>
        /// <para>
        /// Gestures (tap, hold, drag, pinch) are recognized in core from this snapshot - see
        /// <see cref="Gestures.IGestureEvents"/>.
        /// </para>
        /// </remarks>
        IReadOnlyList<TouchContact> Contacts { get; }
    }
}
