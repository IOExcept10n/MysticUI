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
    public class DialogOfTResultTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input);
        }

        [Fact]
        public void IsOpen_MirrorsTheUnderlyingDialog()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog<int>();
            Assert.False(dialog.IsOpen);

            dialog.ShowAsync(canvas);

            Assert.True(dialog.IsOpen);
        }

        [Fact]
        public async Task Close_CompletesShowAsyncsTaskWithTheGivenResult()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog<int>();
            var task = dialog.ShowAsync(canvas);

            dialog.Close(42);

            Assert.Equal(42, await task);
        }

        [Fact]
        public void ShowAsync_WhileAlreadyOpen_ReturnsTheSameTaskInstance()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog<string?>();

            var first = dialog.ShowAsync(canvas);
            var second = dialog.ShowAsync(canvas);

            Assert.Same(first, second);
        }

        [Fact]
        public void Close_BeforeShowAsync_IsANoOp()
        {
            var dialog = new Dialog<int>();

            var exception = Record.Exception(() => dialog.Close(1));

            Assert.Null(exception);
            Assert.False(dialog.IsOpen);
        }

        [Fact]
        public async Task Close_CalledTwice_OnlyCompletesTheTaskWithTheFirstResult()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog<int>();
            var task = dialog.ShowAsync(canvas);

            dialog.Close(1);
            var exception = Record.Exception(() => dialog.Close(2));

            Assert.Null(exception);
            Assert.Equal(1, await task);
        }

        [Fact]
        public async Task EscapeDrivenClose_CompletesTheTaskWithDefault()
        {
            var (canvas, input) = CreateCanvas();
            var dialog = new Dialog<int>();
            var task = dialog.ShowAsync(canvas);

            input.Events.Navigation.RaiseCloseModal();

            Assert.Equal(default, await task);
            Assert.False(dialog.IsOpen);
        }
    }
}
