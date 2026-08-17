using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.UI
{
    /// <summary>
    /// Phase 7 coverage: Canvas's pointer dispatch bubbles from the exact hit-tested leaf up through every
    /// ancestor (<see cref="ControlState.Hovered"/>/<see cref="ControlState.Pressed"/>, <see cref="UIElement.OnTap"/>,
    /// the drag hooks) - added because Phase 6 makes it possible to nest one interactive element inside another
    /// (e.g. a Button's TextBlock label), and a direct-hit-only dispatch would leave the outer control never
    /// reacting to input landing on its own content.
    /// </summary>
    public class CanvasEventBubblingTests
    {
        private static (Canvas Canvas, FakeInputSystem Input, FakeRenderContext RenderContext) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input, renderContext);
        }

        private static (Border Outer, UIElement Leaf) CreateNestedPair(int size = 100)
        {
            var leaf = new UIElement { Width = size, Height = size };
            var outer = new Border
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = leaf,
            };
            return (outer, leaf);
        }

        [Fact]
        public void Hover_BubblesToAncestor()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var (outer, leaf) = CreateNestedPair();
            canvas.Add(outer);

            input.Mouse.MouseInfo = new(new Point(50, 50));
            canvas.Render();

            Assert.Equal(ControlState.Hovered, leaf.ControlState & ControlState.Hovered);
            Assert.Equal(ControlState.Hovered, outer.ControlState & ControlState.Hovered);
        }

        [Fact]
        public void Hover_MovingBetweenLeaves_KeepsCommonAncestorHovered()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var leafA = new UIElement { Width = 40, Height = 100, HorizontalAlignment = HorizontalAlignment.Left };
            var leafB = new UIElement { Width = 40, Height = 100, HorizontalAlignment = HorizontalAlignment.Right };
            var outer = new Panel
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            outer.Children.Add(leafA);
            outer.Children.Add(leafB);
            canvas.Add(outer);

            input.Mouse.MouseInfo = new(new Point(10, 50));
            canvas.Render();
            Assert.Equal(ControlState.Hovered, outer.ControlState & ControlState.Hovered);

            bool outerToggledOff = false;
            outer.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UIElement.ControlState) && (outer.ControlState & ControlState.Hovered) == 0)
                    outerToggledOff = true;
            };

            input.Mouse.MouseInfo = new(new Point(90, 50));
            canvas.Render();

            Assert.False(outerToggledOff);
            Assert.Equal(ControlState.Hovered, outer.ControlState & ControlState.Hovered);
            Assert.Equal(ControlState.Normal, leafA.ControlState & ControlState.Hovered);
            Assert.Equal(ControlState.Hovered, leafB.ControlState & ControlState.Hovered);
        }

        [Fact]
        public void TouchDownUp_BubblesPressedToAncestor()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var (outer, leaf) = CreateNestedPair();
            canvas.Add(outer);
            canvas.Render();

            input.Events.Touch.RaiseTouchDown(new Point(50, 50));
            Assert.Equal(ControlState.Pressed, leaf.ControlState & ControlState.Pressed);
            Assert.Equal(ControlState.Pressed, outer.ControlState & ControlState.Pressed);

            input.Events.Touch.RaiseTouchUp(new Point(50, 50));
            Assert.Equal(ControlState.Normal, leaf.ControlState & ControlState.Pressed);
            Assert.Equal(ControlState.Normal, outer.ControlState & ControlState.Pressed);
        }

        [Fact]
        public void Tap_BubblesOnTapToAncestor()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var leaf = new UIElement { Width = 100, Height = 100 };
            var outer = new RecordingElement
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = leaf,
            };
            canvas.Add(outer);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(50, 50), 1));

            Assert.Equal(1, outer.TapCount);
        }

        [Fact]
        public void Tap_FocusesNearestFocusableAncestor_WhenLeafItselfIsNotFocusable()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var leaf = new UIElement { Width = 100, Height = 100 };
            var outer = new Border
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsFocusable = true,
                Child = leaf,
            };
            canvas.Add(outer);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(50, 50), 1));

            Assert.Same(outer, canvas.FocusedElement);
        }

        [Fact]
        public void Drag_RoutesToElementHitAtDragStart_EvenAsCursorLeavesItsBounds()
        {
            var (canvas, input, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var target = new RecordingElement
            {
                Width = 50,
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(target);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(25, 25));
            input.Events.Drag.RaiseDragPerforming(new Point(500, 500));
            input.Events.Drag.RaiseDragEnded(new Point(500, 500));

            Assert.Equal(1, target.DragStartedCount);
            Assert.Equal(1, target.DragPerformingCount);
            Assert.Equal(1, target.DragEndedCount);
        }

        private sealed class RecordingElement : Border
        {
            public int DragEndedCount { get; private set; }

            public int DragPerformingCount { get; private set; }

            public int DragStartedCount { get; private set; }

            public int TapCount { get; private set; }

            protected internal override void OnDragEnded(Point screenPoint) => DragEndedCount++;

            protected internal override void OnDragPerforming(Point screenPoint) => DragPerformingCount++;

            protected internal override void OnDragStarted(Point screenPoint) => DragStartedCount++;

            protected internal override void OnTap() => TapCount++;
        }
    }
}
