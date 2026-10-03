using System.Windows.Input;
using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Markup;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Samples
{
    /// <summary>
    /// Pins MarkupDemo's numeric TextBox: a markup {Binding} (a DynamicPropertyPath) converts typed text into an int,
    /// and bad text leaves the view-model alone and marks the box Invalid.
    /// </summary>
    public class MarkupDemoTests
    {
        private static UIElement Build()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            return MarkupDemo.Build(configuration, "Airfool");
        }

        private static string Echo(UIElement root) => root.FindRequiredControl<TextBlock>("quantityEcho").Text;

        [Fact]
        public void QuantityBox_ShowsTheViewModelValue()
        {
            UIElement root = Build();

            Assert.Equal("3", root.FindRequiredControl<TextBox>("quantity").Text);
            Assert.Equal("View-model value: 3", Echo(root));
        }

        [Fact]
        public void QuantityBox_ParsesTypedNumbers()
        {
            UIElement root = Build();
            var box = root.FindRequiredControl<TextBox>("quantity");

            box.Text = "42";

            Assert.Equal("View-model value: 42", Echo(root));
            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Theory]
        [InlineData("4x")]
        [InlineData("-1")]
        [InlineData("")]
        public void QuantityBox_RejectsBadText_AndRecovers(string text)
        {
            UIElement root = Build();
            var box = root.FindRequiredControl<TextBox>("quantity");

            box.Text = text;

            Assert.Equal("View-model value: 3", Echo(root));
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));

            box.Text = "7";

            Assert.Equal("View-model value: 7", Echo(root));
            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void IncrementingTheOtherProperty_KeepsTheBadText()
        {
            UIElement root = Build();
            var box = root.FindRequiredControl<TextBox>("quantity");
            box.Text = "4x";

            object model = root.DataContext!;
            ((ICommand)model.GetType().GetProperty("IncrementCommand")!.GetValue(model)!).Execute(null);

            Assert.Equal("4x", box.Text);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));
        }
    }
}
