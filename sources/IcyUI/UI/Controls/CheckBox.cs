// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="ToggleButton"/> styled as a checkbox. Behaviorally identical to <see cref="ToggleButton"/> - a
    /// distinct type so styles/themes can target checkbox appearance separately from a generic toggle button.
    /// </summary>
    /// <remarks>
    /// <see cref="CheckBrush"/> is a purely themable data slot - a template's own checkmark glyph (typically an
    /// <see cref="Icon"/> with <see cref="IconKind.Checkmark"/>) binds its own <c>Stroke</c> to it via
    /// <c>{TemplateBinding CheckBrush}</c>, letting a <c>Checked</c>/<c>Normal</c> <see cref="Styles.VisualState"/>
    /// pair swap it between visible and transparent - see IcyUI's own bundled default theme for the pattern this
    /// property exists for.
    /// </remarks>
    public class CheckBox : ToggleButton
    {
        private IBrush checkBrush = new SolidColorBrush(Color.Transparent);

        /// <summary>
        /// Gets or sets the brush a template's checkmark glyph should draw with - transparent (invisible) by
        /// default, so an untemplated <see cref="CheckBox"/> (which has nowhere to draw a glyph anyway) is
        /// unaffected either way.
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush CheckBrush
        {
            get => checkBrush;
            set => SetProperty(ref checkBrush, value);
        }
    }
}
