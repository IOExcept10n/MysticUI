using System.Drawing;
using Icy.Input.Devices;
using Icy.Input.Events;
using Xunit;

namespace Icy.Tests.Input
{
    public class ScrollEventsTests
    {
        [Fact]
        public void MiddleButtonDrag_NoLongerAutoscrolls()
        {
            // Middle-mouse drags pan scroll areas 1:1 through gestures now (ScrollViewer.PanningMode); the old autoscroll is gone.
            var input = new FakeInputSystem();
            var scroll = new ScrollEvents(input);
            scroll.Initialize();
            int raised = 0;
            scroll.Scroll += (_, _) => raised++;

            input.Mouse.MouseInfo = new MouseInfo(new Point(100, 100));
            input.Mouse.RaiseButtonPressed(MouseButtons.MiddleButton);
            for (int i = 1; i <= 10; i++)
            {
                input.Mouse.MouseInfo = new MouseInfo(new Point(100, 100 + (i * 20)));
                scroll.Update(TimeSpan.FromMilliseconds(50));
            }

            Assert.Equal(0, raised);
        }
    }
}
