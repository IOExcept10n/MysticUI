using System.Drawing;
using Icy.Data;
using Icy.Input.Devices;
using Icy.Input.Events;
using Xunit;

namespace Icy.Tests.Input
{
    /// <summary>
    /// Covers <see cref="DragEvens"/>, the default <see cref="IDragEvents"/> implementation - specifically that
    /// <see cref="IDragEvents.DragPerforming"/> carries an absolute screen position, matching the documented
    /// contract on <see cref="Icy.UI.UIElement.OnDragPerforming(Point)"/> ("the drag's current position, in
    /// screen/window space") and the same contract <see cref="IDragEvents.DragStarted"/>/
    /// <see cref="IDragEvents.DragEnded"/> already follow.
    /// </summary>
    public class DragEvensTests
    {
        [Fact]
        public void MouseDrag_DragPerforming_CarriesAbsolutePositionNotDelta()
        {
            // Regression: this used to pass the incremental delta since the last Update() call instead of the
            // absolute cursor position - consumers like Slider.UpdateValueFromPoint (via PointToLocal, which
            // expects a real screen coordinate) treated that delta as an absolute position, producing a
            // near-random result every frame instead of tracking the cursor.
            var input = new FakeInputSystem();
            var dragEvents = new DragEvens(input);
            dragEvents.Initialize();

            Point? performing = null;
            dragEvents.DragPerforming += (_, e) => performing = e.Data;

            input.Mouse.MouseInfo = new MouseInfo(new Point(10, 10));
            dragEvents.OnMouseMove(new Point(10, 10));

            input.Mouse.MouseInfo = new MouseInfo(new Point(50, 80));
            dragEvents.Update(TimeSpan.Zero);

            Assert.Equal(new Point(50, 80), performing);
        }

        [Fact]
        public void MouseDrag_MultipleUpdates_EachCarriesCurrentAbsolutePosition()
        {
            var input = new FakeInputSystem();
            var dragEvents = new DragEvens(input);
            dragEvents.Initialize();

            Point? performing = null;
            dragEvents.DragPerforming += (_, e) => performing = e.Data;

            input.Mouse.MouseInfo = new MouseInfo(new Point(0, 0));
            dragEvents.OnMouseMove(new Point(0, 0));

            input.Mouse.MouseInfo = new MouseInfo(new Point(20, 0));
            dragEvents.Update(TimeSpan.Zero);
            Assert.Equal(new Point(20, 0), performing);

            input.Mouse.MouseInfo = new MouseInfo(new Point(200, 0));
            dragEvents.Update(TimeSpan.Zero);
            Assert.Equal(new Point(200, 0), performing);
        }
    }
}
