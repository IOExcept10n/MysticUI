using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.DragDrop;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Input
{
    /// <summary>
    /// Covers the cross-container drag-and-drop framework (<see cref="IDragSource"/>/<see cref="IDropTarget"/>/
    /// <see cref="DragDropSession"/>) via plain <see cref="UIElement"/> test doubles - no real control needs this
    /// yet (it's a Phase-9-M5-roadmap foundational phase, built ahead of any concrete consumer).
    /// </summary>
    public class DragDropTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            return (canvas, input);
        }

        private static FakeDragSource AddSource(Canvas canvas, object payload, UIElement? preview = null)
        {
            var source = new FakeDragSource(payload, preview)
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(source);
            return source;
        }

        private static FakeDropTarget AddTarget(Canvas canvas, bool accepts, Rectangle bounds)
        {
            var target = new FakeDropTarget(accepts)
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Margin = new Thickness(bounds.X, bounds.Y, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(target);
            return target;
        }

        [Fact]
        public void DragOntoAcceptingTarget_ThenEnded_CallsOnDropWithThePayload()
        {
            var (canvas, input) = CreateCanvas();
            var payload = new object();
            AddSource(canvas, payload);
            var target = AddTarget(canvas, accepts: true, new Rectangle(200, 0, 100, 100));
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(250, 50));
            input.Events.Drag.RaiseDragEnded(new Point(250, 50));

            Assert.Same(payload, target.DroppedPayload);
        }

        [Fact]
        public void DragEndedOverNonAcceptingElement_DoesNotCallOnDrop()
        {
            var (canvas, input) = CreateCanvas();
            AddSource(canvas, new object());
            var target = AddTarget(canvas, accepts: false, new Rectangle(200, 0, 100, 100));
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(250, 50));
            input.Events.Drag.RaiseDragEnded(new Point(250, 50));

            Assert.False(target.DroppedOn);
        }

        [Fact]
        public void DragEndedOverEmptySpace_DoesNotThrowAndCallsNoDrop()
        {
            var (canvas, input) = CreateCanvas();
            AddSource(canvas, new object());
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(900, 900));
            input.Events.Drag.RaiseDragEnded(new Point(900, 900));
        }

        [Fact]
        public void MovingBetweenTwoTargets_FiresLeaveOnOldAndEnterOnNew()
        {
            var (canvas, input) = CreateCanvas();
            AddSource(canvas, new object());
            var first = AddTarget(canvas, accepts: true, new Rectangle(200, 0, 50, 50));
            var second = AddTarget(canvas, accepts: true, new Rectangle(400, 0, 50, 50));
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(210, 10));

            Assert.Equal(1, first.EnterCount);
            Assert.Equal(0, first.LeaveCount);

            input.Events.Drag.RaiseDragPerforming(new Point(410, 10));

            Assert.Equal(1, first.LeaveCount);
            Assert.Equal(1, second.EnterCount);

            input.Events.Drag.RaiseDragEnded(new Point(410, 10));

            Assert.Same(second, GetLastDroppedTarget(first, second));
        }

        [Fact]
        public void StayingOverTheSameTarget_FiresOnDragOverNotRepeatedEnter()
        {
            var (canvas, input) = CreateCanvas();
            AddSource(canvas, new object());
            var target = AddTarget(canvas, accepts: true, new Rectangle(200, 0, 100, 100));
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(210, 10));
            input.Events.Drag.RaiseDragPerforming(new Point(220, 20));
            input.Events.Drag.RaiseDragPerforming(new Point(230, 30));

            Assert.Equal(1, target.EnterCount);
            Assert.Equal(2, target.OverCount);
        }

        [Fact]
        public void Preview_AddedToOverlaysOnStart_RemovedOnEnd()
        {
            var (canvas, input) = CreateCanvas();
            var preview = new UIElement();
            AddSource(canvas, new object(), preview);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            Assert.Contains(preview, canvas.Overlays);

            input.Events.Drag.RaiseDragEnded(new Point(50, 50));
            Assert.DoesNotContain(preview, canvas.Overlays);
        }

        [Fact]
        public void Preview_FollowsTheCursorDuringTheDrag()
        {
            var (canvas, input) = CreateCanvas();
            var preview = new UIElement();
            AddSource(canvas, new object(), preview);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(300, 120));

            Assert.Equal(300, preview.Margin.Left);
            Assert.Equal(120, preview.Margin.Top);
        }

        [Fact]
        public void Preview_FollowsTheCursorInSurfaceSpace_AtDisplayScale2()
        {
            // Overlays live in surface space (physical ÷ EffectiveScale), so the physical cursor point must be converted
            // before it becomes the preview's Margin - otherwise the preview drifts to twice the cursor position.
            var input = new FakeInputSystem();
            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext { DisplayScale = 2f }, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            var preview = new UIElement();
            AddSource(canvas, new object(), preview);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            Assert.Equal(25, preview.Margin.Left);
            Assert.Equal(25, preview.Margin.Top);

            input.Events.Drag.RaiseDragPerforming(new Point(300, 120));
            Assert.Equal(150, preview.Margin.Left);
            Assert.Equal(60, preview.Margin.Top);
        }

        [Fact]
        public void DragWithAPreview_StillFindsTheDropTargetUnderneathIt()
        {
            // Regression coverage: the preview is an overlay positioned with its top-left exactly on the cursor, and
            // Canvas.HitTest scans Overlays before rootElements, so once the preview has been arranged at the cursor
            // it won every HitTest of the drag point. An overlay has no Parent, so UpdateDragDropTarget's
            // SelfAndAncestors walk terminated on it immediately and no drop target was ever found - every
            // preview-carrying drag silently stopped firing OnDragEnter/OnDragOver/OnDrop. Hence
            // UIElement.IsHitTestVisible, which Canvas.OnDragStarted clears on the preview.
            var (canvas, input) = CreateCanvas();
            var payload = new object();
            var preview = new UIElement { Width = 60, Height = 60 };
            AddSource(canvas, payload, preview);
            var target = AddTarget(canvas, accepts: true, new Rectangle(200, 0, 100, 100));
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(250, 50));

            // The frame an app would render between two drag updates - this is what actually arranges the overlay at
            // the cursor, so the next hit test genuinely has the preview sitting on top of the target.
            canvas.Render();
            Assert.Equal(new Rectangle(250, 50, 60, 60), preview.ActualBounds);

            input.Events.Drag.RaiseDragPerforming(new Point(250, 50));

            Assert.Equal(1, target.EnterCount);
            Assert.Equal(0, target.LeaveCount);

            input.Events.Drag.RaiseDragEnded(new Point(250, 50));

            Assert.Same(payload, target.DroppedPayload);
        }

        [Fact]
        public void SourceDecliningTheDrag_StartsNoSession()
        {
            var (canvas, input) = CreateCanvas();
            var source = new FakeDragSource(payload: null, preview: null)
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(source);
            var target = AddTarget(canvas, accepts: true, new Rectangle(200, 0, 100, 100));
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(250, 50));
            input.Events.Drag.RaiseDragEnded(new Point(250, 50));

            Assert.False(target.DroppedOn);
        }

        private static UIElement? GetLastDroppedTarget(FakeDropTarget first, FakeDropTarget second) =>
            first.DroppedOn ? first : second.DroppedOn ? second : null;

        private sealed class FakeDragSource(object? payload, UIElement? preview) : UIElement, IDragSource
        {
            public bool TryBeginDrag(Point screenPoint, out object? outPayload, out UIElement? outPreview)
            {
                outPayload = payload;
                outPreview = preview;
                return payload != null;
            }
        }

        private sealed class FakeDropTarget(bool accepts) : UIElement, IDropTarget
        {
            public bool DroppedOn { get; private set; }

            public object? DroppedPayload { get; private set; }

            public int EnterCount { get; private set; }

            public int LeaveCount { get; private set; }

            public int OverCount { get; private set; }

            public bool CanDrop(DragDropSession session) => accepts;

            public void OnDragEnter(DragDropSession session) => EnterCount++;

            public void OnDragLeave(DragDropSession session) => LeaveCount++;

            public void OnDragOver(DragDropSession session) => OverCount++;

            public void OnDrop(DragDropSession session)
            {
                DroppedOn = true;
                DroppedPayload = session.Payload;
            }
        }
    }
}
