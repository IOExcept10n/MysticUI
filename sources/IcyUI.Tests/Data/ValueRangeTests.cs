using System.Globalization;
using Icy.Data;
using Xunit;

namespace Icy.Tests.Data
{
    public class ValueRangeTests
    {
        [Theory]
        [InlineData(0d, 1d, false, false, 0d, true)]
        [InlineData(0d, 1d, false, false, 1d, true)]
        [InlineData(0d, 1d, true, false, 0d, false)]
        [InlineData(0d, 1d, false, true, 1d, false)]
        [InlineData(0d, double.PositiveInfinity, true, false, 1e30, true)]
        [InlineData(0d, double.PositiveInfinity, false, false, double.PositiveInfinity, false)]
        [InlineData(0d, 1d, false, false, double.NaN, false)]
        [InlineData(-1d, double.PositiveInfinity, false, false, -1d, true)]
        public void Contains_HonoursExclusiveEnds_AndRejectsNonFiniteValues(double min, double max, bool minExclusive, bool maxExclusive, double value, bool expected) =>
            Assert.Equal(expected, new ValueRange(min, max, minExclusive, maxExclusive).Contains(value));

        [Theory]
        [InlineData(0d, double.PositiveInfinity, true, false, "Must be greater than 0.")]
        [InlineData(0d, double.PositiveInfinity, false, false, "Must be at least 0.")]
        [InlineData(double.NegativeInfinity, 10d, false, true, "Must be less than 10.")]
        [InlineData(double.NegativeInfinity, 10d, false, false, "Must be at most 10.")]
        [InlineData(0d, 1d, false, false, "Must be between 0 and 1.")]
        [InlineData(0d, 1d, true, false, "Must be greater than 0 and at most 1.")]
        [InlineData(0d, 1d, false, true, "Must be at least 0 and less than 1.")]
        [InlineData(0d, 1d, true, true, "Must be greater than 0 and less than 1.")]
        [InlineData(double.NegativeInfinity, double.PositiveInfinity, false, false, "Must be a finite number.")]
        public void Describe_NamesTheRule(double min, double max, bool minExclusive, bool maxExclusive, string expected) =>
            Assert.Equal(expected, new ValueRange(min, max, minExclusive, maxExclusive).Describe());

        [Fact]
        public void Describe_UsesTheInvariantCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            try
            {
                Assert.Equal("Must be between 0.5 and 1.5.", new ValueRange(0.5, 1.5, false, false).Describe());
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void IsBounded_NeedsBothEndsFinite()
        {
            Assert.True(new ValueRange(0, 1, false, false).IsBounded);
            Assert.False(new ValueRange(0, double.PositiveInfinity, false, false).IsBounded);
        }
    }
}
