// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.UI
{
    public class BringIntoViewTests
    {
        [Fact]
        public void BringIntoView_BelowTheViewport_AlignsTheBottomEdge()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100);

            items[4].BringIntoView();
            canvas.Render();

            Assert.Equal(100, viewer.VerticalOffset);
            AssertInside(viewer, items[4]);
        }

        [Fact]
        public void BringIntoView_AboveTheViewport_AlignsTheTopEdge()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100);
            viewer.VerticalOffset = 200;
            canvas.Render();

            items[1].BringIntoView();
            canvas.Render();

            Assert.Equal(40, viewer.VerticalOffset);
            AssertInside(viewer, items[1]);
        }

        [Fact]
        public void BringIntoView_AlreadyVisible_DoesNotScroll()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100);

            items[1].BringIntoView();

            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void BringIntoView_LargerThanTheViewport_AlignsTheLeadingEdge()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 30);

            items[2].BringIntoView();

            Assert.Equal(80, viewer.VerticalOffset);
        }

        [Fact]
        public void BringIntoView_Horizontally_AlignsTheRightEdge()
        {
            (Canvas canvas, _, _) = CreateCanvas();
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            Border[] items = [.. Enumerable.Range(0, 10).Select(_ => new Border { Width = 40, Height = 20 })];
            foreach (Border item in items)
                panel.Children.Add(item);
            var viewer = new ScrollViewer { Width = 100, Height = 50, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = panel };
            canvas.Add(viewer);
            canvas.Render();

            items[4].BringIntoView();

            Assert.Equal(100, viewer.HorizontalOffset);
        }

        [Fact]
        public void BringIntoView_NestedViewers_ScrollsBoth()
        {
            (Canvas canvas, _, _) = CreateCanvas();
            var inner = new StackPanel();
            Border[] items = [.. Enumerable.Range(0, 10).Select(_ => new Border { Height = 40 })];
            foreach (Border item in items)
                inner.Children.Add(item);
            var innerViewer = new ScrollViewer { Height = 100, Content = inner };
            var outerPanel = new StackPanel();
            outerPanel.Children.Add(new Border { Height = 150 });
            outerPanel.Children.Add(innerViewer);
            var outerViewer = new ScrollViewer { Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = outerPanel };
            canvas.Add(outerViewer);
            canvas.Render();

            items[4].BringIntoView();
            canvas.Render();

            Assert.Equal(100, innerViewer.VerticalOffset);
            Assert.Equal(150, outerViewer.VerticalOffset);
            AssertInside(outerViewer, items[4]);
        }

        [Fact]
        public void BringIntoView_ARealizedTreeRow_ScrollsTheTree()
        {
            (Canvas canvas, _) = Icy.Tests.Controls.TreeViewTestKit.CreateCanvas();
            TreeView tree = CreateTree([.. Enumerable.Range(0, 30).Select(i => Node($"n{i}"))]);
            tree.Width = 200;
            tree.Height = 100;
            tree.HorizontalAlignment = HorizontalAlignment.Left;
            tree.VerticalAlignment = VerticalAlignment.Top;
            canvas.Add(tree);
            canvas.Render();

            TreeViewItem row = Row(tree, 6);
            row.BringIntoView();
            canvas.Render();

            Assert.True(tree.ScrollViewer.VerticalOffset > 0);
            AssertInside(tree.ScrollViewer, Row(tree, 6));
        }

        [Fact]
        public void BringIntoView_UnderDisplayScale_SetsOffsetsInLayoutUnits()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100, displayScale: 2f);

            items[4].BringIntoView();

            Assert.Equal(100, viewer.VerticalOffset);
        }

        internal static (Canvas Canvas, ScrollViewer Viewer, Border[] Items) CreateList(int viewportHeight, float displayScale = 1f)
        {
            (Canvas canvas, _, _) = CreateCanvas(displayScale);
            var panel = new StackPanel();
            Border[] items = [.. Enumerable.Range(0, 10).Select(_ => new Border { Height = 40 })];
            foreach (Border item in items)
                panel.Children.Add(item);
            var viewer = new ScrollViewer { Width = 100, Height = viewportHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = panel };
            canvas.Add(viewer);
            canvas.Render();
            return (canvas, viewer, items);
        }

        internal static (Canvas Canvas, FakeInputSystem Input, FakeRenderContext Context) CreateCanvas(float displayScale = 1f)
        {
            var input = new FakeInputSystem();
            var context = new FakeRenderContext { ViewportSize = new Size(800, 600), DisplayScale = displayScale };
            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), context, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input, context);
        }

        internal static void AssertInside(ScrollViewer viewer, UIElement element)
        {
            Rectangle viewport = viewer.GetViewportScreenBounds();
            Rectangle bounds = element.GetScreenBounds();
            Assert.True(viewport.Contains(bounds), $"{bounds} is not inside the viewport {viewport}.");
        }
    }
}
