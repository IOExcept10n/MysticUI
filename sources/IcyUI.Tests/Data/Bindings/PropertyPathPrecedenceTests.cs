using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Xunit;

namespace Icy.Tests.Data.Bindings
{
    public class PropertyPathPrecedenceTests
    {
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
        public void PropertyPath_ResolvesRegisteredProperty_ThroughPrecedenceSystem()
        {
            var target = new BindingTarget();
            var path = new PropertyPath(nameof(BindingTarget.Score), typeof(BindingTarget));
            var reference = PropertyRegistry.Default.GetPropertyStore(typeof(BindingTarget)).GetProperty(nameof(BindingTarget.Score));

            reference.SetTierValue(target, PropertyValuePrecedence.Style, 5);
            Assert.Equal(5, path.GetValue(target));

            path.SetValue(target, 42);

            Assert.Equal(42, target.Score);

            reference.SetTierValue(target, PropertyValuePrecedence.Animation, 100);
            Assert.Equal(42, target.Score);
        }

        [Fact]
        public void CompiledPropertyPath_ResolvesRegisteredProperty_ThroughPrecedenceSystem()
        {
            var target = new BindingTarget();
            var path = new CompiledPropertyPath(nameof(BindingTarget.Score), typeof(BindingTarget));
            var reference = PropertyRegistry.Default.GetPropertyStore(typeof(BindingTarget)).GetProperty(nameof(BindingTarget.Score));

            reference.SetTierValue(target, PropertyValuePrecedence.Style, 7);
            Assert.Equal(7, path.GetValue(target));

            path.SetValue(target, 21);

            Assert.Equal(21, target.Score);
        }
    }
}
