// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Displays a single <see cref="IImage"/> (e.g. an <see cref="ImageBrush"/> or <see cref="NinePatchImageBrush"/>),
    /// sized to <see cref="IImage.Size"/> unless overridden by an explicit <see cref="UIElement.Width"/>/<see cref="UIElement.Height"/>.
    /// </summary>
    public class Image : UIElement
    {
        private IImage? source;

        /// <summary>
        /// Gets or sets the image to display.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        [AffectsMeasure]
        public IImage? Source
        {
            get => source;
            set
            {
                if (SetProperty(ref source, value))
                {
                    InvalidateMeasure();
                }
            }
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            // Leaf element - no children to arrange.
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => Source?.Size ?? Size.Empty;

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context) => Source?.Draw(context, GetDefaultRenderOptions());
    }
}
