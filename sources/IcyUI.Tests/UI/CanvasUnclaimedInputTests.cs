// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasUnclaimedInputTests
    {
        private static (Canvas Canvas, FakeInputSystem Input, ScrollViewer Viewer) Create()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var viewer = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = new Border { Width = 100, Height = 500 },
            };
            canvas.Add(viewer);
            canvas.Render();
            return (canvas, input, viewer);
        }

        private static Border Cover(Canvas canvas, bool passes)
        {
            var overlay = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                PassesUnclaimedInput = passes,
            };
            canvas.AddOverlay(overlay);
            canvas.Render();
            return overlay;
        }

        private static void Hover(Canvas canvas, FakeInputSystem input, Point point)
        {
            input.Mouse.MouseInfo = new MouseInfo(point);
            canvas.Render();
        }

        private static DragInfo Drag(PointerKind kind, Point start, Point position) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        [Fact]
        public void TheWheel_OverAFallThroughOverlay_ScrollsTheViewerBeneath()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: true);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, viewer.VerticalOffset);
        }

        [Fact]
        public void TheWheel_OverAnOrdinaryOverlay_IsBlocked()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: false);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void TheWheel_FallsThroughTwoStackedOverlays()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: true);
            Cover(canvas, passes: true);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, viewer.VerticalOffset);
        }

        [Fact]
        public void TheWheel_ConsumedInsideTheOverlay_DoesNotFallThrough()
        {
            var (canvas, input, viewer) = Create();
            var inner = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = new Border { Width = 100, Height = 500 },
                PassesUnclaimedInput = true,
            };
            canvas.AddOverlay(inner);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, inner.VerticalOffset);
            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void AMiddleDrag_OverAFallThroughOverlay_PansTheViewerBeneath()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: true);

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 60)));
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 40)));
            input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 40)));

            // Panning follows the pointer 1:1 from where it crossed the drag threshold (60), as ScrollViewerPanningTests pins.
            Assert.Equal(20, viewer.VerticalOffset);
        }

        [Fact]
        public void AMiddleDrag_OverAnOrdinaryOverlay_IsBlocked()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: false);

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 60)));
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 40)));

            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void ADragTheOverlayClaims_StaysWithTheOverlay()
        {
            var (canvas, input, viewer) = Create();
            var overlay = new AxisElement(DragAxes.Both)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                PassesUnclaimedInput = true,
            };
            canvas.AddOverlay(overlay);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 60)));

            Assert.Equal(new[] { "start 50,80", "move 50,60" }, overlay.Log);
            Assert.Equal(0, viewer.VerticalOffset);
        }
    }
}
