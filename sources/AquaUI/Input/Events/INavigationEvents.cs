// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using AquaUI.Data;
using AquaUI.Input.Devices;

namespace AquaUI.Input.Events
{
    /// <summary>
    /// Represents an interface for navigation related events.
    /// </summary>
    public interface INavigationEvents : IInputEventProvider, IStartRepeatEvents
    {
        /// <summary>
        /// Occurs when user tries to navigate back to the previous page.
        /// </summary>
        event EventHandler? NavigateBack;

        /// <summary>
        /// Occurs when user tries to navigate forward to the next page.
        /// </summary>
        event EventHandler? NavigateForward;

        /// <summary>
        /// Occurs when user tries to close the modal window (or use cancel button).
        /// </summary>
        event EventHandler? CloseModal;

        /// <summary>
        /// Occurs when user tries to use the selected element (or use accept button).
        /// </summary>
        event EventHandler? SelectElement;

        /// <summary>
        /// Occurs when user presses any non-modifier key with the <see cref="ModifierKeys.Alt"/> pressed.
        /// </summary>
        event EventHandler<AcceptableEventArgs<Keys>>? AltKeyNavigation;

        /// <summary>
        /// Occurs when user tries to navigate between UI elements using keyboard for navigation.
        /// </summary>
        /// <remarks>
        /// Direction of the navigation change is passed to event arguments.
        /// </remarks>
        event EventHandler<AcceptableEventArgs<Vector2>>? FocusChanging;

        /// <summary>
        /// Occurs when user tries to focus the next UI element.
        /// </summary>
        event EventHandler? FocusNext;

        /// <summary>
        /// Occurs when user tries to focus previous UI element.
        /// </summary>
        event EventHandler? FocusPrevious;

        /// <summary>
        /// Gets or sets the minimal distance for the gamepad stick to raise the <see cref="FocusChanging"/> event.
        /// </summary>
        float MinimalFocusChangeDistance { get; set; }
    }
}