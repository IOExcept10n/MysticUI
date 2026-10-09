using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Xunit;

namespace Icy.Tests.Data
{
    public class PropertyGridEntryTests
    {
        private sealed class PlainPoco
        {
            public string Name { get; set; } = string.Empty;

            [Category("Stats")]
            public int Health { get; set; }

            [Category("Stats")]
            [Range(0, 100)]
            public float Armor { get; set; }

            [DisplayName("Is Alive")]
            public bool Alive { get; set; }

            [Browsable(false)]
            public string Hidden { get; set; } = "secret";

            public string ReadOnlyValue { get; } = "fixed";

            [ReadOnly(true)]
            public string ExplicitlyReadOnly { get; set; } = "locked";

            [Range(0, 100)]
            public string RangeOnString { get; set; } = "text";

            public int PrivatelySettable { get; private set; } = 7;

            [Range(100, 1)]
            public int InvertedRange { get; set; }

            [Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]
            public float Positive { get; set; } = 1;

            [DefaultValue(float.NaN)]
            [Range(0d, double.PositiveInfinity)]
            public float Limit { get; set; } = float.NaN;

            [DefaultValue(40)]
            public float ConvertedDefault { get; set; } = 40;

            [Description("How far the hero can see.")]
            public int Sight { get; set; }

            [EditorBrowsable(EditorBrowsableState.Never)]
            public int NeverShown { get; set; }

            [EditorBrowsable(EditorBrowsableState.Advanced)]
            public int AdvancedShown { get; set; }

            [Range(1, 1, MinimumIsExclusive = true)]
            public int EmptyRange { get; set; } = 1;
        }

        private sealed class RegisteredTarget : Icy.Data.Markup.DependencyObject
        {
            private string registeredName = string.Empty;

            [RegisterReference]
            [Category("Registered")]
            public string RegisteredName
            {
                get => registeredName;
                set => SetProperty(ref registeredName, value);
            }
        }

        [Fact]
        public void EnumerateFor_PlainPoco_ReadsCategoryDisplayNameAndRange()
        {
            var target = new PlainPoco();
            var entries = PropertyGridEntry.EnumerateFor(target);

            var health = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Health));
            Assert.Equal("Stats", health.Category);

            var armor = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Armor));
            Assert.Equal(new ValueRange(0, 100, false, false), armor.Range);

            var alive = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Alive));
            Assert.Equal("Is Alive", alive.DisplayName);
        }

        [Fact]
        public void EnumerateFor_BrowsableFalse_IsExcluded()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            Assert.DoesNotContain(entries, e => e.Name == nameof(PlainPoco.Hidden));
        }

        [Fact]
        public void EnumerateFor_NoSetterOrExplicitReadOnly_IsMarkedReadOnly()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());

            var noSetter = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ReadOnlyValue));
            Assert.True(noSetter.IsReadOnly);

            var explicitReadOnly = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ExplicitlyReadOnly));
            Assert.True(explicitReadOnly.IsReadOnly);
        }

        [Fact]
        public void GetValue_And_TrySetValue_RoundTripOnPlainPoco()
        {
            var target = new PlainPoco();
            var entries = PropertyGridEntry.EnumerateFor(target);
            var health = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Health));

            Assert.True(health.TrySetValue(target, 42));
            Assert.Equal(42, health.GetValue(target));
            Assert.Equal(42, target.Health);
        }

        [Fact]
        public void TrySetValue_OnReadOnlyEntry_ReturnsFalseAndDoesNotThrow()
        {
            var target = new PlainPoco();
            var entries = PropertyGridEntry.EnumerateFor(target);
            var readOnly = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ExplicitlyReadOnly));

            Assert.False(readOnly.TrySetValue(target, "new value"));
            Assert.Equal("locked", target.ExplicitlyReadOnly);
        }

        [Fact]
        public void EnumerateFor_RegisteredDependencyObjectProperty_IsIncludedAndNotDuplicated()
        {
            var target = new RegisteredTarget();
            var entries = PropertyGridEntry.EnumerateFor(target);

            var matches = entries.Where(e => e.Name == nameof(RegisteredTarget.RegisteredName)).ToList();
            Assert.Single(matches);
            Assert.Equal("Registered", matches[0].Category);

            Assert.True(matches[0].TrySetValue(target, "Hero"));
            Assert.Equal("Hero", target.RegisteredName);
        }

        [Fact]
        public void EnumerateFor_GroupsEntriesByFirstSeenCategoryOrder()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            var categories = entries.Select(e => e.Category).Distinct().ToList();

            int statsIndex = categories.IndexOf("Stats");
            int miscIndex = categories.IndexOf("Misc");
            Assert.True(statsIndex >= 0, "expected a 'Stats' group");
            Assert.True(miscIndex >= 0, "expected a 'Misc' group");

            // PlainPoco's first property (Name) is uncategorized, so "Misc" is seen before "Stats" (Health) - the
            // groups must come out in that order, not merely both be present.
            Assert.True(miscIndex < statsIndex, $"expected 'Misc' (first seen) before 'Stats' - got {string.Join(", ", categories)}");
        }

        [Fact]
        public void EnumerateFor_NonPublicSetterWithoutReadOnlyAttribute_IsMarkedReadOnly()
        {
            // Regression: the fallback was !CanWrite, which is false for a private setter - PropertyInfo.SetValue
            // writes straight through one by reflection, so the grid rendered an enabled editor that let a user
            // edit past the encapsulation the property's author chose.
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());

            var privatelySettable = Assert.Single(entries, e => e.Name == nameof(PlainPoco.PrivatelySettable));
            Assert.True(privatelySettable.IsReadOnly);
            Assert.False(privatelySettable.TrySetValue(new PlainPoco(), 42));
        }

        [Fact]
        public void EnumerateFor_InvertedRange_IsTreatedAsNoRange()
        {
            // A minimum above the maximum describes no values at all, and handing such a pair to a consumer is
            // actively harmful (Slider.Minimum/Maximum throw on min > max).
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());

            var inverted = Assert.Single(entries, e => e.Name == nameof(PlainPoco.InvertedRange));
            Assert.Null(inverted.Range);
        }

        [Fact]
        public void EnumerateFor_InheritedRegisteredProperty_IsListedExactlyOnce()
        {
            // Regression: the registered pass resolves a property off its DECLARING type (IPropertyReference.
            // OwnerType) while the reflected pass resolves it off the target's RUNTIME type - two distinct
            // PropertyInfo instances for one inherited property, which a HashSet<PropertyInfo>'s reference
            // equality never unified, so every inherited [RegisterReference] property was listed twice.
            var target = new DerivedRegisteredTarget();
            var entries = PropertyGridEntry.EnumerateFor(target);

            var matches = entries.Where(e => e.Name == nameof(BaseRegisteredTarget.InheritedRegisteredName)).ToList();
            Assert.Single(matches);
            Assert.Equal("Registered", matches[0].Category);
        }

        private class BaseRegisteredTarget : Icy.Data.Markup.DependencyObject
        {
            private string inheritedRegisteredName = string.Empty;

            [RegisterReference]
            [Category("Registered")]
            public string InheritedRegisteredName
            {
                get => inheritedRegisteredName;
                set => SetProperty(ref inheritedRegisteredName, value);
            }
        }

        private sealed class DerivedRegisteredTarget : BaseRegisteredTarget
        {
            private int derivedOnly;

            [RegisterReference]
            public int DerivedOnly
            {
                get => derivedOnly;
                set => SetProperty(ref derivedOnly, value);
            }
        }

        [Fact]
        public void EnumerateFor_RangeOnNonNumericProperty_DoesNotThrowAndReturnsNullRange()
        {
            var target = new PlainPoco();
            var entries = PropertyGridEntry.EnumerateFor(target);

            var rangeOnString = Assert.Single(entries, e => e.Name == nameof(PlainPoco.RangeOnString));
            Assert.Null(rangeOnString.Range);
        }

        [Fact]
        public void EnumerateFor_DuplicateDisplayNameDifferentProperties_DedupsOnPropertyInfoNotDisplayName()
        {
            var target = new WithDuplicateDisplayNames();
            var entries = PropertyGridEntry.EnumerateFor(target);

            var property1 = Assert.Single(entries, e => e.Name == nameof(WithDuplicateDisplayNames.Property1));
            var property2 = Assert.Single(entries, e => e.Name == nameof(WithDuplicateDisplayNames.Property2));

            Assert.Equal("Same Label", property1.DisplayName);
            Assert.Equal("Same Label", property2.DisplayName);
            Assert.NotEqual(property1.Name, property2.Name);
        }

        private sealed class WithDuplicateDisplayNames
        {
            [DisplayName("Same Label")]
            public string Property1 { get; set; } = "first";

            [DisplayName("Same Label")]
            public string Property2 { get; set; } = "second";
        }

        [Fact]
        public void EnumerateFor_ReadsExclusiveAndOpenEndedRanges()
        {
            var positive = Assert.Single(PropertyGridEntry.EnumerateFor(new PlainPoco()), e => e.Name == nameof(PlainPoco.Positive));

            Assert.Equal(new ValueRange(0, double.PositiveInfinity, true, false), positive.Range);
        }

        [Fact]
        public void EnumerateFor_AnEmptyRange_IsTreatedAsNoRange()
        {
            var empty = Assert.Single(PropertyGridEntry.EnumerateFor(new PlainPoco()), e => e.Name == nameof(PlainPoco.EmptyRange));

            Assert.Null(empty.Range);
        }

        [Fact]
        public void EnumerateFor_ReadsDefaultValue_ConvertedToThePropertyType()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            var converted = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ConvertedDefault));
            var health = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Health));

            Assert.True(converted.HasDefaultValue);
            Assert.Equal(40f, converted.DefaultValue);
            Assert.False(health.HasDefaultValue);
        }

        [Fact]
        public void EnumerateFor_ReadsDescription()
        {
            var sight = Assert.Single(PropertyGridEntry.EnumerateFor(new PlainPoco()), e => e.Name == nameof(PlainPoco.Sight));

            Assert.Equal("How far the hero can see.", sight.Description);
        }

        [Fact]
        public void EnumerateFor_HidesEditorBrowsableNever_ButShowsAdvanced()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());

            Assert.DoesNotContain(entries, e => e.Name == nameof(PlainPoco.NeverShown));
            Assert.Contains(entries, e => e.Name == nameof(PlainPoco.AdvancedShown));
        }

        [Fact]
        public void Validate_AcceptsTheDefault_AndRejectsOutOfRange()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            var limit = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Limit));
            var positive = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Positive));

            Assert.True(limit.Validate(float.NaN, out _));
            Assert.True(limit.Validate(0f, out _));
            Assert.False(limit.Validate(-1f, out string? reason));
            Assert.Equal("Must be at least 0.", reason);
            Assert.False(positive.Validate(0f, out reason));
            Assert.Equal("Must be greater than 0.", reason);
            Assert.False(positive.Validate(float.PositiveInfinity, out _));
        }
    }
}
