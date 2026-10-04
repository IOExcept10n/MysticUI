// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;
using Xunit;

namespace Icy.Tests.Design.Text
{
    public class TextChangeSetTests
    {
        private const string Original = "Hello world!";

        [Fact]
        public void Apply_ReplacesInsertsAndDeletesInOnePass()
        {
            Assert.Equal(">> Hello there", CreateSet().Apply(Original));
        }

        [Fact]
        public void Changes_AreSortedByStart()
        {
            Assert.Equal([0, 6, 11], CreateSet().Select(x => x.Span.Start));
        }

        [Fact]
        public void Invert_RestoresTheOriginalText()
        {
            TextChangeSet set = CreateSet();
            string changed = set.Apply(Original);

            Assert.Equal(Original, set.Invert(Original).Apply(changed));
        }

        [Fact]
        public void Constructor_RejectsOverlappingChanges()
        {
            Assert.Throws<ArgumentException>(() => new TextChangeSet(
            [
                new TextChange(new TextSpan(0, 5), "a"),
                new TextChange(new TextSpan(3, 4), "b"),
            ]));
        }

        [Fact]
        public void Constructor_RejectsTwoInsertionsAtTheSameOffset()
        {
            Assert.Throws<ArgumentException>(() => new TextChangeSet(
            [
                new TextChange(new TextSpan(2, 0), "a"),
                new TextChange(new TextSpan(2, 0), "b"),
            ]));
        }

        [Fact]
        public void Apply_RejectsAChangeOutsideTheText()
        {
            var set = new TextChangeSet([new TextChange(new TextSpan(10, 5), "x")]);

            Assert.Throws<ArgumentException>(() => set.Apply("short"));
        }

        // Insert "abc" at 2, replace [5,8) with "X", delete [10,12).
        [Theory]
        [InlineData(0, 0)]
        [InlineData(2, 5)]
        [InlineData(4, 7)]
        [InlineData(5, null)]
        [InlineData(6, null)]
        [InlineData(8, 9)]
        [InlineData(10, null)]
        [InlineData(12, 11)]
        public void MapPosition_ShiftsSurvivorsAndDropsReplacedPositions(int position, int? expected)
        {
            var set = new TextChangeSet(
            [
                new TextChange(new TextSpan(2, 0), "abc"),
                new TextChange(new TextSpan(5, 3), "X"),
                new TextChange(new TextSpan(10, 2), string.Empty),
            ]);

            Assert.Equal(expected, set.MapPosition(position));
        }

        private static TextChangeSet CreateSet() => new(
        [
            new TextChange(new TextSpan(6, 5), "there"),
            new TextChange(new TextSpan(0, 0), ">> "),
            new TextChange(new TextSpan(11, 1), string.Empty),
        ]);
    }
}
