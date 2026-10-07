// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class SpatialNavigationTests
    {
        private static readonly Vector2 Up = -Vector2.UnitY;
        private static readonly Vector2 Down = Vector2.UnitY;
        private static readonly Vector2 Left = -Vector2.UnitX;
        private static readonly Vector2 Right = Vector2.UnitX;

        [Theory]
        [InlineData(0.9f, 0.5f, 1, 0)]
        [InlineData(0.2f, -0.8f, 0, -1)]
        [InlineData(1f, 1f, 1, 0)]
        [InlineData(-0.3f, 0.1f, -1, 0)]
        [InlineData(0f, 0f, 0, 0)]
        public void Snap_PicksTheDominantAxis_DiagonalsGoHorizontal(float x, float y, float ex, float ey)
        {
            Assert.Equal(new Vector2(ex, ey), SpatialNavigation.Snap(new Vector2(x, y)));
        }

        [Fact]
        public void FindBest_InAGrid_PicksTheAdjacentCellInEachDirection()
        {
            // 3x3 grid of 10x10 cells, 20 apart; index = row * 3 + column.
            var cells = new List<Rectangle>();
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    cells.Add(new Rectangle(column * 20, row * 20, 10, 10));
            }

            Rectangle center = cells[4];
            List<Rectangle> others = [.. cells.Where(c => c != center)];
            Assert.Equal(cells[1], others[SpatialNavigation.FindBest(center, Up, others, 0)]);
            Assert.Equal(cells[7], others[SpatialNavigation.FindBest(center, Down, others, 0)]);
            Assert.Equal(cells[3], others[SpatialNavigation.FindBest(center, Left, others, 0)]);
            Assert.Equal(cells[5], others[SpatialNavigation.FindBest(center, Right, others, 0)]);
        }

        [Fact]
        public void FindBest_ACandidateInTheBeam_BeatsACloserDiagonalOne()
        {
            var from = new Rectangle(0, 0, 100, 20);
            var diagonal = new Rectangle(110, 25, 20, 10);
            var below = new Rectangle(0, 80, 100, 20);

            Assert.Equal(1, SpatialNavigation.FindBest(from, Down, [diagonal, below], 0));
        }

        [Fact]
        public void FindBest_ToleratesASmallOverlap_ButNotALargeOne()
        {
            var from = new Rectangle(0, 0, 50, 20);

            Assert.Equal(0, SpatialNavigation.FindBest(from, Down, [new Rectangle(0, 17, 50, 20)], 4));
            Assert.Equal(-1, SpatialNavigation.FindBest(from, Down, [new Rectangle(0, 15, 50, 20)], 4));
        }

        [Fact]
        public void FindBest_OnAFullTie_KeepsTheEarlierCandidate()
        {
            var from = new Rectangle(40, 0, 20, 20);
            var leftBelow = new Rectangle(0, 50, 20, 20);
            var rightBelow = new Rectangle(80, 50, 20, 20);

            Assert.Equal(0, SpatialNavigation.FindBest(from, Down, [leftBelow, rightBelow], 0));
            Assert.Equal(0, SpatialNavigation.FindBest(from, Down, [rightBelow, leftBelow], 0));
        }

        [Fact]
        public void FindBest_WithNothingAhead_ReturnsMinusOne()
        {
            var from = new Rectangle(0, 100, 20, 20);

            Assert.Equal(-1, SpatialNavigation.FindBest(from, Down, [new Rectangle(0, 0, 20, 20)], 0));
            Assert.Equal(-1, SpatialNavigation.FindBest(from, Down, [], 0));
        }
    }
}
