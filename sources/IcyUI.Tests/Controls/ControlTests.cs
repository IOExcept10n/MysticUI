using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Rendering.Brushes;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ControlTests
    {
        private static UIElement GetChrome(Control control) =>
            (UIElement)typeof(Control).GetProperty("Chrome", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;

        private static ControlTemplate LoadTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (ControlTemplate)loader.LoadObject(markup);
        }

        private sealed class TestControl : Control
        {
            public int OnApplyTemplateCallCount { get; private set; }

            public T? FindTemplateChild<T>(string name)
                where T : UIElement => GetTemplateChild<T>(name);

            protected override void OnApplyTemplate()
            {
                base.OnApplyTemplate();
                OnApplyTemplateCallCount++;
            }
        }

        [Fact]
        public void Background_ForwardsToInternalChrome()
        {
            var control = new Control();
            var brush = new SolidColorBrush(Color.Red);

            control.Background = brush;

            Assert.Same(brush, control.Background);
        }

        [Fact]
        public void BorderThickness_ForwardsToInternalChrome()
        {
            var control = new Control { BorderThickness = new Thickness(2, 3, 4, 5) };

            Assert.Equal(new Thickness(2, 3, 4, 5), control.BorderThickness);
        }

        [Fact]
        public void MeasureContent_WithNoContent_EqualsBorderThickness()
        {
            var control = new Control { BorderThickness = new Thickness(5) };

            Size measured = control.Measure();

            Assert.Equal(new Size(10, 10), measured);
        }

        [Fact]
        public void Draw_DrawsInternalChromeBackground()
        {
            var control = new Control { Background = new SolidColorBrush(Color.Blue) };
            var context = new FakeRenderContext();

            control.Arrange(new Rectangle(0, 0, 50, 50));
            control.Draw(context);

            Assert.Single(context.DrawCalls);
        }

        [Fact]
        public void Padding_ForwardsToInternalChrome()
        {
            var control = new Control { Padding = new Thickness(2, 3, 4, 5) };

            Assert.Equal(new Thickness(2, 3, 4, 5), control.Padding);
        }

        [Fact]
        public void Padding_IsNotDoubleCountedInMeasure()
        {
            // Regression: Control.Padding forwards to Chrome.Padding (like Background/BorderBrush/BorderThickness
            // already do) so Chrome's own Measure() adds it exactly once - Control's own base UIElement.Padding
            // field must stay untouched (always zero) or the generic UIElement.Measure() padding step would add
            // it again on top, doubling it.
            var control = new Control { Padding = new Thickness(5) };

            Size measured = control.Measure();

            Assert.Equal(new Size(10, 10), measured);
        }

        [Fact]
        public void ContentBounds_InsetsByBothBorderThicknessAndPadding()
        {
            // Regression: Control.ContentBounds used to subtract only Padding, ignoring BorderThickness entirely -
            // the actual content-available area needs both.
            var control = new Control { BorderThickness = new Thickness(2), Padding = new Thickness(3) };
            control.Arrange(new Rectangle(0, 0, 100, 100));

            Assert.Equal(new Rectangle(5, 5, 90, 90), control.ContentBounds);
        }

        [Fact]
        public void Draw_ChromeBackgroundCoversFullBoundsIncludingPadding()
        {
            // Regression: Control.ArrangeContent used to arrange Chrome into ContentBounds (already inset by
            // Padding), shrinking Background/BorderBrush - drawn across Chrome's own full local bounds - down to
            // roughly the size of the content. Background must cover the whole control (including the padding
            // band), matching the standard box model; only the actual content is inset by Padding.
            var control = new Control
            {
                Background = new SolidColorBrush(Color.Blue),
                Padding = new Thickness(10),
            };

            control.Arrange(new Rectangle(0, 0, 50, 50));

            var chrome = GetChrome(control);

            Assert.Equal(control.ActualBounds, chrome.ActualBounds);
        }

        [Fact]
        public void Template_Set_ReplacesChromeWithTemplateContent()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Image/></ControlTemplate>""");
            var control = new Control();

            control.Template = template;

            Assert.IsType<Image>(GetChrome(control));
        }

        [Fact]
        public void Template_SetThenCleared_RestoresDefaultBorderChrome()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Image/></ControlTemplate>""");
            var control = new Control { Template = template };

            control.Template = null;

            Assert.IsType<Border>(GetChrome(control));
        }

        [Fact]
        public void Template_SetAndCleared_PreservesBackgroundBorderBrushBorderThicknessPadding()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Border/></ControlTemplate>""");
            var background = new SolidColorBrush(Color.Red);
            var borderBrush = new SolidColorBrush(Color.Blue);
            var control = new Control
            {
                Background = background,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(2, 3, 4, 5),
                Padding = new Thickness(6, 7, 8, 9),
            };

            control.Template = template;
            Assert.Same(background, control.Background);
            Assert.Same(borderBrush, control.BorderBrush);
            Assert.Equal(new Thickness(2, 3, 4, 5), control.BorderThickness);
            Assert.Equal(new Thickness(6, 7, 8, 9), control.Padding);

            control.Template = null;
            Assert.Same(background, control.Background);
            Assert.Same(borderBrush, control.BorderBrush);
            Assert.Equal(new Thickness(2, 3, 4, 5), control.BorderThickness);
            Assert.Equal(new Thickness(6, 7, 8, 9), control.Padding);
        }

        [Fact]
        public void Template_Set_UnbindsOldChromeSubtree()
        {
            var firstTemplate = LoadTemplate("""<ControlTemplate TargetType="Control"><Border Background="{TemplateBinding Background}"/></ControlTemplate>""");
            var secondTemplate = LoadTemplate("""<ControlTemplate TargetType="Control"><Image/></ControlTemplate>""");
            var control = new Control { Template = firstTemplate };
            var firstChrome = (Border)GetChrome(control);

            control.Template = secondTemplate;
            control.Background = new SolidColorBrush(Color.Green);

            // The old Chrome's {TemplateBinding} would have updated firstChrome.Background here if it were still
            // subscribed to this control's PropertyChanged - it must not be, or it leaks for this control's whole
            // remaining lifetime.
            Assert.NotEqual(Color.Green.ToArgb(), ((SolidColorBrush)firstChrome.Background).Color.ToArgb());
        }

        [Fact]
        public void Template_Set_InvalidatesMeasureAndArrange()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Image/></ControlTemplate>""");
            var control = new Control();
            control.Arrange(new Rectangle(0, 0, 50, 50));

            control.Template = template;

            Assert.True((bool)typeof(UIElement).GetProperty("IsMeasureInvalid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!);
        }

        [Fact]
        public void Template_SetWhileAttachedToCanvas_NewChromeInheritsCanvas()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration);
            var control = new Control();
            canvas.Add(control);
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Image/></ControlTemplate>""");

            control.Template = template;

            Assert.Same(canvas, GetChrome(control).Canvas);
        }

        [Fact]
        public void Template_MismatchedTargetType_Throws()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Button"><Image/></ControlTemplate>""");
            var control = new Control();

            Assert.Throws<ArgumentException>(() => control.Template = template);
        }

        [Fact]
        public void Template_LoadContent_ProducesDistinctChromePerControlInstance()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Image/></ControlTemplate>""");
            var first = new Control { Template = template };
            var second = new Control { Template = template };

            Assert.NotSame(GetChrome(first), GetChrome(second));
        }

        [Fact]
        public void GetTemplateChild_FindsNamedElementInsideTheCurrentTemplate()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Border><Image x:Name="PART_Icon"/></Border></ControlTemplate>""");
            var control = new TestControl { Template = template };

            Assert.NotNull(control.FindTemplateChild<Image>("PART_Icon"));
        }

        [Fact]
        public void GetTemplateChild_UnknownName_ReturnsNull()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Border><Image x:Name="PART_Icon"/></Border></ControlTemplate>""");
            var control = new TestControl { Template = template };

            Assert.Null(control.FindTemplateChild<Image>("PART_DoesNotExist"));
        }

        [Fact]
        public void GetTemplateChild_WrongType_ReturnsNull()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Border><Image x:Name="PART_Icon"/></Border></ControlTemplate>""");
            var control = new TestControl { Template = template };

            Assert.Null(control.FindTemplateChild<Border>("PART_Icon"));
        }

        [Fact]
        public void GetTemplateChild_NoTemplateApplied_ReturnsNull()
        {
            var control = new TestControl();

            Assert.Null(control.FindTemplateChild<UIElement>("PART_Anything"));
        }

        [Fact]
        public void GetTemplateChild_DoesNotSearchOutsideTheTemplatesOwnScope()
        {
            // Regression guard for the reason this doesn't just reuse FindControl<T>: an element named the same
            // as a template part, but declared in the OUTER document (not inside this control's own template),
            // must not be found - FindControl<T> would find it (it searches from element.GetRoot()), but
            // GetTemplateChild must not, since it's scoped to Chrome's own isolated MarkupNameScope only.
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            var outerDocument = loader.Load("""<StackPanel><Image x:Name="PART_Icon"/></StackPanel>""");
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Border/></ControlTemplate>""");
            var control = new TestControl { Template = template };
            ((Panel)outerDocument).Children.Add(control);

            Assert.Null(control.FindTemplateChild<Image>("PART_Icon"));
        }

        [Fact]
        public void OnApplyTemplate_CalledExactlyOncePerSwap_BothDirections()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="Control"><Image/></ControlTemplate>""");
            var control = new TestControl();
            Assert.Equal(0, control.OnApplyTemplateCallCount);

            control.Template = template;
            Assert.Equal(1, control.OnApplyTemplateCallCount);

            control.Template = null;
            Assert.Equal(2, control.OnApplyTemplateCallCount);
        }

        [Fact]
        public void ArrangeContent_ReArrangedToANewSize_ChromeFollows()
        {
            // Regression (same bug class as Border.ArrangeContent - see [[project_icyui_tier2_roadmap]]):
            // Chrome.Arrange(ActualBounds) no-ops once Chrome's own IsArrangeInvalid is already false, so
            // without an explicit Chrome.InvalidateArrange() first, Chrome (and therefore every control's own
            // Background/BorderBrush/Content decoration) gets stuck at whatever size it happened to receive the
            // first time this control was arranged - even after the control's own ActualBounds later changes
            // size around it (e.g. a StackPanel/Grid slot resizing across layout passes, or any other
            // already-arranged element being given a new size on a later pass).
            var control = new TestControl();
            control.Arrange(new Rectangle(0, 0, 50, 20));
            var chrome = GetChrome(control);
            int firstChromeHeight = chrome.ActualBounds.Height;

            control.InvalidateArrange();
            control.Arrange(new Rectangle(0, 0, 200, 200));

            Assert.NotEqual(firstChromeHeight, chrome.ActualBounds.Height);
            Assert.Equal(200, chrome.ActualBounds.Height);
        }
    }
}
