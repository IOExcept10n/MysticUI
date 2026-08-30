// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;
using Icy.Animations;
using Icy.Data.Markup;
using Icy.UI;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.UI.Styles
{
    public class VisualStateTransitionTests
    {
        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        [Fact]
        public void DurationUnset_SnapsInstantly()
        {
            var element = new TestElement();
            var group = new VisualStateGroup("Group");
            var hovered = new VisualState("Hovered", ControlState.Hovered);
            hovered.Setters["Width"] = 20f;
            group.States.Add(hovered);
            element.RegisterStateGroup(group);

            element.ControlState = ControlState.Hovered;

            Assert.Equal(20f, element.Width);
        }

        [Fact]
        public void DurationSet_AnimatesFromCurrentValueToTarget()
        {
            // Give Width a real starting value through the Style tier - a local (directly-assigned) value would
            // permanently outrank every tier (see StyleTests.Apply_LocalValueOutranksStyle), and NaN (Width's
            // unset default) can't be lerped by ValueInterpolator - so neither works as a baseline here.
            var style = new Style(typeof(TestElement));
            style.Setters["Width"] = 0f;
            var element = new TestElement { Style = style };

            var group = new VisualStateGroup("Group");
            var hovered = new VisualState("Hovered", ControlState.Hovered) { Duration = TimeSpan.FromSeconds(1) };
            hovered.Setters["Width"] = 20f;
            group.States.Add(hovered);
            element.RegisterStateGroup(group);

            element.ControlState = ControlState.Hovered;

            // Immediately after triggering, the transition has started but not completed - value should have moved
            // partway or still be at the animation's t=0 sample, not already snapped to 20.
            Assert.NotEqual(20f, element.Width);

            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(2));

            Assert.Equal(20f, element.Width);
        }

        [Fact]
        public void RetriggeringBeforeCompletion_ReplacesTheInFlightAnimation()
        {
            var style = new Style(typeof(TestElement));
            style.Setters["Width"] = 0f;
            var element = new TestElement { Style = style };

            var group = new VisualStateGroup("Group");
            var hovered = new VisualState("Hovered", ControlState.Hovered) { Duration = TimeSpan.FromSeconds(1) };
            hovered.Setters["Width"] = 20f;
            group.States.Add(hovered);
            element.RegisterStateGroup(group);

            // Add "Normal" only after registering, so the group's registration-time evaluation (ControlState
            // defaults to Normal, which a State = ControlState.Normal always matches) doesn't itself kick off a
            // spurious transition before the test is ready to exercise the interruption.
            var normal = new VisualState("Normal", ControlState.Normal) { Duration = TimeSpan.FromSeconds(1) };
            normal.Setters["Width"] = 0f;
            group.States.Add(normal);

            element.ControlState = ControlState.Hovered;
            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromMilliseconds(500));
            float midway = element.Width;
            Assert.True(midway is > 0f and < 20f);

            // Capture the in-flight Hovered->Width animation via its private tracking dictionary so we can prove -
            // not just infer from the final settled value, which two competing animations could coincidentally
            // still produce - that retriggering genuinely Stop()s it rather than leaving it registered to fight
            // whatever animation the Normal transition starts.
            Animation? oldAnimation = GetTrackedTransition(element, "Width");
            Assert.NotNull(oldAnimation);
            Assert.True(oldAnimation!.IsRunning);

            element.ControlState = ControlState.Normal; // re-trigger before the Hovered transition finishes

            Assert.False(oldAnimation.IsRunning); // the old transition was actually Stop()ped, not merely orphaned

            Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(TimeSpan.FromSeconds(2));

            Assert.Equal(0f, element.Width); // settles on Normal's target, not stuck fighting the old animation
        }

        private static Animation? GetTrackedTransition(UIElement element, string propertyName)
        {
            FieldInfo field = typeof(UIElement).GetField("activeStateTransitions", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var transitions = (Dictionary<string, Animation>?)field.GetValue(element);
            return transitions != null && transitions.TryGetValue(propertyName, out Animation? animation) ? animation : null;
        }
    }
}
