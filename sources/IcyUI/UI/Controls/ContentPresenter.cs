// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Markup;
using Icy.Rendering;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Marks the spot inside a <see cref="Icy.UI.Styles.ControlTemplate"/>'s content where a
    /// <see cref="ContentControl"/>'s real <see cref="Content"/> should be hosted.
    /// </summary>
    /// <remarks>
    /// A pure passthrough host - no background, border, or padding-band decoration of its own (unlike
    /// <see cref="Border"/>, which it otherwise mirrors closely). Typically wired as
    /// <c>&lt;ContentPresenter Content="{TemplateBinding Content}"/&gt;</c> inside a template.
    /// </remarks>
    [ContentProperty(nameof(Content))]
    public class ContentPresenter : UIElement, IContainerLayout
    {
        private UIElement? content;

        /// <summary>
        /// Gets or sets the element this presenter hosts.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Content
        {
            get => content;
            set
            {
                if (content == value)
                    return;
                if (content != null)
                {
                    content.Parent = null;
                    content.Canvas = null;
                }

                content = value;
                if (content != null)
                {
                    content.Parent = this;
                    content.Canvas = Canvas;
                }

                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets the area available to <see cref="Content"/>, i.e. <see cref="UIElement.ActualBounds"/> inset by
        /// <see cref="UIElement.Padding"/>.
        /// </summary>
        public Rectangle ContentBounds => ActualBounds - Padding;

        /// <inheritdoc/>
        /// <remarks>
        /// Force-invalidates <see cref="Content"/> before arranging it - same reasoning as
        /// <see cref="UI.Border.ArrangeContent"/> (<see cref="UIElement.Arrange(Rectangle)"/> no-ops once the
        /// target's own <c>IsArrangeInvalid</c> is already <see langword="false"/>, regardless of whether the
        /// rect changed), so <see cref="Content"/> doesn't get stuck at whatever size it happened to receive
        /// the first time this <see cref="ContentPresenter"/> was arranged.
        /// </remarks>
        protected override void ArrangeContent()
        {
            if (Content?.IsVisible == true)
            {
                Content.InvalidateArrange();
                Content.Arrange();
            }
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            if (Content?.IsVisible != true)
                return Size.Empty;

            // UIElement.Measure()/DesiredSize deliberately excludes the element's own Margin - every container
            // that sizes itself to a child's content, not just whatever space it's handed, must add that child's
            // Margin back in itself. See Border.MeasureContent's matching remarks for the regression this guards
            // against (a Margin-bearing child measuring short, then rendering visibly smaller than its content).
            Size contentSize = Content.Measure();
            Thickness contentMargin = Content.Margin;
            return new Size(contentSize.Width + contentMargin.Width, contentSize.Height + contentMargin.Height);
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            if (Content?.IsVisible == true)
                Content.Draw(context);
        }

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            if (Content != null)
                yield return Content;
        }
    }
}
