// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Markup;
using Icy.Tests.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class GridSpanTests
    {
        [Fact]
        public void ColumnSpan_ArrangesAcrossTheSpannedTracks()
        {
            Grid grid = CreateGrid(columns: [50f, 50f, 50f]);
            var child = new UIElement();
            Grid.SetColumnSpan(child, 2);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 150, 40));

            Assert.Equal(new Rectangle(0, 0, 100, 40), child.ActualBounds);
        }

        [Fact]
        public void RowSpan_ArrangesAcrossTheSpannedTracks()
        {
            var grid = new Grid();
            for (int i = 0; i < 3; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = 20f });
            var child = new UIElement();
            Grid.SetRow(child, 1);
            Grid.SetRowSpan(child, 2);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 100, 60));

            Assert.Equal(new Rectangle(0, 20, 100, 40), child.ActualBounds);
        }

        [Fact]
        public void ASpanPastTheLastTrack_IsClamped()
        {
            Grid grid = CreateGrid(columns: [50f, 50f, 50f]);
            var child = new UIElement();
            Grid.SetColumn(child, 1);
            Grid.SetColumnSpan(child, 5);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 150, 40));

            Assert.Equal(new Rectangle(50, 0, 100, 40), child.ActualBounds);
        }

        [Fact]
        public void ASpanBelowOne_IsTreatedAsOne()
        {
            Grid grid = CreateGrid(columns: [50f, 50f]);
            var child = new UIElement();
            Grid.SetColumnSpan(child, 0);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 100, 40));

            Assert.Equal(1, Grid.GetColumnSpan(child));
            Assert.Equal(50, child.ActualBounds.Width);
        }

        [Fact]
        public void AutoTracks_GrowEvenlyForASpanningChild()
        {
            Grid grid = CreateGrid(columns: [GridLength.Auto, GridLength.Auto]);
            grid.Children.Add(new UIElement { Width = 30, Height = 10 });
            var wide = new UIElement { Width = 100, Height = 10 };
            Grid.SetColumnSpan(wide, 2);
            grid.Children.Add(wide);

            grid.Arrange(new Rectangle(0, 0, 300, 40));

            // Single-span children size the tracks first (30, 0); the spanning child's deficit of 70 is split evenly.
            Assert.Equal(65, grid.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(35, grid.ColumnDefinitions[1].ActualWidth);
        }

        [Fact]
        public void PixelTracks_DontGrowForASpanningChild()
        {
            Grid grid = CreateGrid(columns: [40f, GridLength.Auto]);
            var wide = new UIElement { Width = 100, Height = 10 };
            Grid.SetColumnSpan(wide, 2);
            grid.Children.Add(wide);

            grid.Arrange(new Rectangle(0, 0, 300, 40));

            Assert.Equal(40, grid.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(60, grid.ColumnDefinitions[1].ActualWidth);
        }

        [Fact]
        public void Markup_SetsTheSpans()
        {
            var loader = new MarkupLoader(MarkupLoadObserverTests.CreateConfiguration());

            UIElement root = loader.Load("<Grid><Border x:Name=\"b\" Grid.RowSpan=\"3\" Grid.ColumnSpan=\"2\"/></Grid>");

            var border = (Border)MarkupNameScope.GetScope(root)!.Find("b")!;
            Assert.Equal(3, Grid.GetRowSpan(border));
            Assert.Equal(2, Grid.GetColumnSpan(border));
        }

        private static Grid CreateGrid(GridLength[] columns)
        {
            var grid = new Grid();
            foreach (GridLength width in columns)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            return grid;
        }
    }
}
