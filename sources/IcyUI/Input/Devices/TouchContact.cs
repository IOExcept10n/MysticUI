// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Input.Devices
{
    /// <summary>
    /// Describes one touch contact (finger) in an <see cref="ITouchInput.Contacts"/> snapshot.
    /// </summary>
    /// <param name="Id">The contact identifier, stable from its <see cref="TouchContactState.Pressed"/> to its <see cref="TouchContactState.Released"/> snapshot.</param>
    /// <param name="Position">The contact position, in physical (back-buffer) pixels.</param>
    /// <param name="State">The contact state in this snapshot.</param>
    public readonly record struct TouchContact(int Id, Point Position, TouchContactState State);
}
