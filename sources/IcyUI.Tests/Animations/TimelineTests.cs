using Icy.Animations;
using Xunit;

namespace Icy.Tests.Animations
{
    public class TimelineTests
    {
        [Fact]
        public void Constructor_ThrowsForNonPositiveDuration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Timeline("Value", TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Timeline("Value", TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void Constructor_ThrowsForBlankTargetProperty()
        {
            Assert.Throws<ArgumentException>(() => new Timeline(string.Empty, TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void RepeatCount_ThrowsForZeroOrNegativeNonForeverValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Timeline("Value", TimeSpan.FromSeconds(1)) { RepeatCount = 0 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new Timeline("Value", TimeSpan.FromSeconds(1)) { RepeatCount = -2 });
        }

        [Fact]
        public void RepeatCount_AcceptsForever()
        {
            var timeline = new Timeline("Value", TimeSpan.FromSeconds(1)) { RepeatCount = Timeline.Forever };

            Assert.Equal(Timeline.Forever, timeline.RepeatCount);
        }

        [Fact]
        public void AddKeyframe_KeepsKeyframesSortedByOffset()
        {
            var timeline = new Timeline("Value", TimeSpan.FromSeconds(1));

            timeline.AddKeyframe(1f, "end");
            timeline.AddKeyframe(0f, "start");
            timeline.AddKeyframe(0.5f, "middle");

            Assert.Equal([0f, 0.5f, 1f], timeline.Keyframes.Select(k => k.Offset));
            Assert.Equal(["start", "middle", "end"], timeline.Keyframes.Select(k => k.Value));
        }

        [Fact]
        public void FromTo_AddsFromAndToAsKeyframesZeroAndOne()
        {
            var timeline = Timeline.FromTo("Value", TimeSpan.FromSeconds(2), 0f, 1f, repeatCount: 3, autoReverse: true);

            Assert.Equal(2, timeline.Keyframes.Count);
            Assert.Equal(0f, timeline.Keyframes[0].Offset);
            Assert.Equal(0f, timeline.Keyframes[0].Value);
            Assert.Equal(1f, timeline.Keyframes[1].Offset);
            Assert.Equal(1f, timeline.Keyframes[1].Value);
            Assert.Equal(3, timeline.RepeatCount);
            Assert.True(timeline.AutoReverse);
        }

        [Fact]
        public void Easing_DefaultsToLinear()
        {
            var timeline = new Timeline("Value", TimeSpan.FromSeconds(1));

            Assert.Same(Easing.Linear, timeline.Easing);
        }
    }
}
