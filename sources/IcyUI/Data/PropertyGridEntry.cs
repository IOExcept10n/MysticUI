// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Icy.Data.Markup;

namespace Icy.Data
{
    /// <summary>
    /// Describes a single property a <see cref="UI.Controls.PropertyGrid"/> row edits - unifying an
    /// <see cref="IPropertyReference"/>-registered property and a plain reflected <see cref="PropertyInfo"/>
    /// behind one shape, since <see cref="PropertyGrid.Target"/> may be either.
    /// </summary>
    /// <remarks>
    /// Metadata comes entirely from standard <see cref="System.ComponentModel"/>/
    /// <see cref="System.ComponentModel.DataAnnotations"/> attributes on the underlying property - see the
    /// design spec (<c>docs/superpowers/specs/2026-09-27-propertygrid-design.md</c>) for why no new IcyUI
    /// attribute type was introduced for this.
    /// </remarks>
    public sealed class PropertyGridEntry
    {
        private readonly PropertyInfo property;

        private PropertyGridEntry(PropertyInfo property)
        {
            this.property = property;
            DisplayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
            Category = property.GetCustomAttribute<CategoryAttribute>()?.Category ?? "Misc";
            IsReadOnly = property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? !property.CanWrite;
            Range = TryReadRange(property);
        }

        /// <summary>
        /// Gets the underlying property's name.
        /// </summary>
        public string Name => property.Name;

        /// <summary>
        /// Gets the label to show for this property - <see cref="DisplayNameAttribute"/>'s value, or
        /// <see cref="Name"/> when none is present.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the group this property is shown under - <see cref="CategoryAttribute"/>'s value, or
        /// <c>"Misc"</c> when none is present.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the underlying property's value type.
        /// </summary>
        public Type PropertyType => property.PropertyType;

        /// <summary>
        /// Gets a value indicating whether this property's editor should be disabled - either
        /// <see cref="ReadOnlyAttribute"/> says so explicitly, or the underlying property has no public setter.
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Gets the numeric range a <see cref="RangeAttribute"/> declares for this property, converted to
        /// <see langword="double"/>, or <see langword="null"/> when no valid range applies.
        /// </summary>
        public (double Min, double Max)? Range { get; }

        /// <summary>
        /// Reads this property's current value from <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The object to read the value from.</param>
        /// <returns>The property's current value.</returns>
        public object? GetValue(object target) => property.GetValue(target);

        /// <summary>
        /// Attempts to write <paramref name="value"/> to this property on <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The object to write the value to.</param>
        /// <param name="value">The value to assign.</param>
        /// <returns>
        /// <see langword="true"/> if the value was written; <see langword="false"/> when <see cref="IsReadOnly"/>
        /// is <see langword="true"/>, the property has no setter, or the assignment threw (an incompatible
        /// <paramref name="value"/>).
        /// </returns>
        public bool TrySetValue(object target, object? value)
        {
            if (IsReadOnly || !property.CanWrite)
                return false;

            try
            {
                property.SetValue(target, value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Enumerates every browsable property on <paramref name="target"/>'s runtime type - properties
        /// registered via <c>[RegisterReference]</c> first (deduplicated by the reflected member itself, not by
        /// name string), then every other public readable property found by plain reflection - grouped by
        /// <see cref="Category"/> in first-seen order.
        /// </summary>
        /// <param name="target">The object to enumerate properties for.</param>
        /// <returns>The resulting entries, grouped by <see cref="Category"/>.</returns>
        public static IReadOnlyList<PropertyGridEntry> EnumerateFor(object target)
        {
            ArgumentNullException.ThrowIfNull(target);

            Type type = target.GetType();
            var seen = new HashSet<PropertyInfo>();
            var entries = new List<PropertyGridEntry>();

            foreach (IPropertyReference reference in PropertyRegistry.For(target).GetPropertyStore(type).EnumerateProperties())
            {
                PropertyInfo? info = reference.OwnerType.GetProperty(reference.Name, BindingFlags.Public | BindingFlags.Instance);
                if (info == null || info.GetIndexParameters().Length > 0 || !seen.Add(info))
                    continue;
                if (TryCreate(info, out PropertyGridEntry? entry))
                    entries.Add(entry);
            }

            foreach (PropertyInfo info in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (info.GetIndexParameters().Length > 0 || !info.CanRead || !seen.Add(info))
                    continue;
                if (TryCreate(info, out PropertyGridEntry? entry))
                    entries.Add(entry);
            }

            return entries.GroupBy(e => e.Category).SelectMany(group => group).ToList();
        }

        private static bool TryCreate(PropertyInfo info, out PropertyGridEntry? entry)
        {
            entry = null;
            var browsable = info.GetCustomAttribute<BrowsableAttribute>();
            if (browsable != null && !browsable.Browsable)
                return false;

            entry = new PropertyGridEntry(info);
            return true;
        }

        private static (double Min, double Max)? TryReadRange(PropertyInfo property)
        {
            var range = property.GetCustomAttribute<RangeAttribute>();
            if (range == null)
                return null;

            try
            {
                return (Convert.ToDouble(range.Minimum), Convert.ToDouble(range.Maximum));
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                return null;
            }
        }
    }
}
