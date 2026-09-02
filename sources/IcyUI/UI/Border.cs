// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Markup;
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
    [ContentProperty(nameof(Child))]
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
        /// <see cref="BorderThickness"/> and <see cref="UIElement.Padding"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="Background"/>/<see cref="BorderBrush"/> are unaffected by <see cref="UIElement.Padding"/> -
        /// they're drawn (in <see cref="OnRender"/>) across this <see cref="Border"/>'s full local bounds, not
        /// <see cref="ContentBounds"/>. Padding only pushes <see cref="Child"/> in from the border, matching the
        /// standard box model (CSS/WPF): the decoration covers the padding band, only the content is inset by it.
        /// </remarks>
        public Rectangle ContentBounds => ActualBounds - BorderThickness - Padding;

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            if (Child?.IsVisible == true)
                Child.Arrange();
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            if (Child?.IsVisible != true)
                return new Size(BorderThickness.Width, BorderThickness.Height);

            // UIElement.Measure()/DesiredSize deliberately excludes the element's own Margin (see its remarks) -
            // every container that sizes itself to a child's content, not just whatever space it's handed, must
            // add that child's Margin back in itself, the same way StackPanel/Grid already do at their own
            // Measure/Arrange call sites. Border used to skip this, so a Margin-bearing Child (e.g. Content set to
            // a label with a bottom Margin meant for stacking, reused as a Button's content) measured this Border
            // - and everything sized from it, up through Control/Button - short by exactly that Margin. That
            // shortfall then compounded at Arrange time: CalculateOverflow's Stretch branch subtracts the child's
            // own Margin from whatever (too-small) space it's given with no floor at the child's DesiredSize, so
            // the child rendered visibly smaller than its measured content - e.g. button labels clipped in half.
            Size childSize = Child.Measure();
            Thickness childMargin = Child.Margin;
            return new Size(
                childSize.Width + childMargin.Width + BorderThickness.Width,
                childSize.Height + childMargin.Height + BorderThickness.Height);
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            TextureRenderingOptions renderOptions = GetDefaultRenderOptions();

            Background?.Draw(context, renderOptions);

            if (BorderBrush != null && BorderThickness != Thickness.Zero)
            {
                // BorderThickness insets *into* ActualBounds (see ContentBounds => ActualBounds - BorderThickness,
                // the standard border-box convention) - the strips below must be computed directly against
                // renderOptions.Destination (the full (0,0,W,H) local box), not expanded outward by
                // "+ BorderThickness" (that operator grows a rect *outward*, the opposite of what's needed here).
                // The old code drew every strip partially or fully outside the element's own local bounds, which
                // ClipToBounds's scissor (set to exactly those bounds) then silently clipped away.
                Rectangle drawArea = renderOptions.Destination;

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
