using System.Drawing;
using System.Numerics;
using Icy.UI;

namespace Icy.Tests.UI
{
    /// <summary>
    /// A leaf element that claims fixed drag axes and records every drag call it receives.
    /// </summary>
    internal sealed class AxisElement(DragAxes axes) : UIElement
    {
        public List<string> Log { get; } = [];

        protected internal override DragAxes GetDragAxes(in DragClaimContext context) => axes;

        protected internal override void OnDragStarted(Point screenPoint) => Log.Add($"start {screenPoint.X},{screenPoint.Y}");

        protected internal override void OnDragPerforming(Point screenPoint) => Log.Add($"move {screenPoint.X},{screenPoint.Y}");

        protected internal override void OnDragEnded(Point screenPoint) => Log.Add("end");

        protected internal override void OnDragFling(Vector2 screenVelocity) => Log.Add("fling");
    }

    /// <summary>
    /// A container counterpart of <see cref="AxisElement"/>.
    /// </summary>
    internal sealed class AxisBorder(DragAxes axes) : Border
    {
        public List<string> Log { get; } = [];

        protected internal override DragAxes GetDragAxes(in DragClaimContext context) => axes;

        protected internal override void OnDragStarted(Point screenPoint) => Log.Add("start");

        protected internal override void OnDragPerforming(Point screenPoint) => Log.Add("move");

        protected internal override void OnDragEnded(Point screenPoint) => Log.Add("end");
    }

    /// <summary>
    /// An <see cref="AxisElement"/> that tells a cancel from an end.
    /// </summary>
    internal sealed class CancelAwareElement(DragAxes axes) : UIElement
    {
        public List<string> Log { get; } = [];

        protected internal override DragAxes GetDragAxes(in DragClaimContext context) => axes;

        protected internal override void OnDragStarted(Point screenPoint) => Log.Add("start");

        protected internal override void OnDragEnded(Point screenPoint) => Log.Add("end");

        protected internal override void OnDragCanceled(Point screenPoint) => Log.Add("cancel");
    }
}
