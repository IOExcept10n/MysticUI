using System.Drawing;
using Icy.Animations;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Xunit;

namespace Icy.Tests.Animations
{
    public class AnimationTests
    {
        private class AnimationTarget : DependencyObject
        {
            private float value;
            private Color tint = Color.Black;
            private string? label = "before";
            private int locked;

            [RegisterReference]
            public float Value
            {
                get => value;
                set => SetProperty(ref this.value, value);
            }

            [RegisterReference]
            public Color Tint
            {
                get => tint;
                set => SetProperty(ref tint, value);
            }

            [RegisterReference]
            public string? Label
            {
                get => label;
                set => SetProperty(ref label, value);
            }

            [RegisterReference]
            [NonAnimatable]
            public int Locked
            {
                get => locked;
                set => SetProperty(ref locked, value);
            }
        }

        [Fact]
        public void Constructor_ThrowsForUnregisteredProperty()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo("NoSuchProperty", TimeSpan.FromSeconds(1), 0f, 1f);

            Assert.Throws<ArgumentException>(() => new Animation(target, timeline));
        }

        [Fact]
        public void Constructor_ThrowsForNonAnimatableProperty()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Locked), TimeSpan.FromSeconds(1), 0, 10);

            Assert.Throws<InvalidOperationException>(() => new Animation(target, timeline));
        }

        [Fact]
        public void Start_AppliesFirstKeyframeImmediately()
        {
            // No local value assigned beforehand - a prior local assignment always outranks the Animation tier
            // (see LocalAssignment_AlwaysOutranksTheAnimationTier below), so this uses a from-value (5f) distinct
            // from the untouched field default (0f) to prove Start() actually applied it.
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 5f, 100f);
            var animation = new Animation(target, timeline);

            animation.Start();

            Assert.Equal(5f, target.Value);
            Assert.True(animation.IsRunning);
        }

        [Fact]
        public void Update_InterpolatesLinearlyAtMidpoint()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f);
            var animation = new Animation(target, timeline);

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(0.5));

            Assert.Equal(50f, target.Value, 3);
        }

        [Fact]
        public void Update_InterpolatesColors()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Tint), TimeSpan.FromSeconds(1), Color.Black, Color.White);
            var animation = new Animation(target, timeline);

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(0.5));

            Assert.Equal(Color.FromArgb(255, 128, 128, 128), target.Tint);
        }

        [Fact]
        public void Update_HoldsThenSnapsForNonLerpableValues()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Label), TimeSpan.FromSeconds(1), "before", "after");
            var animation = new Animation(target, timeline);

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(0.9));
            Assert.Equal("before", target.Label);

            animation.Update(TimeSpan.FromSeconds(0.1));
            Assert.Equal("after", target.Label);
        }

        [Fact]
        public void Update_CompletesAfterSinglePass_HoldingFinalValue()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f);
            var animation = new Animation(target, timeline);
            bool completed = false;
            animation.Completed += (_, _) => completed = true;

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(1));

            Assert.True(completed);
            Assert.False(animation.IsRunning);
            Assert.Equal(100f, target.Value, 3);
        }

        [Fact]
        public void Update_RepeatsRequestedNumberOfTimes()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f, repeatCount: 2);
            var animation = new Animation(target, timeline);
            int completedCount = 0;
            animation.Completed += (_, _) => completedCount++;

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(1));
            Assert.True(animation.IsRunning);
            Assert.Equal(0f, target.Value, 3);

            animation.Update(TimeSpan.FromSeconds(1));
            Assert.False(animation.IsRunning);
            Assert.Equal(1, completedCount);
            Assert.Equal(100f, target.Value, 3);
        }

        [Fact]
        public void Update_AutoReverse_PingPongsBetweenEndpoints()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f, autoReverse: true);
            var animation = new Animation(target, timeline);

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(1));
            Assert.True(animation.IsRunning);
            Assert.Equal(100f, target.Value, 3);

            animation.Update(TimeSpan.FromSeconds(1));
            Assert.False(animation.IsRunning);
            Assert.Equal(0f, target.Value, 3);
        }

        [Fact]
        public void Update_Forever_NeverCompletesOnItsOwn()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f, repeatCount: Timeline.Forever);
            var animation = new Animation(target, timeline);

            animation.Start();
            for (int i = 0; i < 50; i++)
                animation.Update(TimeSpan.FromSeconds(1));

            Assert.True(animation.IsRunning);
        }

        [Fact]
        public void Pause_StopsAdvancingUntilResumed()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f);
            var animation = new Animation(target, timeline);

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(0.5));
            animation.Pause();
            animation.Update(TimeSpan.FromSeconds(0.5));

            Assert.Equal(50f, target.Value, 3);

            animation.Resume();
            animation.Update(TimeSpan.FromSeconds(0.5));

            Assert.Equal(100f, target.Value, 3);
        }

        [Fact]
        public void Stop_RevertsToTheValueFromBeforeTheAnimationStarted()
        {
            // No local value assigned beforehand - see Start_AppliesFirstKeyframeImmediately's note. Reverting
            // lands back on the untouched field default (0f) once the Animation tier's contribution is cleared.
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f);
            var animation = new Animation(target, timeline);

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(0.5));
            Assert.Equal(50f, target.Value, 3);

            animation.Stop();

            Assert.Equal(0f, target.Value);
            Assert.False(animation.IsRunning);
        }

        [Fact]
        public void Stop_AfterNaturalCompletion_RevertsHeldFinalValue()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f);
            var animation = new Animation(target, timeline);

            animation.Start();
            animation.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(100f, target.Value, 3);

            animation.Stop();

            Assert.Equal(0f, target.Value);
        }

        [Fact]
        public void Stop_AfterLocalAssignmentDuringPlayback_RevertsToThatLocalValue()
        {
            // Unlike a local value set before Start() (which blocks the animation from ever applying - see
            // LocalAssignment_AlwaysOutranksTheAnimationTier), a local assignment mid-flight becomes the new
            // fallback the Animation tier's contribution reverts to once cleared.
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f);
            var animation = new Animation(target, timeline);

            animation.Start();
            target.Value = 7f;
            animation.Stop();

            Assert.Equal(7f, target.Value);
        }

        [Fact]
        public void LocalAssignment_AlwaysOutranksTheAnimationTier()
        {
            var target = new AnimationTarget();
            var timeline = Timeline.FromTo(nameof(AnimationTarget.Value), TimeSpan.FromSeconds(1), 0f, 100f);
            var animation = new Animation(target, timeline);

            animation.Start();
            target.Value = 999f;
            animation.Update(TimeSpan.FromSeconds(0.5));

            Assert.Equal(999f, target.Value);
        }
    }
}
