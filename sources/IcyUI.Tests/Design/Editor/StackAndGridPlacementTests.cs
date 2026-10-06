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
    public class StackAndGridPlacementTests
    {
        [Theory]
        [InlineData(5, 0)]
        [InlineData(25, 1)]
        [InlineData(45, 2)]
        [InlineData(55, 3)]
        [InlineData(500, 3)]
        public void AVerticalStack_DropsAtTheNearestGap(int y, int expected)
        {
            (StackPanel stack, UIElement[] children) = Stack(Orientation.Vertical, 3, 20);
            var dragged = new UIElement { Width = 10, Height = 20 };
            PlacementContext context = Context(stack, dragged, children, isCurrent: false);

            PlacementTarget? target = new StackPanelPlacement().GetDropTarget(context, new Vector2(0, y));

            Assert.NotNull(target);
            Assert.Equal(expected, target.Index);
            Assert.True(target.IndicatorIsLine);
            Assert.Equal(expected * 20, target.Indicator.Y);
        }

        [Fact]
        public void AHorizontalStack_DropsAlongX()
        {
            (StackPanel stack, UIElement[] children) = Stack(Orientation.Horizontal, 2, 30);
            PlacementContext context = Context(stack, new UIElement { Width = 10, Height = 10 }, children, isCurrent: false);

            PlacementTarget? target = new StackPanelPlacement().GetDropTarget(context, new Vector2(40, 0));

            Assert.Equal(1, target!.Index);
            Assert.Equal(30, target.Indicator.X);
        }

        [Fact]
        public void AStack_ResizesItsStackingAxisWithoutTouchingTheMargin()
        {
            (StackPanel stack, UIElement[] children) = Stack(Orientation.Vertical, 2, 20);
            PlacementContext context = Context(stack, children[1], [children[0]], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new StackPanelPlacement().BeginResize(context, ResizeHandle.Top).Update(new Vector2(0, -6));

            Assert.Equal([new AttributeEdit("Height", "26")], edits);
        }

        [Fact]
        public void AGrid_DropsIntoTheCellUnderThePoint()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement { Width = 10, Height = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            PlacementTarget? target = new GridPlacement().GetDropTarget(context, new Vector2(150, 60));

            Assert.Contains(new AttributeEdit("Grid.Column", "1"), target!.Edits);
            Assert.Contains(new AttributeEdit("Grid.Row", "1"), target.Edits);
            Assert.Equal(new RectangleF(100, 50, 100, 50), target.Indicator);
        }

        [Fact]
        public void AGrid_RemovesRowAndColumnWhenTheyBecomeZero()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement { Width = 10, Height = 10 });
            Grid.SetRow(element, 2);
            Grid.SetColumn(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            PlacementTarget? target = new GridPlacement().GetDropTarget(context, new Vector2(5, 5));

            Assert.Contains(new AttributeEdit("Grid.Column", null), target!.Edits);
            Assert.Contains(new AttributeEdit("Grid.Row", null), target.Edits);
        }

        [Fact]
        public void AGridDrop_KeepsTheSpanButFitsItIn()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            Grid.SetColumnSpan(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            PlacementTarget? target = new GridPlacement().GetDropTarget(context, new Vector2(250, 5));

            // Column 2 can't hold a span of 2, so the element starts at column 1.
            Assert.Contains(new AttributeEdit("Grid.Column", "1"), target!.Edits);
            Assert.Equal(new RectangleF(100, 0, 200, 50), target.Indicator);
        }

        [Theory]
        [InlineData(-30, 1, "0,0,30,0")]
        [InlineData(20, 2, "0,0,80,0")]
        [InlineData(126, 3, "0,0,74,0")]
        public void AStretchedEdge_FollowsThePointer_SpanningTheTrackItIsIn(int dx, int expectedSpan, string expectedMargin)
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(dx, 0));

            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", expectedSpan == 1 ? null : expectedSpan.ToString(System.Globalization.CultureInfo.InvariantCulture)), edits);
            Assert.Contains(new AttributeEdit("Margin", expectedMargin), edits);
        }

        [Theory]
        [InlineData(-6, 1)]
        [InlineData(6, 1)]
        [InlineData(105, 2)]
        public void AStretchedEdge_NearATrackLine_SnapsToIt(int dx, int expectedSpan)
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(dx, 0));

            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", expectedSpan == 1 ? null : expectedSpan.ToString(System.Globalization.CultureInfo.InvariantCulture)), edits);
            Assert.DoesNotContain(edits, x => x.Name == "Margin");
        }

        [Fact]
        public void WithoutSnapping_TheEdgeStopsShortOfTheLine()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-6, 0), snap: false);

            Assert.Contains(new AttributeEdit("Margin", "0,0,6,0"), edits);
        }

        [Fact]
        public void TheSnapDistance_IsConfigurable()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement { SnapDistance = 20 }.BeginResize(context, ResizeHandle.Right).Update(new Vector2(-15, 0));

            Assert.DoesNotContain(edits, x => x.Name == "Margin");
        }

        [Fact]
        public void PullingAStretchedEdgeBack_ShrinksTheSpanToTheTrackItIsIn()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            Grid.SetColumnSpan(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            // The right edge at 200 moves to 80: inside column 0.
            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-120, 0));

            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", null), edits);
            Assert.Contains(new AttributeEdit("Margin", "0,0,20,0"), edits);
        }

        [Fact]
        public void AStretchedStartEdge_MovesTheColumnBack_AndKeepsTheInset()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            Grid.SetColumn(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            // The left edge at 200 moves to 160: 60 into column 1.
            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Left).Update(new Vector2(-40, 0));

            Assert.Contains(new AttributeEdit("Grid.Column", "1"), edits);
            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", "2"), edits);
            Assert.Contains(new AttributeEdit("Margin", "60,0,0,0"), edits);
        }

        [Fact]
        public void AnExistingInset_IsKeptOnTheEdgeThatIsNotDragged()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement { Margin = new Thickness(10, 5, 0, 0) });
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Bottom).Update(new Vector2(0, -20));

            Assert.Contains(new AttributeEdit("Margin", "10,5,0,20"), edits);
        }

        [Fact]
        public void AStretchedEdge_NeverCrossesTheOtherEdge()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-500, 0));

            Assert.Contains(new AttributeEdit("Margin", "0,0,99,0"), edits);
        }

        [Fact]
        public void AStretchedEdge_StopsAtTheGridEdge()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(1000, 0));

            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", "3"), edits);
            Assert.DoesNotContain(edits, x => x.Name == "Margin");
        }

        [Fact]
        public void AGridResize_OutlinesTheTargetSpan()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);
            IResizeOperation resize = new GridPlacement().BeginResize(context, ResizeHandle.BottomRight);

            resize.Update(new Vector2(130, 20));

            Assert.Equal(new RectangleF(0, 0, 300, 100), resize.Indicator);
        }

        [Fact]
        public void AnExplicitWidthInAGrid_IsResizedWithTheSpan()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement { Width = 80, HorizontalAlignment = HorizontalAlignment.Left });
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(60, 0));

            Assert.Contains(new AttributeEdit("Width", "140"), edits);
            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", "2"), edits);
        }

        [Fact]
        public void TheGridOwnsItsAttachedAttributes()
        {
            Assert.Equal(["Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan"], new GridPlacement().OwnedAttributes);
        }

        [Fact]
        public void TheDefaultRegistry_KnowsStacksAndGrids()
        {
            var registry = new PlacementRegistry();

            Assert.IsType<StackPanelPlacement>(registry.Resolve(new StackPanel()));
            Assert.IsType<GridPlacement>(registry.Resolve(new Grid()));
            Assert.IsType<MarginPlacement>(registry.Resolve(new Panel()));
        }

        private static (StackPanel Stack, UIElement[] Children) Stack(Orientation orientation, int count, int size)
        {
            var stack = new StackPanel { Orientation = orientation };
            var children = new UIElement[count];
            for (int i = 0; i < count; i++)
            {
                children[i] = new UIElement { Width = size, Height = size };
                stack.Children.Add(children[i]);
            }

            stack.Arrange(new Rectangle(0, 0, 300, 300));
            return (stack, children);
        }

        private static (Grid Grid, UIElement Element) Grid3x3(UIElement element)
        {
            var grid = new Grid();
            for (int i = 0; i < 3; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 100f });
                grid.RowDefinitions.Add(new RowDefinition { Height = 50f });
            }

            grid.Children.Add(element);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            return (grid, element);
        }

        private static PlacementContext Context(UIElement container, UIElement element, UIElement[] others, bool isCurrent)
        {
            PlacementChild[] children = [.. others.Select((x, i) => new PlacementChild(i, x))];
            Rectangle bounds = isCurrent ? PlacementContext.ToLocal(container, element.ActualBounds) : new Rectangle(0, 0, (int)element.Width, (int)element.Height);
            return new PlacementContext(container, element, children, children.Length, bounds, isCurrent);
        }
    }
}
