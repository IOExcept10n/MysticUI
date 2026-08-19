using Icy.Animations;
using Xunit;

namespace Icy.Tests.Animations
{
    public class EasingTests
    {
        [Theory]
        [InlineData(0f)]
        [InlineData(0.25f)]
        [InlineData(0.5f)]
        [InlineData(0.75f)]
        [InlineData(1f)]
        public void Linear_ReturnsInputUnchanged(float t)
        {
            Assert.Equal(t, Easing.Linear(t), 5);
        }

        public static IEnumerable<object[]> BoundaryPreservingFunctions()
        {
            yield return [Easing.EaseInQuad];
            yield return [Easing.EaseOutQuad];
            yield return [Easing.EaseInOutQuad];
            yield return [Easing.EaseInCubic];
            yield return [Easing.EaseOutCubic];
            yield return [Easing.EaseInOutCubic];
        }

        [Theory]
        [MemberData(nameof(BoundaryPreservingFunctions))]
        public void StandardEasings_MapZeroToZeroAndOneToOne(EasingFunction easing)
        {
            Assert.Equal(0f, easing(0f), 5);
            Assert.Equal(1f, easing(1f), 5);
        }

        [Fact]
        public void EaseInQuad_StartsSlowerThanLinear()
        {
            Assert.True(Easing.EaseInQuad(0.25f) < 0.25f);
        }

        [Fact]
        public void EaseOutQuad_StartsFasterThanLinear()
        {
            Assert.True(Easing.EaseOutQuad(0.25f) > 0.25f);
        }
    }
}
