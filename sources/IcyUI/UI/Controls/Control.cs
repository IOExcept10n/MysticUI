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
    /// Base class for controls that need background/border decoration without reimplementing it - composes an
    /// internal <see cref="Border"/> (<see cref="Chrome"/>) that fills this control's <see cref="ContentBounds"/>
    /// and forwards <see cref="Background"/>/<see cref="BorderBrush"/>/<see cref="BorderThickness"/> to it.
    /// </summary>
    /// <remarks>
    /// <see cref="UIElement"/> itself carries no decoration (per the <see cref="UI.Border"/> extraction) - this
    /// class exists so every decorated control doesn't have to compose its own <see cref="UI.Border"/> by hand.
    /// Subclasses that need child content should derive from <see cref="ContentControl"/> instead, which exposes
    /// <see cref="ContentControl.Content"/> as <see cref="Chrome"/>'s child.
    /// </remarks>
    public class Control : UIElement, IContainerLayout
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Control"/> class.
        /// </summary>
        public Control()
        {
            Chrome.Parent = this;
        }

        /// <summary>
        /// Gets or sets the background brush drawn behind this control's content.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(typeof(SolidColorBrush), "Transparent")]
        [RegisterReference]
        public IBrush Background { get => Chrome.Background; set => Chrome.Background = value; }

        /// <summary>
        /// Gets or sets the brush used to paint this control's border.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(null)]
        [RegisterReference]
        public IBrush? BorderBrush { get => Chrome.BorderBrush; set => Chrome.BorderBrush = value; }

        /// <summary>
        /// Gets or sets the thickness of this control's border, and the amount by which its content is inset from
        /// <see cref="ContentBounds"/>.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public Thickness BorderThickness { get => Chrome.BorderThickness; set => Chrome.BorderThickness = value; }

        /// <summary>
        /// Gets the area available to this control's decorated content, i.e. <see cref="UIElement.ActualBounds"/>
        /// inset by <see cref="UIElement.Padding"/>. This is the area <see cref="Chrome"/> fills; <see cref="Chrome"/>
        /// further insets its own content by <see cref="BorderThickness"/>.
        /// </summary>
        public Rectangle ContentBounds => ActualBounds - Padding;

        /// <summary>
        /// Gets the internal <see cref="Border"/> this control composes for its background/border decoration.
        /// </summary>
        protected Border Chrome { get; } = new();

        /// <inheritdoc/>
        protected override void ArrangeContent() => Chrome.Arrange();

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            yield return Chrome;
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => Chrome.Measure();

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            Chrome.Canvas = Canvas;
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            base.OnDetached();
            Chrome.Canvas = null;
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context) => Chrome.Draw(context);
    }
}
