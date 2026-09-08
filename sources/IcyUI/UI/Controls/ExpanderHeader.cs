// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="ToggleButton"/> styled as an <see cref="Expander"/>'s clickable header row. Behaviorally
    /// identical to <see cref="ToggleButton"/> - a distinct type so styles/themes can target the header's
    /// chevron-plus-content appearance separately from a generic toggle button, the same reasoning
    /// <see cref="CheckBox"/> already documents for itself.
    /// </summary>
    public class ExpanderHeader : ToggleButton
    {
        private float chevronRotation;

        /// <summary>
        /// Gets or sets the rotation, in degrees, a template's chevron glyph should render at - a purely
        /// themable data slot (mirrors <see cref="CheckBox.CheckBrush"/>'s own shape) letting a
        /// <c>Checked</c>/<c>Normal</c> <see cref="Styles.VisualState"/> pair flip the chevron between
        /// "collapsed" and "expanded" orientations via <see cref="UIElement.RenderRotation"/> (purely
        /// cosmetic - doesn't affect layout) without any new rendering mechanism - see IcyUI's own bundled
        /// default theme for the pattern this property exists for.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(0f)]
        [RegisterReference]
        public float ChevronRotation
        {
            get => chevronRotation;
            set => SetProperty(ref chevronRotation, value);
        }
    }
}
