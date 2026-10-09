// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Markup;

namespace Icy.UI.Styles
{
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

        /// <summary>
        /// Gets or sets how long entering this state takes to animate each setter from the element's current
        /// effective value to its target, or <see langword="null"/> to snap instantly (the default).
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets the easing function used for the transition when <see cref="Duration"/> is set. Ignored when
        /// <see cref="Duration"/> is <see langword="null"/>.
        /// </summary>
        public Animations.EasingFunction? Easing { get; set; }
    }
}
