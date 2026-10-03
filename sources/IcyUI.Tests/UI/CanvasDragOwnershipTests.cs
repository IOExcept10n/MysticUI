using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.DragDrop;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasDragOwnershipTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) Create()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            return (canvas, input);
        }

        private static T Place<T>(T element, int x, int y, int width, int height)
            where T : UIElement
        {
            element.Margin = new Thickness(x, y, 0, 0);
            element.Width = width;
            element.Height = height;
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;
            return element;
        }

        private static DragInfo Drag(PointerKind kind, Point start, Point position, bool fromHold = false) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero, fromHold);

        [Fact]
        public void InnermostClaimantOfTheMainAxis_OwnsTheDrag_AndItsParentGetsNothing()
        {
            var (canvas, input) = Create();
            var child = new AxisElement(DragAxes.Horizontal) { Width = 100, Height = 50, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var parent = Place(new AxisBorder(DragAxes.Both) { Child = child }, 0, 0, 200, 200);
            canvas.Add(parent);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 12)));

            Assert.Equal(new[] { "start 10,10", "move 40,12" }, child.Log);
            Assert.Empty(parent.Log);
        }

        [Fact]
        public void CrossAxisDrag_GoesToTheAncestorClaimingThatAxis()
        {
            var (canvas, input) = Create();
            var child = new AxisElement(DragAxes.Horizontal) { Width = 100, Height = 50, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var parent = Place(new AxisBorder(DragAxes.Vertical) { Child = child }, 0, 0, 200, 200);
            canvas.Add(parent);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(12, 40)));

            Assert.Empty(child.Log);
            Assert.Equal(new[] { "start", "move" }, parent.Log);
        }

        [Fact]
        public void NoMainAxisClaimant_FallsBackToTheInnermostClaimant()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Horizontal), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseLeft, new Point(10, 10), new Point(14, 40)));

            Assert.Equal("start 10,10", element.Log[0]);
        }

        [Fact]
        public void ElementsThatClaimNothing_ReceiveNoDrag()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.None), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10)));
            input.Events.Gestures.RaiseDragCompleted(Drag(PointerKind.Touch, new Point(10, 10), new Point(60, 10)));

            Assert.Empty(element.Log);
        }

        [Fact]
        public void CompletedDrag_FlingsThenEnds_OnTheOwnerOnly()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Both), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(10, 10)));
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCompleted(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));

            Assert.Equal(new[] { "start 10,10", "move 30,10", "fling", "end" }, element.Log);
        }

        [Fact]
        public void CanceledDrag_EndsWithoutFling()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Both), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(10, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));

            Assert.Equal(new[] { "start 10,10", "end" }, element.Log);
        }

        [Fact]
        public void OwnerDetachedMidDrag_ReceivesNothingMore()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Both), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(10, 10)));
            canvas.Remove(element);
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCompleted(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));

            Assert.Equal(new[] { "start 10,10" }, element.Log);
        }

        [Fact]
        public void TouchDragDrop_StartsOnlyAfterAHold()
        {
            var (canvas, input) = Create();
            var source = Place(new PreviewSource(), 0, 0, 100, 100);
            canvas.Add(source);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10)));
            Assert.Empty(canvas.Overlays);

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10), fromHold: true));
            Assert.Single(canvas.Overlays);
        }

        [Fact]
        public void BuiltInControls_ClaimTheirAxes_AndNeverMiddleMouse()
        {
            var touch = new DragClaimContext(PointerKind.Touch, Point.Empty, new Vector2(10, 0), false);
            var middle = touch with { Kind = PointerKind.MouseMiddle };

            Assert.Equal(DragAxes.Horizontal, new Slider().GetDragAxes(touch));
            Assert.Equal(DragAxes.None, new Slider().GetDragAxes(middle));
            Assert.Equal(DragAxes.Both, new HsvSquare().GetDragAxes(touch));
            Assert.Equal(DragAxes.None, new HsvSquare().GetDragAxes(middle));
        }

        private sealed class PreviewSource : UIElement, IDragSource
        {
            public bool TryBeginDrag(Point screenPoint, out object? payload, out UIElement? preview)
            {
                payload = new object();
                preview = new UIElement();
                return true;
            }
        }
    }
}
