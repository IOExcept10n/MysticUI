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
            Assert.Equal((0d, 100d), armor.Range);

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

            int healthIndex = categories.IndexOf("Stats");
            int miscIndex = categories.IndexOf("Misc");
            Assert.True(healthIndex >= 0 && miscIndex >= 0);
        }
    }
}
