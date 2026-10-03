// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input.Events;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Represents the gestures recognized from touch contacts and mouse buttons - the single source of pointer gestures
    /// on every engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Engines only report raw contacts (<see cref="Devices.ITouchInput.Contacts"/>) and mouse buttons. Recognition happens
    /// here, so thresholds and behavior are identical everywhere. All positions are in physical pixels.
    /// </para>
    /// <para>
    /// Per pointer kind (see <see cref="PointerKind"/>):
    /// <list type="bullet">
    /// <item><description><see cref="PointerKind.Touch"/> and <see cref="PointerKind.MouseLeft"/> tap, hold and drag.</description></item>
    /// <item><description><see cref="PointerKind.MouseRight"/> raises <see cref="Held"/> on release.</description></item>
    /// <item><description><see cref="PointerKind.MouseMiddle"/> only drags.</description></item>
    /// <item><description>Two touch contacts pinch. A second finger cancels the first finger's drag; after a pinch, the
    /// remaining fingers stay inert until all are lifted.</description></item>
    /// </list>
    /// Mouse presses are ignored while any touch contact is active and for a short moment after the last one lifts,
    /// because the OS synthesizes mouse input from touch (on Windows, a tap's click arrives after the finger lifts).
    /// Mouse releases always go through, so a button pressed before the touch began still ends its gesture.
    /// </para>
    /// </remarks>
    public interface IGestureEvents : IInputEventProvider
    {
        /// <summary>
        /// Occurs when a pointer is pressed.
        /// </summary>
        event EventHandler<GenericEventArgs<PointerInfo>>? PointerPressed;

        /// <summary>
        /// Occurs when a pointer is released, or its contact vanished.
        /// </summary>
        event EventHandler<GenericEventArgs<PointerInfo>>? PointerReleased;

        /// <summary>
        /// Occurs when a pointer is released inside <see cref="GestureSettings.Slop"/> before <see cref="GestureSettings.HoldDelay"/>.
        /// </summary>
        event EventHandler<GenericEventArgs<TapInfo>>? Tapped;

        /// <summary>
        /// Occurs when a touch or left-mouse press stays inside <see cref="GestureSettings.Slop"/> for
        /// <see cref="GestureSettings.HoldDelay"/>, or when the right mouse button is released.
        /// </summary>
        event EventHandler<GenericEventArgs<PointerInfo>>? Held;

        /// <summary>
        /// Occurs when a press moves past <see cref="GestureSettings.Slop"/>. Set <see cref="System.ComponentModel.CancelEventArgs.Cancel"/>
        /// to reject the drag: no further drag events are raised for that press.
        /// </summary>
        event EventHandler<AcceptableEventArgs<DragInfo>>? DragStarted;

        /// <summary>
        /// Occurs when a dragging pointer moves.
        /// </summary>
        event EventHandler<GenericEventArgs<DragInfo>>? DragMoved;

        /// <summary>
        /// Occurs when a dragging pointer is released. <see cref="DragInfo.Velocity"/> holds the release velocity.
        /// </summary>
        event EventHandler<GenericEventArgs<DragInfo>>? DragCompleted;

        /// <summary>
        /// Occurs when a drag ends without a release - a second finger started a pinch, or the contact vanished.
        /// </summary>
        event EventHandler<GenericEventArgs<DragInfo>>? DragCanceled;

        /// <summary>
        /// Occurs when a second touch contact lands and a pinch begins.
        /// </summary>
        event EventHandler<GenericEventArgs<PinchInfo>>? PinchStarted;

        /// <summary>
        /// Occurs when a pinching finger moves.
        /// </summary>
        event EventHandler<GenericEventArgs<PinchInfo>>? PinchChanged;

        /// <summary>
        /// Occurs when either pinching finger is lifted or vanishes.
        /// </summary>
        event EventHandler<GenericEventArgs<PinchInfo>>? PinchCompleted;

        /// <summary>
        /// Gets the recognition thresholds.
        /// </summary>
        GestureSettings Settings { get; }
    }
}
