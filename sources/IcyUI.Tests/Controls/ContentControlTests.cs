using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ContentControlTests
    {
        private static ControlTemplate LoadTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (ControlTemplate)loader.LoadObject(markup);
        }
        [Fact]
        public void Content_ForwardsToInternalChromeChild()
        {
            var contentControl = new ContentControl();
            var content = new UIElement();

            contentControl.Content = content;

            Assert.Same(content, contentControl.Content);

            // Content becomes the child of the internal chrome Border, not contentControl directly - see
            // Control.Chrome/ContentControl.Content forwarding to Chrome.Child.
            Assert.IsType<Border>(content.Parent);
        }

        [Fact]
        public void MeasureContent_IncludesContentSizeAndBorderThickness()
        {
            var contentControl = new ContentControl
            {
                BorderThickness = new Thickness(2),
                Content = new UIElement { Width = 40, Height = 20 },
            };

            Size measured = contentControl.Measure();

            Assert.Equal(new Size(44, 24), measured);
        }

        [Fact]
        public void ArrangeContent_PositionsContentInsetByBorderThickness()
        {
            var content = new UIElement { Width = 10, Height = 10 };
            var contentControl = new ContentControl
            {
                BorderThickness = new Thickness(5),
                Content = content,
            };

            contentControl.Arrange(new Rectangle(0, 0, 100, 100));

            // Content is Stretch-aligned by default with a fixed Width/Height, so it centers within the space left
            // after the 5px border inset on every side (90x90, from (5,5) to (95,95)) - see
            // UIElement.CalculateLocation's Stretch-without-NaN-size behavior, which falls back to centering.
            Assert.Equal(45, content.ActualBounds.X);
            Assert.Equal(45, content.ActualBounds.Y);
        }

        [Fact]
        public void Template_SetAndCleared_PreservesContent()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="ContentControl">
                  <ContentPresenter Content="{TemplateBinding Content}"/>
                </ControlTemplate>
                """);
            var content = new UIElement();
            var contentControl = new ContentControl { Content = content };

            contentControl.Template = template;
            Assert.Same(content, contentControl.Content);

            contentControl.Template = null;
            Assert.Same(content, contentControl.Content);
        }

        [Fact]
        public void Content_SetWhileTemplated_IsParentedToWhateverTheTemplateHostsItWith()
        {
            // Regression: Content's templated-mode setter used to explicitly wire the new value's Parent/Canvas
            // to `this` (the control) itself - unconditionally, *after* SetProperty had already synchronously
            // triggered {TemplateBinding Content}'s reactive update, which had already correctly parented it to
            // the ContentPresenter. That later assignment silently overwrote the correct one back to the control.
            // It went unnoticed on a templated Button whose own BorderThickness happened to match its template's
            // hardcoded one, masking the wrong ContentBounds this produced; it broke visibly (content measuring
            // against the control's own box instead of the presenter's, ignoring the border/padding the template
            // itself draws) on a control whose values differ - see ControlTemplateDemo's CheckBox.
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration);
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="ContentControl">
                  <ContentPresenter Content="{TemplateBinding Content}"/>
                </ControlTemplate>
                """);
            var contentControl = new ContentControl { Template = template };
            canvas.Add(contentControl);
            var content = new UIElement();

            contentControl.Content = content;

            var presenter = Assert.Single(contentControl.EnumerateVisualSubtree().OfType<ContentPresenter>());
            Assert.Same(presenter, content.Parent);
            Assert.NotSame(contentControl, content.Parent);
            Assert.Same(canvas, content.Canvas);
        }
    }
}
