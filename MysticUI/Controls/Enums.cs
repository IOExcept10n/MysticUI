namespace MysticUI.Controls
{
    /// <summary>
    /// Describes how a child element is horizontally positioned or stretched within a parent's layout slot.
    /// </summary>
    public enum HorizontalAlignment
    {
        /// <summary>
        /// An element stretched to fill the entire layout slot of the parent element.
        /// </summary>
        Stretch,

        /// <summary>
        /// An element aligned to the left of the layout slot for the parent element.
        /// </summary>
        Left,

        /// <summary>
        /// An element aligned to the center of the layout slot for the parent element.
        /// </summary>
        Center,

        /// <summary>
        /// An element aligned to the right of the layout slot for the parent element.
        /// </summary>
        Right,
    }

    /// <summary>
    /// Describes how a child element is vertically positioned or stretched within a parent's layout slot.
    /// </summary>
    public enum VerticalAlignment
    {
        /// <summary>
        /// An element stretched to fill the entire layout slot of the parent element.
        /// </summary>
        Stretch,

        /// <summary>
        /// An element aligned to the top of the layout slot for the parent element.
        /// </summary>
        Top,

        /// <summary>
        /// An element aligned to the center of the layout slot for the parent element.
        /// </summary>
        Center,

        /// <summary>
        /// An element aligned to the bottom of the layout slot for the parent element.
        /// </summary>
        Bottom
    }

    /// <summary>
    /// Describes the orientation of elements that supports multiple space orientations.
    /// </summary>
    public enum Orientation
    {
        /// <summary>
        /// Vertical orientation.
        /// </summary>
        Vertical,

        /// <summary>
        /// Horizontal orientation.
        /// </summary>
        Horizontal,
    }

    /// <summary>
    /// Describes the dragging directions for the element.
    /// </summary>
    [Flags]
    public enum DragDirection
    {
        /// <summary>
        /// No dragging support.
        /// </summary>
        None,

        /// <summary>
        /// Only vertical dragging enabled.
        /// </summary>
        Vertical,

        /// <summary>
        /// Only horizontal dragging enabled.
        /// </summary>
        Horizontal,

        /// <summary>
        /// Both vertical and horizontal dragging enabled.
        /// </summary>
        Both = Vertical | Horizontal
    }

    /// <summary>
    /// Describes the state of control during the frame.
    /// </summary>
    public enum ControlInteractionState
    {
        /// <summary>
        /// Default state: control is enabled and have no interaction.
        /// </summary>
        Default,

        /// <summary>
        /// Control disabled state.
        /// </summary>
        Disabled,

        /// <summary>
        /// Used when the control has keyboard focus.
        /// </summary>
        Focused,

        /// <summary>
        /// Used when the mouse is over the control.
        /// </summary>
        MouseOver,

        /// <summary>
        /// Used when the mouse is touching over the control.
        /// </summary>
        Clicking
    }

    /// <summary>
    /// Determines the scroll bar visibility.
    /// </summary>
    public enum ScrollbarVisibility
    {
        /// <summary>
        /// A scroll bar appears when the viewport cannot display all of the content.
        /// </summary>
        Auto,

        /// <summary>
        /// Scroll bar is visible but disabled.
        /// </summary>
        Disabled,

        /// <summary>
        /// A scroll bar doesn't appear even when the viewport cannot display all of the content.
        /// </summary>
        Hidden,

        /// <summary>
        /// A scroll bar is visible always even when the viewport can display all of the content.
        /// </summary>
        Visible
    }
}