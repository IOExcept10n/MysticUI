using System.Drawing;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class GridTests
    {
        [Fact]
        public void PixelAutoStarColumns_ResolveToExpectedWidths()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 50f });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            var autoChild = new UIElement { Width = 80, Height = 20 };
            Grid.SetColumn(autoChild, 1);
            var starChild = new UIElement();
            Grid.SetColumn(starChild, 2);
            grid.Children.Add(autoChild);
            grid.Children.Add(starChild);

            grid.Arrange(new Rectangle(0, 0, 300, 100));

            Assert.Equal(50, grid.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(80, grid.ColumnDefinitions[1].ActualWidth);
            Assert.Equal(170, grid.ColumnDefinitions[2].ActualWidth);
            Assert.Equal(50, autoChild.ActualBounds.X);
            Assert.Equal(130, starChild.ActualBounds.X);
            Assert.Equal(170, starChild.ActualBounds.Width);
        }

        [Fact]
        public void StarColumns_ShareRemainingSpaceByWeight()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.StarWeighted(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.StarWeighted(3) });

            grid.Arrange(new Rectangle(0, 0, 400, 100));

            Assert.Equal(100, grid.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(300, grid.ColumnDefinitions[1].ActualWidth);
        }

        [Fact]
        public void ChildWithNoAttachedProperty_DefaultsToCellZeroZero()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 50f });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 50f });

            var child = new UIElement();
            grid.Children.Add(child);

            Assert.Equal(0, Grid.GetRow(child));
            Assert.Equal(0, Grid.GetColumn(child));
        }

        [Fact]
        public void OutOfRangeColumn_ClampsToLastTrack()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 50f });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 50f });

            var child = new UIElement();
            Grid.SetColumn(child, 99);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 100, 50));

            Assert.Equal(50, child.ActualBounds.X);
        }

        [Fact]
        public void NoDefinitions_BehavesAsSingleImplicitStarTrack()
        {
            var grid = new Grid();
            var child = new UIElement();
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 100, 60));

            Assert.Equal(100, child.ActualBounds.Width);
            Assert.Equal(60, child.ActualBounds.Height);
        }

        [Fact]
        public void RowAndColumn_PlaceChildInCorrectCell()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 40f });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 40f });
            grid.RowDefinitions.Add(new RowDefinition { Height = 30f });
            grid.RowDefinitions.Add(new RowDefinition { Height = 30f });

            var child = new UIElement();
            Grid.SetRow(child, 1);
            Grid.SetColumn(child, 1);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 80, 60));

            Assert.Equal(40, child.ActualBounds.X);
            Assert.Equal(30, child.ActualBounds.Y);
        }
    }
}
