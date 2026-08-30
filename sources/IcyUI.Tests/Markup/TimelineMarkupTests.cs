// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Animations;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Xunit;

namespace Icy.Tests.Markup
{
    public class TimelineMarkupTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void TimelineWithKeyframes_ParsesToTheEquivalentHandBuiltTimeline()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var timeline = (Timeline)loader.LoadObject(
                """
                <Timeline TargetProperty="Opacity" Duration="0:0:1" Easing="EaseInOutQuad" RepeatCount="-1" AutoReverse="True">
                  <AnimationKeyframe Offset="0" Value="0.5"/>
                  <AnimationKeyframe Offset="1" Value="1.0"/>
                </Timeline>
                """);

            Assert.Equal("Opacity", timeline.TargetProperty);
            Assert.Equal(TimeSpan.FromSeconds(1), timeline.Duration);
            Assert.Equal(Icy.Animations.Easing.EaseInOutQuad, timeline.Easing);
            Assert.Equal(Timeline.Forever, timeline.RepeatCount);
            Assert.True(timeline.AutoReverse);
            Assert.Equal(2, timeline.Keyframes.Count);
            Assert.Equal(0f, timeline.Keyframes[0].Offset);
            Assert.Equal("0.5", timeline.Keyframes[0].Value); // still a raw string pre-Animation-construction - see Task 12 design note
        }
    }
}
