using System.Reflection;
using Icy.Data;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class RangeAnnotationTests
    {
        public static TheoryData<Type, string> Guarded => new()
        {
            { typeof(UIElement), nameof(UIElement.Width) },
            { typeof(UIElement), nameof(UIElement.Height) },
            { typeof(UIElement), nameof(UIElement.MinWidth) },
            { typeof(UIElement), nameof(UIElement.MinHeight) },
            { typeof(UIElement), nameof(UIElement.MaxWidth) },
            { typeof(UIElement), nameof(UIElement.MaxHeight) },
            { typeof(UIElement), nameof(UIElement.Opacity) },
            { typeof(ItemsControl), nameof(ItemsControl.DefaultEstimatedItemHeight) },
            { typeof(ComboBox), nameof(Selector.MaxDropDownHeight) },
            { typeof(WrapGrid), nameof(WrapGrid.ItemWidth) },
            { typeof(WrapGrid), nameof(WrapGrid.ItemHeight) },
            { typeof(TreeView), nameof(TreeView.Indent) },
            { typeof(ListBox), nameof(SelectingItemsControl.SelectedIndex) },
        };

        [Theory]
        [MemberData(nameof(Guarded))]
        public void EveryGuardedProperty_DeclaresItsRule(Type type, string name)
        {
            PropertyGridEntry entry = Entry(Activator.CreateInstance(type)!, name);

            Assert.NotNull(entry.Range);
        }

        [Fact]
        public void EveryRangeInCore_AgreesWithItsSetter()
        {
            // For each public, constructible UIElement type and each of its properties with a [Range]:
            // - the default and values just inside each finite bound are accepted;
            // - a value just outside either throws or is stored back inside the range (a rotation normalises).
            var failures = new List<string>();
            foreach (Type type in typeof(UIElement).Assembly.GetExportedTypes()
                .Where(t => typeof(UIElement).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null))
            {
                foreach (PropertyGridEntry entry in PropertyGridEntry.EnumerateFor(Activator.CreateInstance(type)!))
                {
                    if (entry.Range is not { } range || entry.IsReadOnly)
                        continue;

                    foreach (double inside in InsideValues(range, entry.PropertyType))
                    {
                        if (!TrySet(type, entry, inside, out _))
                            failures.Add($"{type.Name}.{entry.Name} rejected {inside}, inside {range.Describe()}");
                    }

                    if (entry.HasDefaultValue && !TrySetObject(type, entry, entry.DefaultValue))
                        failures.Add($"{type.Name}.{entry.Name} rejected its default {entry.DefaultValue}");

                    foreach (double outside in OutsideValues(range, entry.PropertyType))
                    {
                        if (TrySet(type, entry, outside, out object? stored) && !range.Contains(Convert.ToDouble(stored)))
                            failures.Add($"{type.Name}.{entry.Name} stored {stored}, outside {range.Describe()}");
                    }
                }
            }

            Assert.Empty(failures);
        }

        private static PropertyGridEntry Entry(object target, string name) =>
            PropertyGridEntry.EnumerateFor(target).Single(e => e.Name == name);

        private static double Step(Type type) => type == typeof(float) || type == typeof(double) ? 0.5 : 1;

        private static IEnumerable<double> InsideValues(ValueRange range, Type type)
        {
            if (double.IsFinite(range.Min))
                yield return range.MinIsExclusive ? range.Min + Step(type) : range.Min;
            if (double.IsFinite(range.Max))
                yield return range.MaxIsExclusive ? range.Max - Step(type) : range.Max;
        }

        private static IEnumerable<double> OutsideValues(ValueRange range, Type type)
        {
            if (double.IsFinite(range.Min))
                yield return range.MinIsExclusive ? range.Min : range.Min - Step(type);
            if (double.IsFinite(range.Max))
                yield return range.MaxIsExclusive ? range.Max : range.Max + Step(type);
        }

        private static bool TrySet(Type type, PropertyGridEntry entry, double value, out object? stored)
        {
            stored = null;
            object target = Activator.CreateInstance(type)!;
            PropertyInfo property = type.GetProperty(entry.Name)!;
            try
            {
                property.SetValue(target, Convert.ChangeType(value, entry.PropertyType, System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (TargetInvocationException)
            {
                return false;
            }

            stored = property.GetValue(target);
            return true;
        }

        private static bool TrySetObject(Type type, PropertyGridEntry entry, object? value)
        {
            object target = Activator.CreateInstance(type)!;
            try
            {
                type.GetProperty(entry.Name)!.SetValue(target, value);
                return true;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }
    }
}
