// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Markup;

namespace Icy.UI.Styles
{
    /// <summary>
    /// Represents a set of property values (and, optionally, <see cref="VisualStateGroup"/>s) applied to every
    /// <see cref="UIElement"/> whose <see cref="UIElement.Style"/> is set to this instance.
    /// </summary>
    /// <param name="targetType">The type of element this style is meant to be applied to.</param>
    public class Style(Type targetType)
    {
        /// <summary>
        /// Gets the type of element this style is meant to be applied to.
        /// </summary>
        public Type TargetType { get; } = targetType;

        /// <summary>
        /// Gets the property values this style sets, keyed by property name.
        /// </summary>
        /// <remarks>
        /// Resolved against <see cref="PropertyRegistry"/> when applied (see <see cref="Apply(UIElement)"/>), so
        /// only properties registered via <see cref="Icy.Data.Markup.Attributes.RegisterReferenceAttribute"/> (or
        /// an attached property) can be targeted by name here.
        /// </remarks>
        public Dictionary<string, object?> Setters { get; } = [];

        /// <summary>
        /// Gets the visual-state groups this style registers on every element it's applied to.
        /// </summary>
        public List<VisualStateGroup> StateGroups { get; } = [];

        /// <summary>
        /// Gets or sets a style this style inherits setters and state groups from.
        /// </summary>
        /// <remarks>
        /// Applied first, so this style's own setters/state groups take precedence when both set the same property.
        /// </remarks>
        public Style? BasedOn { get; set; }

        /// <summary>
        /// Applies this style's setters and state groups to <paramref name="control"/>.
        /// </summary>
        /// <param name="control">The element to apply this style to.</param>
        public void Apply(UIElement control)
        {
            BasedOn?.Apply(control);

            IPropertyStore store = PropertyRegistry.Instance.GetPropertyStore(control.GetType());
            foreach (KeyValuePair<string, object?> setter in Setters)
            {
                if (store.TryGetProperty(setter.Key, out IPropertyReference? property))
                {
                    property.SetTierValue(control, PropertyValuePrecedence.Style, setter.Value);
                }
            }

            foreach (VisualStateGroup group in StateGroups)
            {
                control.RegisterStateGroup(group);
            }
        }
    }
}
