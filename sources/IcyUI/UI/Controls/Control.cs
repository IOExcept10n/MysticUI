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
    /// internal <see cref="Border"/> (<see cref="Chrome"/>) that fills this control's entire
    /// <see cref="UIElement.ActualBounds"/> and forwards <see cref="Background"/>/<see cref="BorderBrush"/>/
    /// <see cref="BorderThickness"/>/<see cref="Padding"/> to it.
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
        /// Gets or sets the padding of this control, i.e. the amount by which its content is inset from
        /// <see cref="Chrome"/>'s own bounds (inside <see cref="BorderThickness"/>).
        /// </summary>
        /// <remarks>
        /// Hides (rather than overrides - <see cref="UIElement.Padding"/> isn't <see langword="virtual"/>)
        /// <see cref="UIElement.Padding"/> to forward it to <see cref="Chrome"/>, the same way
        /// <see cref="Background"/>/<see cref="BorderBrush"/>/<see cref="BorderThickness"/> already do - so
        /// <see cref="Background"/>/<see cref="BorderBrush"/> (drawn across <see cref="Chrome"/>'s full bounds,
        /// unaffected by padding - the standard box model) cover this control's whole area including the padding
        /// band, and only the actual content ends up inset by it. <see cref="UIElement.Measure()"/>'s own generic
        /// padding step still applies exactly once - to <see cref="Chrome"/>, when <see cref="MeasureContent"/>
        /// calls <see cref="Chrome"/>'s <see cref="UIElement.Measure()"/> - since this control's own (hidden,
        /// always-zero) base <see cref="UIElement.Padding"/> field is never written to.
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public new Thickness Padding { get => Chrome.Padding; set => Chrome.Padding = value; }

        /// <summary>
        /// Gets the area available to this control's decorated content, i.e. this control's own
        /// <see cref="UIElement.ActualBounds"/> inset by <see cref="BorderThickness"/> and <see cref="Padding"/>.
        /// </summary>
        /// <remarks>
        /// Computed from this control's own <see cref="UIElement.ActualBounds"/>, not <see cref="Chrome"/>'s -
        /// normally the two agree (<see cref="Chrome"/> fills <see cref="UIElement.ActualBounds"/> exactly), but
        /// a subclass like <see cref="ScrollViewer"/> deliberately arranges <see cref="Chrome"/> larger than this
        /// control's own bounds (so <see cref="ContentControl.Content"/> can overflow for scrolling) - callers of
        /// <see cref="ContentBounds"/> mean this control's own visible content area, not however big
        /// <see cref="Chrome"/> currently happens to be.
        /// </remarks>
        public Rectangle ContentBounds => ActualBounds - BorderThickness - Padding;

        /// <summary>
        /// Gets the internal <see cref="Border"/> this control composes for its background/border decoration.
        /// </summary>
        protected Border Chrome { get; } = new();

        /// <inheritdoc/>
        protected override void ArrangeContent() => Chrome.Arrange(ActualBounds);

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
