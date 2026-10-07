// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design.Editor;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorSessionScopeTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="100,100,0,0">
              <Border x:Name="outside" Width="100" Height="20" HorizontalAlignment="Left"/>
              <ScrollViewer x:Name="viewer" Width="200" Height="100" HorizontalAlignment="Left">
                <StackPanel x:Name="scope">
                  <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
                  <Border x:Name="tall" Width="100" Height="400" HorizontalAlignment="Left"/>
                </StackPanel>
              </ScrollViewer>
            </StackPanel>
            """;

        private static EditorSession AttachScoped(EditorTestHost host) =>
            EditorSession.Attach(host.Design, host.Canvas, host.Named<StackPanel>("scope"));

        [Fact]
        public void TheRegion_IsTheScopeClippedByItsScrollViewer()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);
            Rectangle viewer = Rectangle.Round(AdornerGeometry.SurfaceBounds(host.Named<ScrollViewer>("viewer")));

            Rectangle region = session.Region();

            Assert.Equal(viewer.Top, region.Top);
            Assert.Equal(viewer.Bottom, region.Bottom);
            Assert.True(region.Width > 0);
            Assert.True(region.Left >= viewer.Left && region.Right <= viewer.Right);
        }

        [Fact]
        public void WithoutAScope_TheRegionIsTheWholeSurface()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = EditorSession.Attach(host.Design, host.Canvas);

            Assert.Null(session.Scope);
            Assert.Equal(new Rectangle(Point.Empty, host.Canvas.SurfaceSize), session.Region());
        }

        [Fact]
        public void HitTest_SeesOnlyTheRegion()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);
            var tall = host.Named<Border>("tall");

            Assert.Same(host.Named<Border>("a"), session.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
            Assert.Null(session.HitTest(host.At(host.Named<Border>("outside"), 5, 5)));

            // Below the viewer: inside the scope's bounds, but clipped away.
            Assert.Null(session.HitTest(host.At(tall, 5, 300)));
        }

        [Fact]
        public void HitTest_OnAScaledCanvas_StillMatchesThePointer()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            host.Configuration.Scaling.UserScale = 2f;
            host.Render();
            using EditorSession session = AttachScoped(host);

            Assert.Same(host.Named<Border>("a"), session.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
            Assert.Null(session.HitTest(host.At(host.Named<Border>("outside"), 5, 5)));
        }

        [Fact]
        public void TheRegion_IsEmpty_WhileAnAncestorIsHidden()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);

            host.Named<ScrollViewer>("viewer").IsVisible = false;
            host.Render();

            Assert.True(session.Region().IsEmpty);
            Assert.Null(session.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
        }

        [Fact]
        public void TheRegion_IsEmpty_WhileAnAncestorIsFullyTransparent()
        {
            // Core treats Opacity <= 0 as hidden for both drawing and hit-testing; a faded-out scope shows nothing to edit.
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);

            host.Named<ScrollViewer>("viewer").Opacity = 0;
            host.Render();

            Assert.True(session.Region().IsEmpty);
            Assert.Null(session.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
        }

        [Fact]
        public void Select_RefusesElementsOutsideTheScope_AndAcceptsOverlays()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);
            UIElement popup = new MarkupLoader(host.Configuration).Load("<Border Width=\"50\" Height=\"50\"/>", "popup.xml");
            host.Canvas.AddOverlay(popup);

            Assert.False(session.Select(host.Named<Border>("outside")));
            Assert.Null(session.Selection);
            Assert.True(session.Select(host.Named<Border>("a")));
            Assert.True(session.Select(popup));
        }

        [Fact]
        public void SelectByNode_RefusesAnElementOutsideTheScope()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);

            Assert.Throws<ArgumentException>(() => session.Select(host.Document, host.IdOf(host.Named<Border>("outside"))));
        }

        [Fact]
        public void ASecondSession_OnTheSameCanvas_Throws_UntilTheFirstIsDisposed()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            EditorSession first = EditorSession.Attach(host.Design, host.Canvas);

            Assert.Throws<InvalidOperationException>(() => EditorSession.Attach(host.Design, host.Canvas));
            Assert.Same(first, EditorSession.FindAttached(host.Canvas));
            Assert.False(host.Canvas.IsKeyboardNavigationEnabled);

            first.Dispose();
            Assert.Null(EditorSession.FindAttached(host.Canvas));
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);

            using EditorSession second = EditorSession.Attach(host.Design, host.Canvas);
            Assert.Same(second, EditorSession.FindAttached(host.Canvas));
        }

        [Fact]
        public void AScopeNotOnTheCanvas_Throws_AndLeavesNoFootprint()
        {
            using var host = new EditorTestHost(Page, attachSession: false);

            Assert.Throws<ArgumentException>(() => EditorSession.Attach(host.Design, host.Canvas, new Border()));

            Assert.Null(EditorSession.FindAttached(host.Canvas));
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
        }
    }
}
