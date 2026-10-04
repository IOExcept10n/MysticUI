// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;
using Xunit;

namespace Icy.Tests.Design.Text
{
    public class LineMapTests
    {
        [Theory]
        [InlineData("a\nbc\nd", 3, 1, 5)]
        [InlineData("a\r\nbc\r\nd", 3, 1, 7)]
        [InlineData("a\rbc\rd", 3, 1, 5)]
        [InlineData("a\nbc", 2, 2, 3)]
        [InlineData("\tx", 1, 2, 1)]
        public void ToOffset_CountsEveryLineBreakStyleOnce(string text, int line, int column, int expected)
        {
            Assert.Equal(expected, new LineMap(text).ToOffset(line, column));
        }

        [Fact]
        public void TryToOffset_RejectsALineOutsideTheText()
        {
            Assert.False(new LineMap("a\nb").TryToOffset(3, 1, out _));
        }
    }
}
