using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class DialogTests
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
        public void IsOpen_TracksShowAndClose()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog();
            Assert.False(dialog.IsOpen);

            dialog.Show(canvas);
            Assert.True(dialog.IsOpen);

            dialog.Close();
            Assert.False(dialog.IsOpen);
        }

        [Fact]
        public void Show_AddsToCanvasOverlays()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog();

            dialog.Show(canvas);

            Assert.Contains(dialog, canvas.Overlays);
        }

        [Fact]
        public void Close_RemovesFromCanvasOverlays()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog();
            dialog.Show(canvas);

            dialog.Close();

            Assert.DoesNotContain(dialog, canvas.Overlays);
        }

        [Fact]
        public void Show_WhileAlreadyOpen_DoesNotAddASecondOverlayEntry()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog();
            dialog.Show(canvas);

            dialog.Show(canvas);

            Assert.Single(canvas.Overlays);
        }

        [Fact]
        public void Close_BeforeShow_IsANoOpAndDoesNotRaiseClosed()
        {
            var dialog = new Dialog();
            bool closedRaised = false;
            dialog.Closed += (_, _) => closedRaised = true;

            var exception = Record.Exception(() => dialog.Close());

            Assert.Null(exception);
            Assert.False(closedRaised);
        }

        [Fact]
        public void Close_CalledTwice_OnlyRaisesClosedOnce()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog();
            dialog.Show(canvas);
            int closedCount = 0;
            dialog.Closed += (_, _) => closedCount++;

            dialog.Close();
            dialog.Close();

            Assert.Equal(1, closedCount);
        }

        [Fact]
        public void Show_SetsIsFocusScope()
        {
            var (canvas, _) = CreateCanvas();
            var dialog = new Dialog();

            dialog.Show(canvas);

            Assert.True(dialog.IsFocusScope);
        }

        [Fact]
        public void Close_RestoresFocusToElementFocusedBeforeShow()
        {
            var (canvas, _) = CreateCanvas();
            var opener = new Button { IsFocusable = true };
            canvas.Add(opener);
            canvas.Focus(opener);

            var dialogButton = new Button();
            var dialog = new Dialog { Content = dialogButton };

            dialog.Show(canvas);
            Assert.Same(dialogButton, canvas.FocusedElement);

            dialog.Close();

            Assert.Same(opener, canvas.FocusedElement);
        }

        [Fact]
        public void CloseModal_WhileOpen_ClosesTheDialogAndRaisesClosed()
        {
            var (canvas, input) = CreateCanvas();
            var dialog = new Dialog();
            dialog.Show(canvas);
            bool closedRaised = false;
            dialog.Closed += (_, _) => closedRaised = true;

            input.Events.Navigation.RaiseCloseModal();

            Assert.False(dialog.IsOpen);
            Assert.True(closedRaised);
        }

        [Fact]
        public void CloseModal_AfterAlreadyClosed_DoesNotThrowOrDoubleFire()
        {
            var (canvas, input) = CreateCanvas();
            var dialog = new Dialog();
            dialog.Show(canvas);
            dialog.Close();
            int closedCount = 0;
            dialog.Closed += (_, _) => closedCount++;

            var exception = Record.Exception(() => input.Events.Navigation.RaiseCloseModal());

            Assert.Null(exception);
            Assert.Equal(0, closedCount);
        }

        [Fact]
        public void TwoDialogsShownAtOnce_StackIndependently()
        {
            var (canvas, _) = CreateCanvas();
            var first = new Dialog();
            var second = new Dialog();

            first.Show(canvas);
            second.Show(canvas);

            Assert.Equal(2, canvas.Overlays.Count);

            first.Close();

            Assert.True(second.IsOpen);
            Assert.Single(canvas.Overlays);
            Assert.Contains(second, canvas.Overlays);
        }

        [Fact]
        public void CloseModal_AfterRender_RestoresFocusToOpener_NotNull()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var opener = new Button { IsFocusable = true };
            canvas.Add(opener);
            canvas.Focus(opener);
            canvas.Render(); // wires up Canvas's own competing OnCloseModal handler, same as CanvasHitTestFocusTests does

            var dialogButton = new Button();
            var dialog = new Dialog { Content = dialogButton };
            dialog.Show(canvas);
            Assert.Same(dialogButton, canvas.FocusedElement);

            input.Events.Navigation.RaiseCloseModal();

            Assert.Same(opener, canvas.FocusedElement);
            Assert.False(dialog.IsOpen);
        }

        [Fact]
        public void CloseModal_TwoStackedDialogs_ClosesOnlyTheTopmost()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            canvas.Render();

            var first = new Dialog();
            var second = new Dialog();
            first.Show(canvas);
            second.Show(canvas);

            input.Events.Navigation.RaiseCloseModal();

            Assert.False(second.IsOpen);
            Assert.True(first.IsOpen);
            Assert.Single(canvas.Overlays);
            Assert.Contains(first, canvas.Overlays);
        }
    }
}
