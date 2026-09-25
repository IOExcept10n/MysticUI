// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class HsvSquareTests
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
        public void Defaults_MatchSpec()
        {
            var square = new HsvSquare();

            Assert.Equal(0f, square.Hue);
            Assert.Equal(0f, square.Saturation);
            Assert.Equal(1f, square.Value);
        }

        [Fact]
        public void Saturation_Value_AreClampedTo0_1()
        {
            var square = new HsvSquare
            {
                Saturation = 1.5f,
                Value = -0.5f,
            };

            Assert.Equal(1f, square.Saturation);
            Assert.Equal(0f, square.Value);
        }

        [Fact]
        public void Dragging_UpdatesSaturationAndValue_FromThePointerPosition()
        {
            var (canvas, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var square = new HsvSquare
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            canvas.Add(square);
            canvas.Render();

            // Element should now be at (0, 0) to (100, 100) in canvas space = screen space
            var bounds = square.ActualBounds;
            bool raised = false;
            square.SaturationValueChanged += (_, _) => raised = true;

            // Drag to top-left corner (0, 0) in screen space -> Saturation=0, Value=1
            InvokeOnDragStarted(square, new Point(bounds.X, bounds.Y));

            Assert.True(raised);
            Assert.Equal(0f, square.Saturation, 2);
            Assert.Equal(1f, square.Value, 2);

            // Drag to bottom-right corner -> Saturation=1, Value=0
            InvokeOnDragPerforming(square, new Point(bounds.Right, bounds.Bottom));

            Assert.Equal(1f, square.Saturation, 2);
            Assert.Equal(0f, square.Value, 2);
        }

        private static void InvokeOnDragStarted(UIElement element, Point screenPoint) =>
            typeof(UIElement).GetMethod("OnDragStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(element, [screenPoint]);

        private static void InvokeOnDragPerforming(UIElement element, Point screenPoint) =>
            typeof(UIElement).GetMethod("OnDragPerforming", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(element, [screenPoint]);
    }
}
