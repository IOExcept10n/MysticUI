using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace AquaUI.Input.Devices
{

#pragma warning disable CS1574
    /// <summary>
    /// Describes the state of a typical gamepad.
    /// </summary>
    /// <remarks>
    /// This struct uses the same definition as in <see cref="Stride.Engine.Input.GamePadState"/>.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
#pragma warning restore CS1574
    public readonly struct GamePadState : IEquatable<GamePadState>, IEqualityOperators<GamePadState, GamePadState, bool>
    {
        /// <summary>
        /// Bitmask of the gamepad buttons.
        /// </summary>
        public readonly GamePadButtons Buttons;

        /// <summary>
        /// Left thumbstick x-axis/y-axis value. The value is in the range [-1.0f, 1.0f] for both axis.
        /// </summary>
        public readonly Vector2 LeftThumb;

        /// <summary>
        /// Right thumbstick x-axis/y-axis value. The value is in the range [-1.0f, 1.0f] for both axis.
        /// </summary>
        public readonly Vector2 RightThumb;

        /// <summary>
        /// The left trigger analog control in the range [0, 1.0f]. See remarks.
        /// </summary>
        /// <remarks>
        /// Some controllers are not supporting the range of value and may act as a simple button returning only 0 or 1.
        /// </remarks>
        public readonly float LeftTrigger;

        /// <summary>
        /// The right trigger analog control in the range [0, 1.0f]. See remarks.
        /// </summary>
        /// <remarks>
        /// Some controllers are not supporting the range of value and may act as a simple button returning only 0 or 1.
        /// </remarks>
        public readonly float RightTrigger;

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(GamePadState other)
        {
            return Buttons == other.Buttons && 
                LeftThumb == other.LeftThumb && 
                RightThumb == other.RightThumb && 
                LeftTrigger == other.LeftTrigger && 
                RightTrigger == other.RightTrigger;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is GamePadState state && Equals(state);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Buttons, LeftThumb, RightThumb, LeftTrigger, RightTrigger);
        }

        /// <inheritdoc/>
        public static bool operator ==(GamePadState left, GamePadState right) => left.Equals(right);
        
        /// <inheritdoc/>
        public static bool operator !=(GamePadState left, GamePadState right) => !(left == right);
    }
}
