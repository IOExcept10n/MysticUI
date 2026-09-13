using System.Collections.Generic;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
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

        private static (Canvas Canvas, FakeInputSystem Input) CreateThemedCanvas()
        {
            var input = new FakeInputSystem();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets();
            var config = builder.Build().UseDefaultTheme();
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
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

        [Fact]
        public void CloseModal_WithOpenDropdownPopupInsideDialog_ClosesOnlyThePopup_NotTheDialog()
        {
            var (canvas, input) = CreateCanvas();

            // An ItemTemplate is required here purely so the popup can realize its containers when opened below
            // (Dropdown has no default ItemTemplate of its own - see DefaultTheme.xml's Dropdown Style - and this
            // test uses the plain, unthemed CreateCanvas, so there's no theme-supplied one either); it has nothing
            // to do with what this test is actually verifying (Escape closing the popup, not the dialog).
            //
            // IsFocusable starts false and is flipped to true (with an explicit Focus call) only after Show()
            // returns - deliberately, not just for style: Dropdown defaults IsFocusable to true, and Dialog.Show
            // auto-focuses the first focusable descendant of its content as part of the SAME call that later
            // subscribes the dialog's own OnCloseModal handler - so if the dropdown were already focusable when
            // Show() ran, its Selector base would auto-subscribe its own (focus-gated) CloseModal handler *before*
            // Dialog's, making Selector's handler close the popup first and leaving Dialog's widened topmost-overlay
            // check to observe a stale, already-collapsed Overlays list. Deferring focus until after Show() returns
            // subscribes Dialog's handler first - matching the ordering a dialog with more than one focusable
            // descendant (the common case) would naturally have - so this test observes the intended interaction:
            // Dialog's check runs first (with the popup still topmost, so it does not close), then Selector's own
            // handler runs and closes just the popup.
            var dropdown = new Dropdown
            {
                ItemsSource = new List<object> { "One", "Two" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                IsFocusable = false,
            };
            var dialog = new Dialog { Content = dropdown };
            dialog.Show(canvas);

            dropdown.IsFocusable = true;
            canvas.Focus(dropdown);
            dropdown.IsOpen = true;

            input.Events.Navigation.RaiseCloseModal();

            Assert.False(dropdown.IsOpen);
            Assert.True(dialog.IsOpen);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        [Fact]
        public void ThemedDialog_AppliesWithoutThrowing()
        {
            var (canvas, _) = CreateThemedCanvas();
            var dialog = new Dialog();

            var exception = Record.Exception(() =>
            {
                dialog.Show(canvas);
                canvas.Render();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void ThemedDialog_BackdropBlocksTapsToContentBehindIt()
        {
            var (canvas, input) = CreateThemedCanvas();
            var behind = new Button
            {
                Width = 100,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(behind);
            canvas.Render();

            bool clicked = false;
            behind.Click += (_, _) => clicked = true;

            var dialog = new Dialog();
            dialog.Show(canvas);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new System.Drawing.Point(50, 20), 1));

            Assert.False(clicked);
        }
    }
}
