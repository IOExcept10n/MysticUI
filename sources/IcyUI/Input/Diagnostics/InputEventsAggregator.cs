using Icy.Input.Devices;
using Icy.Input.Events;

namespace Icy.Input.Diagnostics
{
    /// <summary>
    /// Represents a simple aggregator class that aggregates all the input events from all of the events listeners.
    /// </summary>
    public class InputEventsAggregator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputEventsAggregator"/> class.
        /// </summary>
        /// <param name="events">Input system to access the events.</param>
        public InputEventsAggregator(IInputEventSystem events)
        {
            events.Devices.DeviceConnected += Devices_DeviceConnected;
            events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
            events.Drag.DragStarted += Drag_DragStarted;
            events.Drag.DragEnded += Drag_DragEnded;
            events.Drag.DragPerforming += Drag_DragPerforming;
            events.Navigation.AltKeyNavigation += Navigation_AltKeyNavigation;
            events.Navigation.CloseModal += Navigation_CloseModal;
            events.Navigation.FocusChanging += Navigation_FocusChanging;
            events.Navigation.FocusNext += Navigation_FocusNext;
            events.Navigation.FocusPrevious += Navigation_FocusPrevious;
            events.Navigation.NavigateBack += Navigation_NavigateBack;
            events.Navigation.NavigateForward += Navigation_NavigateForward;
            events.Navigation.SelectElement += Navigation_SelectElement;
            events.Scroll.Scroll += Scroll_Scroll;
            events.Text.CopyText += Text_CopyText;
            events.Text.CutText += Text_CutText;
            events.Text.PasteText += Text_PasteText;
            events.Text.TextInput += Text_TextInput;
            events.Touch.Hold += Touch_Hold;
            events.Touch.Tap += Touch_Tap;
            events.Touch.TouchDown += Touch_TouchDown;
            events.Touch.TouchUp += Touch_TouchUp;
        }

        /// <summary>
        /// Occurs when any of <see cref="IInputEventSystem"/> events is captured.
        /// </summary>
        public event EventHandler<LoggerEventInfo>? OnEvent;

        /// <summary>
        /// Defines the type of the input event.
        /// </summary>
        public enum InputEventType
        {
            /// <summary>
            /// <see cref="IDeviceEvents.DeviceConnected"/> event.
            /// </summary>
            DeviceConnected,

            /// <summary>
            /// <see cref="IDeviceEvents.DeviceDisconnected"/> event.
            /// </summary>
            DeviceDisconnected,

            /// <summary>
            /// <see cref="IDragEvents.DragStarted"/> event.
            /// </summary>
            DragStarted,

            /// <summary>
            /// <see cref="IDragEvents.DragEnded"/> event.
            /// </summary>
            DragEnded,

            /// <summary>
            /// <see cref="IDragEvents.DragPerforming"/> event.
            /// </summary>
            DragPerforming,

            /// <summary>
            /// <see cref="INavigationEvents.AltKeyNavigation"/> event.
            /// </summary>
            AltKeyNavigation,

            /// <summary>
            /// <see cref="INavigationEvents.CloseModal"/> event.
            /// </summary>
            CloseModal,

            /// <summary>
            /// <see cref="INavigationEvents.FocusChanging"/> event.
            /// </summary>
            FocusChanging,

            /// <summary>
            /// <see cref="INavigationEvents.FocusNext"/> event.
            /// </summary>
            FocusNext,

            /// <summary>
            /// <see cref="INavigationEvents.FocusPrevious"/> event.
            /// </summary>
            FocusPrevious,

            /// <summary>
            /// <see cref="INavigationEvents.NavigateBack"/> event.
            /// </summary>
            NavigateBack,

            /// <summary>
            /// <see cref="INavigationEvents.NavigateForward"/> event.
            /// </summary>
            NavigateForward,

            /// <summary>
            /// <see cref="INavigationEvents.SelectElement"/> event.
            /// </summary>
            SelectElement,

            /// <summary>
            /// <see cref="IScrollEvents.Scroll"/> event.
            /// </summary>
            Scroll,

            /// <summary>
            /// <see cref="ITextEvents.CopyText"/> event.
            /// </summary>
            CopyText,

            /// <summary>
            /// <see cref="ITextEvents.CutText"/> event.
            /// </summary>
            CutText,

            /// <summary>
            /// <see cref="ITextEvents.PasteText"/> event.
            /// </summary>
            PasteText,

            /// <summary>
            /// <see cref="ITextEvents.TextInput"/> event.
            /// </summary>
            TextInput,

            /// <summary>
            /// <see cref="ITouchEvents.Hold"/> event.
            /// </summary>
            Hold,

            /// <summary>
            /// <see cref="ITouchEvents.Tap"/> event.
            /// </summary>
            Tap,

            /// <summary>
            /// <see cref="ITouchEvents.TouchDown"/> event.
            /// </summary>
            TouchDown,

            /// <summary>
            /// <see cref="ITouchEvents.TouchUp"/> event.
            /// </summary>
            TouchUp,
        }

        private void Devices_DeviceConnected(object? sender, Data.GenericEventArgs<IInputDeviceListener> e)
        {
            RaiseEvent(InputEventType.DeviceConnected, sender, e);
        }

        private void Devices_DeviceDisconnected(object? sender, Data.GenericEventArgs<IInputDeviceListener> e)
        {
            RaiseEvent(InputEventType.DeviceDisconnected, sender, e);
        }

        private void Drag_DragEnded(object? sender, Data.GenericEventArgs<System.Drawing.Point> e)
        {
            RaiseEvent(InputEventType.DragEnded, sender, e);
        }

        private void Drag_DragPerforming(object? sender, Data.GenericEventArgs<System.Drawing.Point> e)
        {
            RaiseEvent(InputEventType.DragPerforming, sender, e);
        }

        private void Drag_DragStarted(object? sender, Data.AcceptableEventArgs<System.Drawing.Point> e)
        {
            RaiseEvent(InputEventType.DragStarted, sender, e);
        }

        private void Navigation_AltKeyNavigation(object? sender, Data.AcceptableEventArgs<Keys> e)
        {
            RaiseEvent(InputEventType.AltKeyNavigation, sender, e);
        }

        private void Navigation_CloseModal(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.CloseModal, sender, e);
        }

        private void Navigation_FocusChanging(object? sender, Data.AcceptableEventArgs<System.Numerics.Vector2> e)
        {
            RaiseEvent(InputEventType.FocusChanging, sender, e);
        }

        private void Navigation_FocusNext(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.FocusNext, sender, e);
        }

        private void Navigation_FocusPrevious(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.FocusPrevious, sender, e);
        }

        private void Navigation_NavigateBack(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.NavigateBack, sender, e);
        }

        private void Navigation_NavigateForward(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.NavigateForward, sender, e);
        }

        private void Navigation_SelectElement(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.SelectElement, sender, e);
        }

        private void RaiseEvent(InputEventType type, object? sender, EventArgs args)
        {
            if (sender is IInputEventProvider device)
                OnEvent?.Invoke(this, new LoggerEventInfo(device, type, args));
        }

        private void Scroll_Scroll(object? sender, Data.GenericEventArgs<Events.ScrollInfo> e)
        {
            RaiseEvent(InputEventType.Scroll, sender, e);
        }

        private void Text_CopyText(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.CopyText, sender, e);
        }

        private void Text_CutText(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.CutText, sender, e);
        }

        private void Text_PasteText(object? sender, EventArgs e)
        {
            RaiseEvent(InputEventType.PasteText, sender, e);
        }

        private void Text_TextInput(object? sender, Data.GenericEventArgs<Events.ITextInputEventInfo> e)
        {
            RaiseEvent(InputEventType.TextInput, sender, e);
        }

        private void Touch_Hold(object? sender, Data.GenericEventArgs<System.Drawing.Point> e)
        {
            RaiseEvent(InputEventType.Hold, sender, e);
        }

        private void Touch_Tap(object? sender, Data.GenericEventArgs<Events.TouchInfo> e)
        {
            RaiseEvent(InputEventType.Tap, sender, e);
        }

        private void Touch_TouchDown(object? sender, Data.GenericEventArgs<System.Drawing.Point> e)
        {
            RaiseEvent(InputEventType.TouchDown, sender, e);
        }

        private void Touch_TouchUp(object? sender, Data.GenericEventArgs<System.Drawing.Point> e)
        {
            RaiseEvent(InputEventType.TouchUp, sender, e);
        }

        /// <summary>
        /// Provides arguments for the logger events.
        /// </summary>
        /// <param name="InputEventListener">Gets an instance of the event listener that raised the event.</param>
        /// <param name="EventType">Gets the aggregated event type.</param>
        /// <param name="Args">Gets the arguments provided with an event.</param>
        public readonly record struct LoggerEventInfo(IInputEventProvider InputEventListener, InputEventType EventType, EventArgs Args);
    }
}
