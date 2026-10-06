// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class MarginPlacementTests
    {
        [Theory]
        [InlineData(HorizontalAlignment.Left, false, "15,0,0,0")]
        [InlineData(HorizontalAlignment.Right, false, "10,0,-5,0")]
        [InlineData(HorizontalAlignment.Center, false, "15,0,0,0")]
        [InlineData(HorizontalAlignment.Stretch, false, "15,0,0,0")]
        [InlineData(HorizontalAlignment.Stretch, true, "15,0,-5,0")]
        public void Move_FollowsIcyArrangeForEveryAlignment(HorizontalAlignment alignment, bool stretched, string expected)
        {
            var element = new UIElement { HorizontalAlignment = alignment, Width = stretched ? float.NaN : 50 };

            Thickness moved = LayoutMath.Move(element, new Thickness(10, 0, 0, 0), 5, 0);

            Assert.Equal(expected, Expand(moved));
        }

        [Fact]
        public void Move_Vertical_UsesTopAndBottom()
        {
            var element = new UIElement { VerticalAlignment = VerticalAlignment.Bottom, Height = 20 };

            Thickness moved = LayoutMath.Move(element, new Thickness(0, 0, 0, 8), 0, 3);

            Assert.Equal(new Thickness(0, 0, 0, 5), moved);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, "0")]
        [InlineData(4, 4, 4, 4, "4")]
        [InlineData(4, 8, 4, 8, "4,8")]
        [InlineData(1, 2, 3, 4, "1,2,3,4")]
        public void Format_WritesTheShortestThickness(int left, int top, int right, int bottom, string expected)
        {
            Assert.Equal(expected, MarkupValues.Format(new Thickness(left, top, right, bottom)));
        }

        [Fact]
        public void ResizingTheEndEdge_WritesTheSize()
        {
            (PlacementContext context, _) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(12, 0));

            Assert.Contains(new AttributeEdit("Width", "62"), edits);
            Assert.DoesNotContain(edits, x => x.Name == "Margin");
        }

        [Fact]
        public void ResizingTheStartEdge_KeepsTheOppositeEdgeInPlace()
        {
            (PlacementContext context, _) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 0, 0, 0) });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Left).Update(new Vector2(-5, 0));

            Assert.Contains(new AttributeEdit("Width", "55"), edits);
            Assert.Contains(new AttributeEdit("Margin", "5,0,0,0"), edits);
        }

        [Fact]
        public void ResizingAStretchedAxis_MovesTheMarginNotTheSize()
        {
            (PlacementContext context, _) = Context(new UIElement { Height = 20, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4) });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-10, 0));

            Assert.DoesNotContain(edits, x => x.Name == "Width");
            Assert.Contains(new AttributeEdit("Margin", "4,4,14,4"), edits);
        }

        [Fact]
        public void TheSizeNeverGoesBelowOne()
        {
            (PlacementContext context, _) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-500, 0));

            Assert.Contains(new AttributeEdit("Width", "1"), edits);
        }

        [Fact]
        public void ADropInTheSameContainer_MovesByTheMargin()
        {
            (PlacementContext context, UIElement element) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 10, 0, 0) });

            // The element's top-left is at (10, 10); the drop asks for (30, 15).
            PlacementTarget? target = new MarginPlacement().GetDropTarget(context, new Vector2(30, 15));

            Assert.NotNull(target);
            Assert.Contains(new AttributeEdit("Margin", "30,15,0,0"), target.Edits);
            Assert.False(target.IndicatorIsLine);
            Assert.Equal(new RectangleF(30, 15, 50, 20), target.Indicator);
            Assert.Equal(element, context.Element);
        }

        [Fact]
        public void ASingleSlotContainer_RefusesADropWhenFilled()
        {
            var border = new Border { Width = 200, Height = 100, Child = new UIElement() };
            var dragged = new UIElement { Width = 10, Height = 10 };
            border.Arrange(new Rectangle(0, 0, 200, 100));
            var context = new PlacementContext(border, dragged, [new PlacementChild(0, border.Child!)], 1, new Rectangle(0, 0, 10, 10), isCurrentContainer: false);

            Assert.Null(new MarginPlacement().GetDropTarget(context, new Vector2(5, 5)));
        }

        [Fact]
        public void TheRegistry_PrefersTheMostDerivedRegistration()
        {
            var registry = new PlacementRegistry();
            var forPanel = new MarginPlacement();
            var forStack = new MarginPlacement();
            registry.Register<Panel>(forPanel);
            registry.Register<StackPanel>(forStack);

            Assert.Same(forStack, registry.Resolve(new StackPanel()));
            Assert.Same(forPanel, registry.Resolve(new Grid()));
        }

        [Fact]
        public void TheRegistry_FallsBackToTheMarginStrategy()
        {
            var registry = new PlacementRegistry();

            Assert.IsType<MarginPlacement>(registry.Resolve(new Border()));
        }

        private static (PlacementContext Context, UIElement Element) Context(UIElement element)
        {
            var panel = new Panel { Width = 300, Height = 200 };
            panel.Children.Add(element);
            panel.Arrange(new Rectangle(0, 0, 300, 200));
            var context = new PlacementContext(panel, element, [], 0, PlacementContext.ToLocal(panel, element.ActualBounds), isCurrentContainer: true);
            return (context, element);
        }

        private static string Expand(Thickness t) => $"{t.Left},{t.Top},{t.Right},{t.Bottom}";
    }
}
