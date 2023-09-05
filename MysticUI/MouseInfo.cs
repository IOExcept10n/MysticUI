using Stride.Core.Mathematics;
using Stride.Input;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MysticUI
{
    /// <summary>
    /// Represents a structured mouse state.
    /// </summary>
    public readonly struct MouseInfo : IEquatable<MouseInfo>, IEqualityComparer<MouseInfo>
    {
        /// <summary>
        /// Position of the pointer.
        /// </summary>
        public readonly Point Position;

        /// <summary>
        /// Set of clicked buttons.
        /// </summary>
        public readonly MouseButtonFlags ClickedButtons;

        /// <summary>
        /// Mouse wheel value.
        /// </summary>
        public readonly float Wheel;

        /// <summary>
        /// Creates new <see cref="MouseInfo"/> with all provided parameters.
        /// </summary>
        /// <param name="position">Position of the pointer.</param>
        /// <param name="buttonFlags">Flags with pressed buttons.</param>
        /// <param name="wheel">Wheel value.</param>
        public MouseInfo(Point position, MouseButtonFlags buttonFlags = MouseButtonFlags.None, float wheel = 0)
        {
            Position = position;
            ClickedButtons = buttonFlags;
            Wheel = wheel;
        }

        /// <summary>
        /// Detects if some of mouse buttons were clicked in this state.
        /// </summary>
        /// <param name="button">Button to check.</param>
        /// <returns><see langword="true"/> if the button is clicked, <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDown(MouseButton button)
        {
            int key = 1 << (int)button;
            return ((byte)ClickedButtons & key) == key;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is MouseInfo info && Equals(info);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Position, ClickedButtons, Wheel);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{{{Position}: [{ClickedButtons}]; {Wheel}}}";
        }

        /// <inheritdoc/>
        public bool Equals(MouseInfo other)
        {
            return Position == other.Position &&
                   ClickedButtons == other.ClickedButtons &&
                   Wheel == other.Wheel;
        }

        /// <inheritdoc/>
        public bool Equals(MouseInfo x, MouseInfo y)
        {
            return x.Position == y.Position &&
                x.ClickedButtons == y.ClickedButtons &&
                x.Wheel == y.Wheel;
        }

        /// <inheritdoc/>
        public int GetHashCode([DisallowNull] MouseInfo obj)
        {
            return HashCode.Combine(Position, ClickedButtons, Wheel);
        }

        /// <summary>
        /// Checks if two <see cref="MouseInfo"/>s are equal.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns><see langword="true"/> if first <see cref="MouseInfo"/> is equal to second, <see langword="false"/> otherwise.</returns>
        public static bool operator ==(MouseInfo left, MouseInfo right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks if two <see cref="MouseInfo"/>s are equal.
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns><see langword="true"/> if first <see cref="MouseInfo"/> is equal to second, <see langword="false"/> otherwise.</returns>
        public static bool operator !=(MouseInfo left, MouseInfo right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// Represents the flags set for the mouse buttons.
    /// </summary>
    [Flags]
    public enum MouseButtonFlags : byte
    {
        /// <summary>
        /// No buttons.
        /// </summary>
        None = 0,

        /// <summary>
        /// Left button.
        /// </summary>
        LeftButton = 1,

        /// <summary>
        /// Middle button.
        /// </summary>
        MiddleButton = 2,

        /// <summary>
        /// Right button.
        /// </summary>
        RightButton = 4,

        /// <summary>
        /// First extended button.
        /// </summary>
        ExtendedButton1 = 8,

        /// <summary>
        /// Second extended button.
        /// </summary>
        ExtendedButton2 = 16
    }
}