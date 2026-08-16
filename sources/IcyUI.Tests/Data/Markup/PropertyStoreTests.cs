using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Xunit;

namespace Icy.Tests.Data.Markup
{
    public class PropertyStoreTests
    {
        private class TestObject : DependencyObject
        {
            private int number;
            private string? text;

            [RegisterReference]
            [AffectsMeasure]
            public int Number
            {
                get => number;
                set => SetProperty(ref number, value);
            }

            [RegisterReference]
            public string? Text
            {
                get => text;
                set => SetProperty(ref text, value);
            }
        }

        private class DerivedTestObject : TestObject
        {
            private bool flag;

            [RegisterReference]
            public bool Flag
            {
                get => flag;
                set => SetProperty(ref flag, value);
            }
        }

        private class AttachedTarget
        {
        }

        [AttachedProperty(nameof(GetRow), nameof(SetRow))]
        private static class GridLike
        {
            public static int GetRow(AttachedTarget target) => AttachedProperties.GetValue<int>(target, "GridLike.Row", 0);

            public static void SetRow(AttachedTarget target, int value) => AttachedProperties.SetValue(target, "GridLike.Row", value);
        }

        [Fact]
        public void GetPropertyStore_ResolvesRegisteredReferenceProperties()
        {
            var store = PropertyRegistry.Instance.GetPropertyStore(typeof(TestObject));

            Assert.True(store.TryGetProperty("Number", out IPropertyReference? number));
            Assert.True(number!.Metadata is UIPropertyMetadata { AffectsMeasure: true });
            Assert.True(store.TryGetProperty("Text", out _));
        }

        [Fact]
        public void GetPropertyStore_ReturnsSameInstanceOnRepeatedCalls()
        {
            var first = PropertyRegistry.Instance.GetPropertyStore(typeof(TestObject));
            var second = PropertyRegistry.Instance.GetPropertyStore(typeof(TestObject));

            Assert.Same(first, second);
        }

        [Fact]
        public void TryGetProperty_SearchesBaseTypeWhenInherited()
        {
            var derivedStore = PropertyRegistry.Instance.GetPropertyStore(typeof(DerivedTestObject));

            Assert.True(derivedStore.TryGetProperty("Number", searchInherited: true, out _));
            Assert.False(derivedStore.TryGetProperty("Number", searchInherited: false, out _));
        }

        [Fact]
        public void AttachedProperty_RoundTripsThroughGetterAndSetter()
        {
            var store = PropertyRegistry.Instance.GetPropertyStore(typeof(GridLike));
            Assert.True(store.TryGetProperty("Row", out IPropertyReference? row));

            var target = new AttachedTarget();
            row!.SetRawValue(target, 3);

            Assert.Equal(3, GridLike.GetRow(target));
            Assert.Equal(3, row.GetRawValue(target));
        }

        [Fact]
        public void SetTierValue_AppliesWinningTierByPrecedence()
        {
            var store = PropertyRegistry.Instance.GetPropertyStore(typeof(TestObject));
            var property = store.GetProperty("Number");
            var target = new TestObject();

            property.SetTierValue(target, PropertyValuePrecedence.Style, 1);
            Assert.Equal(1, target.Number);

            property.SetTierValue(target, PropertyValuePrecedence.VisualState, 2);
            Assert.Equal(2, target.Number);

            property.SetTierValue(target, PropertyValuePrecedence.Animation, 3);
            Assert.Equal(3, target.Number);

            property.ClearTierValue(target, PropertyValuePrecedence.Animation);
            Assert.Equal(2, target.Number);

            property.ClearTierValue(target, PropertyValuePrecedence.VisualState);
            Assert.Equal(1, target.Number);
        }

        [Fact]
        public void LocalAssignment_AlwaysOutranksActiveTiers()
        {
            var store = PropertyRegistry.Instance.GetPropertyStore(typeof(TestObject));
            var property = store.GetProperty("Number");
            var target = new TestObject();

            property.SetTierValue(target, PropertyValuePrecedence.Style, 10);
            Assert.Equal(10, target.Number);

            target.Number = 99;
            Assert.Equal(99, target.Number);

            property.SetTierValue(target, PropertyValuePrecedence.Animation, 5);
            Assert.Equal(99, target.Number);

            property.ClearTierValue(target, PropertyValuePrecedence.Animation);
            Assert.Equal(99, target.Number);

            property.ClearLocalValue(target);
            Assert.Equal(10, target.Number);
        }
    }
}
