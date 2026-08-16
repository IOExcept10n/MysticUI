using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Xunit;

namespace Icy.Tests.Data.Bindings
{
    public class FrameDrivenBindingTests
    {
        private class PlainSource
        {
            public int Value { get; set; }
        }

        private class BindingTarget : DependencyObject
        {
            private int score;

            [RegisterReference]
            public int Score
            {
                get => score;
                set => SetProperty(ref score, value);
            }
        }

        [Fact]
        public void EveryFrame_Binding_RefreshesFromSourceThatNeverRaisesPropertyChanged()
        {
            var source = new PlainSource { Value = 1 };
            var target = new BindingTarget();
            var propertyRef = PropertyRegistry.Instance.GetPropertyStore(typeof(BindingTarget)).GetProperty(nameof(BindingTarget.Score));
            var dispatcher = Dispatcher.GetCurrentThreadDispatcher();
            var binding = new Binding(target, propertyRef, new PropertyPath(nameof(PlainSource.Value), typeof(PlainSource)))
            {
                Source = source,
                IsEnabled = true,
                UpdateTargetTrigger = UpdateTargetTrigger.EveryFrame,
            };

            try
            {
                dispatcher.UpdateFrameBindings();
                Assert.Equal(1, target.Score);

                // PlainSource has no INotifyPropertyChanged - a Reactive-mode binding would never see this.
                source.Value = 42;
                Assert.Equal(1, target.Score);

                dispatcher.UpdateFrameBindings();
                Assert.Equal(42, target.Score);
            }
            finally
            {
                binding.Dispose();
            }
        }

        [Fact]
        public void EveryFrame_Binding_StopsRefreshingAfterDispose()
        {
            var source = new PlainSource { Value = 1 };
            var target = new BindingTarget();
            var propertyRef = PropertyRegistry.Instance.GetPropertyStore(typeof(BindingTarget)).GetProperty(nameof(BindingTarget.Score));
            var dispatcher = Dispatcher.GetCurrentThreadDispatcher();
            var binding = new Binding(target, propertyRef, new PropertyPath(nameof(PlainSource.Value), typeof(PlainSource)))
            {
                Source = source,
                IsEnabled = true,
                UpdateTargetTrigger = UpdateTargetTrigger.EveryFrame,
            };

            dispatcher.UpdateFrameBindings();
            Assert.Equal(1, target.Score);

            binding.Dispose();
            source.Value = 99;
            dispatcher.UpdateFrameBindings();

            Assert.Equal(1, target.Score);
        }
    }
}
