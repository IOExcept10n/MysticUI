// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Markup.Attributes;
using Icy.Markup;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="Control"/> that hosts a single arbitrary <see cref="UIElement"/> as its content, decorated by
    /// the inherited <see cref="Control.Background"/>/<see cref="Control.BorderBrush"/>/<see cref="Control.BorderThickness"/>.
    /// </summary>
    [ContentProperty(nameof(Content))]
    public class ContentControl : Control
    {
        /// <summary>
        /// Gets or sets the element displayed as this control's content.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Content { get => Chrome.Child; set => Chrome.Child = value; }
    }
}
