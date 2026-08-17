using System.Windows.Input;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

// ICommand requires CanExecuteChanged even though FakeCommand never needs to raise it in these tests.
#pragma warning disable CS0067

namespace Icy.Tests.Controls
{
    public class ButtonTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input);
        }

        private sealed class FakeCommand(Func<bool> canExecute) : ICommand
        {
            public bool ExecutedCalled { get; private set; }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => canExecute();

            public void Execute(object? parameter) => ExecutedCalled = true;
        }

        [Fact]
        public void IsFocusable_DefaultsTrue()
        {
            var button = new Button();

            Assert.True(button.IsFocusable);
        }

        [Fact]
        public void Tap_RaisesClick()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var button = new Button
            {
                Width = 100,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(button);
            canvas.Render();

            bool clicked = false;
            button.Click += (_, _) => clicked = true;

            input.Events.Touch.RaiseTap(new TouchInfo(new System.Drawing.Point(50, 20), 1));

            Assert.True(clicked);
        }

        [Fact]
        public void Tap_ExecutesCommand_WhenCanExecute()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var command = new FakeCommand(() => true);
            var button = new Button
            {
                Width = 100,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Command = command,
            };
            canvas.Add(button);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new System.Drawing.Point(50, 20), 1));

            Assert.True(command.ExecutedCalled);
        }

        [Fact]
        public void Tap_DoesNotExecuteCommand_WhenCanExecuteIsFalse()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var command = new FakeCommand(() => false);
            var button = new Button
            {
                Width = 100,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Command = command,
            };
            canvas.Add(button);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new System.Drawing.Point(50, 20), 1));

            Assert.False(command.ExecutedCalled);
        }
    }
}
