// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents the non-local precedence tiers that can contribute a value to an <see cref="IPropertyReference"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The full effective-value precedence, highest to lowest, is: a directly-assigned (local) value, then
    /// <see cref="Animation"/>, then <see cref="VisualState"/>, then <see cref="Style"/>, then the property's
    /// default value. Local and default values are not represented here: a local value is whatever the backing
    /// field currently holds when no tier below is active, and the default value is <see cref="PropertyMetadata.DefaultValue"/>
    /// used as the initial fallback before anything is ever assigned.
    /// </para>
    /// <para>
    /// The underlying integer value doubles as the tier's storage slot index and its precedence rank
    /// (higher wins) — do not reorder or insert members without updating both meanings.
    /// </para>
    /// </remarks>
    public enum PropertyValuePrecedence
    {
        /// <summary>
        /// The value comes from a <see cref="Icy.UI.Styles.Style"/> setter — the lowest non-local precedence tier.
        /// </summary>
        Style = 0,

        /// <summary>
        /// The value comes from an active <see cref="Icy.UI.Styles.VisualState"/> trigger.
        /// </summary>
        VisualState = 1,

        /// <summary>
        /// The value comes from a running animation — the highest non-local precedence tier.
        /// </summary>
        Animation = 2,
    }
}
