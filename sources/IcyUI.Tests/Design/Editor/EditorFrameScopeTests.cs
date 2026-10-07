// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorFrameScopeTests
    {
        // A sidebar button left of a scrolling content area, like the samples shell.
        private const string Page =
            """
            <StackPanel x:Name="root" Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="side" Width="150" Height="300">Side</Button>
              <ScrollViewer x:Name="viewer" Width="400" Height="300">
                <StackPanel x:Name="content">
                  <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
                  <Border x:Name="tall" Width="100" Height="1000" HorizontalAlignment="Left"/>
                </StackPanel>
              </ScrollViewer>
            </StackPanel>
            """;

        private static EditorFrame AttachScoped(EditorTestHost host)
        {
            EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design, host.Named<ScrollViewer>("viewer"));
            Settle(host);
            return frame;
        }

        // The frame follows the region from its render: one frame to see it, one to arrange the layer, one more for the
        // toolbar's measured size.
        private static void Settle(EditorTestHost host)
        {
            for (int i = 0; i < 3; i++)
                host.Render();
        }

        private static Rectangle Surface(UIElement element) => Rectangle.Round(AdornerGeometry.SurfaceBounds(element));

        private static DragInfo Drag(PointerKind kind, Point start, Point position) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        [Fact]
        public void TheCaptureLayer_CoversExactlyTheRegion()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);

            Assert.Equal(frame.Session.Region(), Surface(frame.CaptureLayer));
            Assert.Equal(Surface(host.Named<ScrollViewer>("viewer")), frame.Session.Region());
        }

        [Fact]
        public void TheCaptureLayer_FollowsAViewportResize()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);

            host.RenderContext.ViewportSize = new Size(300, 200);
            Settle(host);

            Assert.Equal(new Rectangle(150, 0, 150, 200), frame.Session.Region());
            Assert.Equal(frame.Session.Region(), Surface(frame.CaptureLayer));
        }

        [Fact]
        public void TheToolbar_SitsInTheRegionsCorner()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            Rectangle region = frame.Session.Region();

            Rectangle topLeft = Surface(frame.Toolbar);
            Assert.Equal(region.Left + 8, topLeft.Left);
            Assert.Equal(region.Top + 8, topLeft.Top);

            frame.ToolbarPlacement = EditorToolbarPlacement.BottomRight;
            Settle(host);
            Rectangle bottomRight = Surface(frame.Toolbar);
            Assert.Equal(region.Right - 8, bottomRight.Right);
            Assert.Equal(region.Bottom - 8, bottomRight.Bottom);
        }

        [Fact]
        public void TheToolbar_StaysOnTheSurface_WhenTheRegionIsSmallerThanIt()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);

            host.RenderContext.ViewportSize = new Size(170, 200);
            frame.ToolbarPlacement = EditorToolbarPlacement.TopRight;
            Settle(host);

            Rectangle toolbar = Surface(frame.Toolbar);
            Assert.True(toolbar.Left >= 0);
            Assert.True(toolbar.Right <= 170);
        }

        [Fact]
        public void ARightPlacedToolbar_KeepsItsCornerAndWidth_WhenItGrows()
        {
            // The region reaches the surface's right edge, as the shell's content area does: a toolbar placed from its
            // previous, overflow-clamped width would be cut off and creep left frame after frame.
            using var host = new EditorTestHost(Page, attachSession: false);
            host.RenderContext.ViewportSize = new Size(550, 400);
            using EditorFrame frame = AttachScoped(host);
            frame.ToolbarPlacement = EditorToolbarPlacement.TopRight;
            Settle(host);
            Rectangle region = frame.Session.Region();
            Assert.Equal(550, region.Right);

            ((StackPanel)frame.Toolbar.Child!).Children.Add(new Border { Width = 200, Height = 10 });
            Settle(host);
            Rectangle grown = Surface(frame.Toolbar);
            Assert.Equal(region.Right - 8, grown.Right);

            for (int i = 0; i < 5; i++)
                host.Render();
            Assert.Equal(grown, Surface(frame.Toolbar));

            frame.ToolbarPlacement = EditorToolbarPlacement.TopLeft;
            Settle(host);
            Assert.Equal(Surface(frame.Toolbar).Width, grown.Width);
        }

        [Fact]
        public void InEditMode_AClickOutsideTheRegion_ReachesThePage()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var side = host.Named<Button>("side");
            bool clicked = false;
            side.Click += (_, _) => clicked = true;

            host.Input.Events.Touch.RaiseTap(new TouchInfo(host.At(side, 20, 20), 1));

            Assert.True(clicked);
            Assert.Null(frame.Session.Selection);
        }

        [Fact]
        public void InEditMode_AClickInsideTheRegion_Selects()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var a = host.Named<Border>("a");

            host.Input.Events.Touch.RaiseTap(new TouchInfo(host.At(a, 5, 5), 1));

            Assert.Same(a, frame.Session.Selection!.Instance);
        }

        [Fact]
        public void InEditMode_TheWheel_ScrollsThePage()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Named<Border>("a"), 5, 5));
            host.Render();

            host.Input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, host.Named<ScrollViewer>("viewer").VerticalOffset);
        }

        [Fact]
        public void InEditMode_AMiddleDrag_PansThePage()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            Point start = host.At(host.Named<Border>("tall"), 5, 100);
            Point to = start with { Y = start.Y - 40 };
            string before = host.Document.Text;

            host.Input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, start, start with { Y = start.Y - 20 }));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.MouseMiddle, start, to));
            host.Input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.MouseMiddle, start, to));

            Assert.True(host.Named<ScrollViewer>("viewer").VerticalOffset > 0);
            Assert.Equal(before, host.Document.Text);
        }

        [Fact]
        public void AHiddenScope_StopsCapturing_AndComesBack()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var viewer = host.Named<ScrollViewer>("viewer");

            viewer.IsVisible = false;
            Settle(host);
            Assert.False(frame.Toolbar.IsVisible);
            Assert.NotSame(frame.CaptureLayer, host.Canvas.HitTest(host.At(host.Named<Button>("side"), 200, 20)));

            // Toggling the mode while hidden must not turn the capture back on.
            frame.Session.Mode = EditorMode.Interact;
            frame.Session.Mode = EditorMode.Edit;
            Assert.False(frame.CaptureLayer.IsHitTestVisible);

            viewer.IsVisible = true;
            Settle(host);
            Assert.True(frame.Toolbar.IsVisible);
            Assert.Same(frame.CaptureLayer, host.Canvas.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
        }

        [Fact]
        public void TheSelectionOutline_IsDrawnWhereTheElementIs()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var a = host.Named<Border>("a");
            frame.SelectionColor = Color.Magenta;
            frame.Session.Select(a);
            Settle(host);

            host.RenderContext.DrawCalls.Clear();
            host.Render();

            Rectangle top = host.RenderContext.DrawCalls
                .Where(c => c.Options.Color == Color.Magenta)
                .Select(c => c.TransformAtDrawTime.Apply(c.Options.Destination))
                .First();
            Assert.Equal(Surface(a).Location, top.Location);
        }
    }
}
