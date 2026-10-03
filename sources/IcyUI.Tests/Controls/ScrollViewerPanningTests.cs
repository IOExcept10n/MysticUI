using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.Tests.UI;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ScrollViewerPanningTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) Create(float displayScale = 1f)
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext { DisplayScale = displayScale }, new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            return (canvas, input);
        }

        // A 200x200 viewport at the canvas origin over a 200x1000 column: the top 50 units are an AxisElement claiming
        // Horizontal (a slider stand-in).
        private static (ScrollViewer List, AxisElement Slider) AddList(Canvas canvas)
        {
            var slider = new AxisElement(DragAxes.Horizontal) { Height = 50 };
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(slider);
            column.Children.Add(new UIElement { Height = 950 });
            var list = new ScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.Add(list);
            canvas.Render();
            return (list, slider);
        }

        private static DragInfo Drag(PointerKind kind, Point start, Point position) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        private static void Pan(FakeInputSystem input, PointerKind kind, Point start, Point crossing, Point end)
        {
            input.Events.Gestures.RaiseDragStarted(Drag(kind, start, crossing));
            input.Events.Gestures.RaiseDragMoved(Drag(kind, start, end));
            input.Events.Gestures.RaiseDragCanceled(Drag(kind, start, end)); // end without a fling
        }

        private sealed class DraggableRow : UIElement, Icy.Input.DragDrop.IDragSource
        {
            public bool TryBeginDrag(Point screenPoint, out object? payload, out UIElement? preview)
            {
                payload = new object();
                preview = new UIElement();
                return true;
            }
        }

        [Fact]
        public void TouchHoldOnADraggableRow_StartsDragDrop_WithoutPanningTheList()
        {
            // Regression: the held row's drag-drop session and a 1:1 pan of the list both started, so the content slid away
            // under the finger together with the ghost and the drop target barely changed.
            var (canvas, input) = Create();
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(new DraggableRow { Height = 50 });
            column.Children.Add(new UIElement { Height = 950 });
            var list = new ScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.Add(list);
            canvas.Render();
            var start = new Point(100, 40);

            input.Events.Gestures.RaiseDragStarted(new DragInfo(PointerKind.Touch, start, new Point(100, 20), new Vector2(0, -20), Vector2.Zero, StartedFromHold: true));
            input.Events.Gestures.RaiseDragMoved(new DragInfo(PointerKind.Touch, start, new Point(100, 0), new Vector2(0, -20), Vector2.Zero, StartedFromHold: true));

            Assert.Single(canvas.Overlays);
            Assert.Equal(0, list.VerticalOffset);
        }

        [Fact]
        public void PanInsideAnOverlay_IsOneToOne_UnderACanvasZoom()
        {
            // Overlays (popups) live in surface space, untouched by the canvas's own zoom: a finger moving 100 px must move
            // the popup's content 100 units, not 50.
            var (canvas, input) = Create();
            canvas.Scale = new Vector2(2, 2);
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(new UIElement { Height = 1000 });
            var popupList = new ScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.AddOverlay(popupList);
            canvas.Render();

            Pan(input, PointerKind.Touch, new Point(100, 180), new Point(100, 150), new Point(100, 50));

            Assert.Equal(100, popupList.VerticalOffset, 0.5);
        }

        [Fact]
        public void VerticalSwipeOnASlider_ScrollsTheList()
        {
            var (canvas, input) = Create();
            var (list, slider) = AddList(canvas);

            Pan(input, PointerKind.Touch, new Point(100, 40), new Point(100, 25), new Point(100, 5));

            Assert.Equal(20, list.VerticalOffset, 0.5);
            Assert.Empty(slider.Log);
        }

        [Fact]
        public void HorizontalSwipeOnASlider_MovesTheSlider_NotTheList()
        {
            var (canvas, input) = Create();
            var (list, slider) = AddList(canvas);

            Pan(input, PointerKind.Touch, new Point(50, 25), new Point(70, 25), new Point(120, 25));

            Assert.Equal(0, list.VerticalOffset);
            Assert.Equal("start 50,25", slider.Log[0]);
        }

        [Fact]
        public void SliderInListAtEdge_FallsBackToTheSlider()
        {
            var (canvas, input) = Create();
            var (list, slider) = AddList(canvas);

            // Finger moves down at the top edge: the list can't scroll that way, so it doesn't claim.
            Pan(input, PointerKind.Touch, new Point(100, 10), new Point(102, 30), new Point(104, 45));

            Assert.Equal(0, list.VerticalOffset);
            Assert.Equal("start 100,10", slider.Log[0]);
        }

        [Fact]
        public void InnerListAtItsEdge_PassesTheGestureToTheOuterList()
        {
            var (canvas, input) = Create();
            var inner = new ScrollViewer { Height = 100, Content = new UIElement { Height = 300 } };
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(inner);
            column.Children.Add(new UIElement { Height = 900 });
            var outer = new ScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.Add(outer);
            canvas.Render();
            inner.VerticalOffset = 200; // the inner list is at its bottom

            Pan(input, PointerKind.Touch, new Point(100, 80), new Point(100, 60), new Point(100, 30));

            Assert.Equal(200, inner.VerticalOffset);
            Assert.Equal(30, outer.VerticalOffset, 0.5);
        }

        [Fact]
        public void PanningModeNone_DoesNotPan()
        {
            var (canvas, input) = Create();
            var (list, _) = AddList(canvas);
            list.PanningMode = PanningMode.None;

            Pan(input, PointerKind.Touch, new Point(100, 150), new Point(100, 130), new Point(100, 100));

            Assert.Equal(0, list.VerticalOffset);
        }

        [Fact]
        public void AutoWithoutOverflow_ClaimsNothing()
        {
            var list = new ScrollViewer { Width = 200, Height = 200, Content = new UIElement { Height = 100 } };
            list.Arrange(new Rectangle(0, 0, 200, 200));

            Assert.Equal(DragAxes.None, list.GetDragAxes(new DragClaimContext(PointerKind.Touch, Point.Empty, new Vector2(0, -20), false)));
        }

        [Fact]
        public void LeftMouse_NeverPans_MiddleMouseDoes()
        {
            var (canvas, input) = Create();
            var (list, _) = AddList(canvas);

            Pan(input, PointerKind.MouseLeft, new Point(100, 150), new Point(100, 130), new Point(100, 100));
            Assert.Equal(0, list.VerticalOffset);

            Pan(input, PointerKind.MouseMiddle, new Point(100, 150), new Point(100, 130), new Point(100, 100));
            Assert.Equal(30, list.VerticalOffset, 0.5);
        }

        [Fact]
        public void Pan_IsOneToOne_AtDisplayScale2()
        {
            var (canvas, input) = Create(displayScale: 2f);
            var (list, _) = AddList(canvas);

            // Physical pixels: the 200-unit list spans 400 px. 150 px of finger travel = 75 units.
            Pan(input, PointerKind.Touch, new Point(200, 300), new Point(200, 250), new Point(200, 100));

            Assert.Equal(75, list.VerticalOffset, 0.5);
        }

        [Fact]
        public void Pan_IsOneToOne_UnderACanvasZoom()
        {
            var (canvas, input) = Create();
            canvas.Scale = new Vector2(2, 2);
            var (list, _) = AddList(canvas);

            Pan(input, PointerKind.Touch, new Point(200, 300), new Point(200, 250), new Point(200, 100));

            Assert.Equal(75, list.VerticalOffset, 0.5);
        }
    }
}
