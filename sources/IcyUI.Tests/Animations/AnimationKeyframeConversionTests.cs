// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Animations;
using Icy.Data.Markup;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Animations
{
    public class AnimationKeyframeConversionTests
    {
        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        [Fact]
        public void StringKeyframeValues_AreConvertedToThePropertysRealType()
        {
            var element = new TestElement();
            var timeline = new Timeline("Width", TimeSpan.FromSeconds(1));
            timeline.AddKeyframe(0f, "0"); // markup-shaped: a raw string, not a float
            timeline.AddKeyframe(1f, "40");

            var animation = new Animation(element, timeline);
            animation.Start();

            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(1));

            Assert.Equal(40f, element.Width); // would still be the string "40" (or throw) without conversion
        }

        [Fact]
        public void AlreadyTypedKeyframeValues_PassThroughUnchanged()
        {
            var element = new TestElement();
            var timeline = Timeline.FromTo("Width", TimeSpan.FromSeconds(1), 0f, 40f);

            var animation = new Animation(element, timeline);
            animation.Start();

            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(1));

            Assert.Equal(40f, element.Width);
        }
    }
}
