// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Markup;

namespace Icy.UI.Styles
{
    /// <summary>
    /// Represents the interaction states a <see cref="UIElement"/> can combine at once.
    /// </summary>
    [Flags]
    public enum ControlState
    {
        /// <summary>
        /// No interaction state is active.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// The pointer is over the element.
        /// </summary>
        Hovered = 1 << 0,

        /// <summary>
        /// The element is being pressed (e.g. a mouse button is held down over it).
        /// </summary>
        Pressed = 1 << 1,

        /// <summary>
        /// The element is disabled and shouldn't respond to input.
        /// </summary>
        Disabled = 1 << 2,

        /// <summary>
        /// The element has input focus.
        /// </summary>
        Focused = 1 << 3,

        /// <summary>
        /// The element is being touched.
        /// </summary>
        Touching = 1 << 4,

        /// <summary>
        /// The element represents an "on"/checked value (e.g. a checked <see cref="Controls.ToggleButton"/>).
        /// </summary>
        Checked = 1 << 5,
    }

    /// <summary>
    /// Represents a single named state within a <see cref="VisualStateGroup"/>, and the property values it sets
    /// while its <see cref="State"/> flags are active.
    /// </summary>
    /// <param name="name">The name of this state.</param>
    /// <param name="state">The combination of <see cref="ControlState"/> flags this state responds to.</param>
    [MarkupSetterCollection(nameof(Setters))]
    public class VisualState(string name, ControlState state)
    {
        /// <summary>
        /// Gets the name of this state.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the combination of <see cref="ControlState"/> flags this state responds to.
        /// </summary>
        /// <remarks>
        /// An element's <see cref="UIElement.ControlState"/> can have more than one <see cref="ControlState"/> flag
        /// set at once (e.g. hovered and focused). Within a group, the state whose flags form the largest subset of
        /// the element's current <see cref="UIElement.ControlState"/> is the one that becomes active - see
        /// <see cref="UIElement.RegisterStateGroup(VisualStateGroup)"/>.
        /// </remarks>
        public ControlState State { get; } = state;

        /// <summary>
        /// Gets the property values this state sets while active, keyed by property name.
        /// </summary>
        public Dictionary<string, object?> Setters { get; } = [];
    }

    /// <summary>
    /// Represents a named, mutually-exclusive set of <see cref="VisualState"/>s - at most one state within a group
    /// is active on a given element at a time.
    /// </summary>
    /// <param name="name">The name of this group.</param>
    public class VisualStateGroup(string name)
    {
        /// <summary>
        /// Gets the name of this group.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the states within this group.
        /// </summary>
        public List<VisualState> States { get; } = [];
    }
}
