// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;
using Icy.Rendering.Brushes;

namespace Icy.UI
{
    /// <summary>
    /// Decorates a single <see cref="Child"/> element with a background, border brush, and border thickness.
    /// </summary>
    /// <remarks>
    /// <see cref="UIElement"/> itself carries no decoration (only layout: size, margin, padding, alignment,
    /// transforms) - wrap an element in <see cref="Border"/> whenever it needs a background or a drawn border.
    /// </remarks>
    public class Border : UIElement, IContainerLayout
    {
        private IBrush background = new SolidColorBrush(Color.Transparent);
        private IBrush? borderBrush;
        private Thickness borderThickness;
        private UIElement? child;

        /// <summary>
        /// Gets or sets the background brush drawn behind <see cref="Child"/>.
        /// </summary>
        /// <value>
        /// The brush used to paint the background. The default is a transparent solid color brush.
        /// </value>
        [Category("Appearance")]
        [DefaultValue(typeof(SolidColorBrush), "Transparent")]
        [RegisterReference]
        public IBrush Background { get => background; set => SetProperty(ref background, value); }

        /// <summary>
        /// Gets or sets the brush used to paint the border.
        /// </summary>
        /// <value>
        /// The brush used to paint the border. The default is <see langword="null"/> (no border drawn).
        /// </value>
        [Category("Appearance")]
        [DefaultValue(null)]
        [RegisterReference]
        public IBrush? BorderBrush { get => borderBrush; set => SetProperty(ref borderBrush, value); }

        /// <summary>
        /// Gets or sets the thickness of the border, and the amount by which <see cref="Child"/> is inset from
        /// <see cref="UIElement.ActualBounds"/>.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(typeof(Thickness), "0,0,0,0")]
        [RegisterReference]
        [AffectsArrange]
        [AffectsMeasure]
        public Thickness BorderThickness
        {
            get => borderThickness;
            set
            {
                if (SetProperty(ref borderThickness, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the single element decorated by this <see cref="Border"/> instance.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Child
        {
            get => child;
            set
            {
                if (child == value)
                    return;
                if (child != null)
                {
                    child.Parent = null;
                    child.Canvas = null;
                }

                child = value;
                if (child != null)
                {
                    child.Parent = this;
                    child.Canvas = Canvas;
                }

                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets the area available to <see cref="Child"/>, i.e. <see cref="UIElement.ActualBounds"/> inset by
        /// <see cref="BorderThickness"/>.
        /// </summary>
        public Rectangle ContentBounds => ActualBounds - BorderThickness;

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            if (Child?.IsVisible == true)
                Child.Arrange();
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            Size childSize = Child?.IsVisible == true ? Child.Measure() : Size.Empty;
            return new Size(childSize.Width + BorderThickness.Width, childSize.Height + BorderThickness.Height);
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            TextureRenderingOptions renderOptions = GetDefaultRenderOptions();

            Background?.Draw(context, renderOptions);

            if (BorderBrush != null && BorderThickness != Thickness.Zero)
            {
                Rectangle drawArea = renderOptions.Destination + BorderThickness;

                // Draw the border as its four edges.
                BorderBrush.Draw(context, renderOptions with { Destination = drawArea with { Height = BorderThickness.Top } });
                BorderBrush.Draw(context, renderOptions with { Destination = drawArea with { Width = BorderThickness.Left } });
                BorderBrush.Draw(context, renderOptions with { Destination = drawArea with { Height = BorderThickness.Bottom, Y = drawArea.Bottom - BorderThickness.Bottom } });
                BorderBrush.Draw(context, renderOptions with { Destination = drawArea with { Width = BorderThickness.Right, X = drawArea.Right - BorderThickness.Right } });
            }

            if (Child?.IsVisible == true)
                Child.Draw(context);
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            if (Child != null)
                yield return Child;
        }
    }
}
