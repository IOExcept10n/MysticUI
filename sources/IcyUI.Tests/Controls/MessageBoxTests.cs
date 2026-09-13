using System.Linq;
using System.Threading.Tasks;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class MessageBoxTests
    {
        private static Canvas CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return new Canvas(config);
        }

        private static Button[] GetButtons(Canvas canvas) =>
            canvas.Overlays.OfType<Dialog>().Single().Content!.EnumerateVisualSubtree().OfType<Button>().ToArray();

        private static string ButtonText(Button button) => ((TextBlock)button.Content!).Text;

        [Theory]
        [InlineData(DialogButtons.OK, new[] { DialogResult.OK })]
        [InlineData(DialogButtons.OKCancel, new[] { DialogResult.OK, DialogResult.Cancel })]
        [InlineData(DialogButtons.YesNo, new[] { DialogResult.Yes, DialogResult.No })]
        [InlineData(DialogButtons.YesNoCancel, new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel })]
        public void ShowAsync_BuildsExactlyTheExpectedButtonSet(DialogButtons buttons, DialogResult[] expected)
        {
            var canvas = CreateCanvas();

            _ = MessageBox.ShowAsync(canvas, "message", buttons);

            Button[] built = GetButtons(canvas);
            Assert.Equal(expected.Select(r => r.ToString()), built.Select(ButtonText));
        }

        [Fact]
        public async Task ShowAsync_ClickingAButton_ResolvesWithTheMatchingResult()
        {
            var canvas = CreateCanvas();
            Task<DialogResult> task = MessageBox.ShowAsync(canvas, "Proceed?", DialogButtons.OKCancel);

            Button cancel = GetButtons(canvas).Single(b => ButtonText(b) == "Cancel");
            cancel.OnTap();

            Assert.Equal(DialogResult.Cancel, await task);
        }

        [Fact]
        public void ShowInputAsync_PreFillsTheTextBoxWithDefaultValue()
        {
            var canvas = CreateCanvas();

            _ = MessageBox.ShowInputAsync(canvas, "Enter a value:", "hello");

            var textBox = canvas.Overlays.OfType<Dialog>().Single().Content!.EnumerateVisualSubtree().OfType<TextBox>().Single();
            Assert.Equal("hello", textBox.Text);
        }

        [Fact]
        public async Task ShowInputAsync_OkButton_ResolvesWithTheCurrentTextBoxValue()
        {
            var canvas = CreateCanvas();
            Task<string?> task = MessageBox.ShowInputAsync(canvas, "Enter a value:", "hello");

            var content = canvas.Overlays.OfType<Dialog>().Single().Content!;
            var textBox = content.EnumerateVisualSubtree().OfType<TextBox>().Single();
            textBox.Text = "changed";
            content.EnumerateVisualSubtree().OfType<Button>().Single(b => ButtonText(b) == "OK").OnTap();

            Assert.Equal("changed", await task);
        }

        [Fact]
        public async Task ShowInputAsync_CancelButton_ResolvesWithNull()
        {
            var canvas = CreateCanvas();
            Task<string?> task = MessageBox.ShowInputAsync(canvas, "Enter a value:", "hello");

            GetButtons(canvas).Single(b => ButtonText(b) == "Cancel").OnTap();

            Assert.Null(await task);
        }
    }
}
