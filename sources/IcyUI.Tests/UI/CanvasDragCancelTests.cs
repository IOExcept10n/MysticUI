// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasDragCancelTests
    {
        [Fact]
        public void ACancelledDrag_CallsOnDragCanceled()
        {
            (Canvas canvas, FakeInputSystem input) = Create();
            var element = Place(new CancelAwareElement(DragAxes.Both));
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(new Point(10, 10), new Point(30, 10)));

            Assert.Equal(["start", "cancel"], element.Log);
        }

        [Fact]
        public void TheDefaultCancel_StillEndsTheDrag()
        {
            (Canvas canvas, FakeInputSystem input) = Create();
            var element = Place(new AxisElement(DragAxes.Both));
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(new Point(10, 10), new Point(30, 10)));

            Assert.Equal("end", element.Log[^1]);
        }

        private static T Place<T>(T element)
            where T : UIElement
        {
            element.Width = 100;
            element.Height = 100;
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;
            return element;
        }

        private static DragInfo Drag(Point start, Point position) =>
            new(PointerKind.MouseLeft, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        private static (Canvas Canvas, FakeInputSystem Input) Create()
        {
            var input = new FakeInputSystem();
            var canvas = new Canvas(new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()))
            {
                IsInputEnabled = true,
                IsVisible = true,
            };
            return (canvas, input);
        }
    }
}
