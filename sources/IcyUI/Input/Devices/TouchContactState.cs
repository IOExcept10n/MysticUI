// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Devices
{
    /// <summary>
    /// Specifies the state of a <see cref="TouchContact"/> in one <see cref="ITouchInput.Contacts"/> snapshot.
    /// </summary>
    public enum TouchContactState
    {
        /// <summary>
        /// The contact touched down since the previous snapshot.
        /// </summary>
        Pressed,

        /// <summary>
        /// The contact is still down. It is reported in every snapshot, even when it didn't move.
        /// </summary>
        Moved,

        /// <summary>
        /// The contact lifted since the previous snapshot. It is reported exactly once and is absent afterwards.
        /// </summary>
        Released,
    }
}
