using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Markup;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ScrollViewerInertiaTests
    {
        private sealed class TapRecorder : UIElement
        {
            public int Taps { get; private set; }

            protected internal override void OnTap() => Taps++;
        }

        private sealed class TestScrollViewer : ScrollViewer
        {
            public bool RaiseScroll(ScrollInfo info) => OnScroll(info);
        }

        private static (Canvas Canvas, FakeInputSystem Input, TestScrollViewer List, TapRecorder Item) Create(int contentHeight = 5000)
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var item = new TapRecorder { Height = 100 };
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(item);
            column.Children.Add(new UIElement { Height = contentHeight - 100 });
            var list = new TestScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.Add(list);
            canvas.Render();
            return (canvas, input, list, item);
        }

        // An upward touch flick released at velocity (0, vy) px/s: content scrolls down (offset increases).
        private static void Flick(FakeInputSystem input, float vy)
        {
            var start = new Point(100, 150);
            var end = new Point(100, 100);
            input.Events.Gestures.RaiseDragStarted(new DragInfo(PointerKind.Touch, start, new Point(100, 130), new Vector2(0, -20), Vector2.Zero));
            input.Events.Gestures.RaiseDragMoved(new DragInfo(PointerKind.Touch, start, end, new Vector2(0, -30), Vector2.Zero));
            input.Events.Gestures.RaiseDragCompleted(new DragInfo(PointerKind.Touch, start, end, Vector2.Zero, new Vector2(0, vy)));
        }

        private static void Advance(TimeSpan time) => Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(time);

        [Fact]
        public void Fling_CoastsWithConstantDeceleration()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            float afterPan = list.VerticalOffset; // 30

            // v = 1000, d = 1500: T = 0.667 s, distance = 333.3. Halfway through T the eased position is 75 %.
            Advance(TimeSpan.FromSeconds(1d / 3d));
            Assert.Equal(afterPan + 250, list.VerticalOffset, 1);

            Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(afterPan + 333.3f, list.VerticalOffset, 1);
        }

        [Fact]
        public void ExactlyDiagonalFling_KeepsCoastingOnBothAxes()
        {
            // Regression: the "anything moved" check summed both offsets, so equal and opposite steps (a perfectly diagonal
            // up-right flick) looked like no movement and stopped the coast after one frame.
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var list = new ScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, PanningMode = PanningMode.Both, Content = new UIElement { Width = 3000, Height = 3000 } };
            canvas.Add(list);
            canvas.Render();
            list.HorizontalOffset = 1000;
            list.VerticalOffset = 1000;
            var start = new Point(100, 100);

            input.Events.Gestures.RaiseDragStarted(new DragInfo(PointerKind.Touch, start, new Point(80, 120), new Vector2(-20, 20), Vector2.Zero));
            input.Events.Gestures.RaiseDragMoved(new DragInfo(PointerKind.Touch, start, new Point(60, 140), new Vector2(-20, 20), Vector2.Zero));
            input.Events.Gestures.RaiseDragCompleted(new DragInfo(PointerKind.Touch, start, new Point(60, 140), Vector2.Zero, new Vector2(-1000, 1000)));
            Advance(TimeSpan.FromSeconds(2));

            // Pan: (1020, 980). Fling: |v| = 1414.2, T = 0.943 s, distance = v * T / 2 = (471.4, -471.4).
            Assert.Equal(1491.4f, list.HorizontalOffset, 0);
            Assert.Equal(508.6f, list.VerticalOffset, 0);
        }

        [Fact]
        public void InertiaStep_WithEqualAndOppositeAxisSteps_CountsAsMovement()
        {
            // The deterministic core of the diagonal-fling bug: exactly cancelling steps left the offset SUM unchanged, which
            // read as "nothing moved" and stopped the coast.
            var list = new ScrollViewer { Width = 200, Height = 200, PanningMode = PanningMode.Both, Content = new UIElement { Width = 3000, Height = 3000 } };
            list.Arrange(new Rectangle(0, 0, 200, 200));
            list.HorizontalOffset = 1000;
            list.VerticalOffset = 1000;
            var applyStep = typeof(ScrollViewer).GetMethod("ApplyInertiaStep", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            bool moved = (bool)applyStep.Invoke(list, [new Vector2(100, -100)])!;

            Assert.True(moved);
            Assert.Equal(1100, list.HorizontalOffset);
            Assert.Equal(900, list.VerticalOffset);
        }

        [Fact]
        public void Fling_StopsHardAtTheEdge()
        {
            var (_, input, list, _) = Create(contentHeight: 300); // max offset 100
            Flick(input, -3000);

            Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(100, list.VerticalOffset);
        }

        [Theory]
        [InlineData(0f, -1000f)]  // inertia disabled
        [InlineData(1500f, -40f)] // below the 50 units/s threshold
        public void NoFling_WhenDisabledOrTooSlow(float deceleration, float vy)
        {
            var (_, input, list, _) = Create();
            list.PanningDeceleration = deceleration;
            Flick(input, vy);
            float afterPan = list.VerticalOffset;

            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(afterPan, list.VerticalOffset);
        }

        [Fact]
        public void PressDuringFling_StopsIt_AndSwallowsOnlyThatTap()
        {
            var (canvas, input, list, item) = Create();
            list.VerticalOffset = 0;
            Flick(input, -1000);
            Advance(TimeSpan.FromMilliseconds(50));
            list.VerticalOffset = 0; // bring the item back under the finger while the fling still runs

            input.Events.Touch.RaiseTouchDown(new Point(100, 50));
            float stoppedAt = list.VerticalOffset;
            input.Events.Touch.RaiseTouchUp(new Point(100, 50));
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(100, 50), 1));
            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(stoppedAt, list.VerticalOffset);
            Assert.Equal(0, item.Taps);

            canvas.Render(); // end of frame: children are hit-testable again
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(100, 50), 1));
            Assert.Equal(1, item.Taps);
        }

        [Fact]
        public void WheelScroll_StopsTheFling()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            list.RaiseScroll(new ScrollInfo(-10, Orientation.Vertical));
            float afterWheel = list.VerticalOffset;

            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(afterWheel, list.VerticalOffset);
        }

        [Fact]
        public void OffsetCorrectionDuringFling_IsKept()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            float afterPan = list.VerticalOffset;
            Advance(TimeSpan.FromMilliseconds(100));

            list.VerticalOffset += 50; // what a virtualized list's height correction does mid-flight
            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(afterPan + 333.3f + 50, list.VerticalOffset, 1);
        }

        [Fact]
        public void StopInertia_And_PanningModeChange_StopTheFling()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            list.StopInertia();
            float stopped = list.VerticalOffset;
            Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(stopped, list.VerticalOffset);

            Flick(input, -1000);
            list.PanningMode = PanningMode.Vertical;
            stopped = list.VerticalOffset;
            Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(stopped, list.VerticalOffset);
        }

        [Theory]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void PanningDeceleration_RejectsInvalidValues(float value) =>
            Assert.ThrowsAny<ArgumentException>(() => new ScrollViewer().PanningDeceleration = value);

        [Fact]
        public void FlingAcrossAVirtualizedList_RealizesTheRowsItPassesOver()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var template = (Icy.UI.Styles.DataTemplate)new Icy.Markup.MarkupLoader(configuration).LoadObject("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var items = new ItemsControl { ItemsSource = Enumerable.Range(0, 1000).Cast<object>().ToList(), ItemTemplate = template };
            var list = new ScrollViewer { Width = 300, Height = 400, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = items };
            canvas.Add(list);
            canvas.Render();

            Flick(input, -4000);
            Advance(TimeSpan.FromSeconds(3));
            canvas.Render();

            Assert.True(list.VerticalOffset > 1000, $"offset {list.VerticalOffset}");
            int firstVisible = (int)(list.VerticalOffset / 40);
            var realized = ((Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(items)!).Keys;
            Assert.Contains(firstVisible, realized);
            Assert.DoesNotContain(0, realized);
        }
    }
}
