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
    /// behind one shape, since <see cref="UI.Controls.PropertyGrid.Target"/> may be either.
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

            // A non-public setter must read as read-only: PropertyInfo.CanWrite is true for one, and
            // PropertyInfo.SetValue happily writes through it by reflection, so falling back to CanWrite alone
            // would render an enabled editor for a `{ get; private set; }` property and let a user edit straight
            // through the encapsulation its author deliberately chose.
            IsReadOnly = property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? (property.SetMethod?.IsPublic != true);
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
        /// <remarks>
        /// "No valid range" covers a property with no <see cref="RangeAttribute"/> at all, a non-numeric property
        /// carrying one, bounds that don't convert to <see langword="double"/>, and an inverted range whose minimum
        /// exceeds its maximum - the last of which describes no values at all (<see cref="RangeAttribute"/> itself
        /// rejects it when it validates), so a consumer can rely on
        /// <c><see cref="Range"/>.Value.Min &lt;= <see cref="Range"/>.Value.Max</c>.
        /// </remarks>
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
        /// registered via <c>[RegisterReference]</c> first, then every other public readable property found by
        /// plain reflection - grouped by <see cref="Category"/> in first-seen order.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two passes are deduplicated against each other by the reflected member itself - specifically by the
        /// <c>(<see cref="MemberInfo.DeclaringType"/>, <see cref="MemberInfo.Name"/>)</c> pair - rather than by the
        /// display string, so two distinct properties sharing one <see cref="DisplayNameAttribute"/> both survive,
        /// while a property found by both passes is emitted once.
        /// </para>
        /// <para>
        /// The <see cref="PropertyInfo"/> instance itself can't serve as that key: the registered pass resolves a
        /// property through <see cref="IPropertyReference.OwnerType"/> (the property's <i>declaring</i> type),
        /// while the reflected pass resolves it off <paramref name="target"/>'s <i>runtime</i> type. For a
        /// registered property declared on a base class and merely inherited by a derived one, those are two
        /// different <see cref="PropertyInfo"/> objects (differing in <see cref="MemberInfo.ReflectedType"/>) that
        /// reference equality - all <see cref="HashSet{T}"/> has for <see cref="PropertyInfo"/> - never unifies, so
        /// every such property would be listed twice. Keying on
        /// <see cref="MemberInfo.DeclaringType"/>/<see cref="MemberInfo.Name"/> unifies them while still keeping a
        /// derived property that shadows a base one via <see langword="new"/> distinct, since the two differ in
        /// <see cref="MemberInfo.DeclaringType"/>.
        /// </para>
        /// </remarks>
        /// <param name="target">The object to enumerate properties for.</param>
        /// <returns>The resulting entries, grouped by <see cref="Category"/>.</returns>
        public static IReadOnlyList<PropertyGridEntry> EnumerateFor(object target)
        {
            ArgumentNullException.ThrowIfNull(target);

            Type type = target.GetType();
            var seen = new HashSet<(Type? DeclaringType, string Name)>();
            var entries = new List<PropertyGridEntry>();

            foreach (IPropertyReference reference in PropertyRegistry.For(target).GetPropertyStore(type).EnumerateProperties())
            {
                PropertyInfo? info = reference.OwnerType.GetProperty(reference.Name, BindingFlags.Public | BindingFlags.Instance);
                if (info == null || info.GetIndexParameters().Length > 0 || !seen.Add((info.DeclaringType, info.Name)))
                    continue;
                if (TryCreate(info, out PropertyGridEntry? entry))
                    entries.Add(entry);
            }

            foreach (PropertyInfo info in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (info.GetIndexParameters().Length > 0 || !info.CanRead || !seen.Add((info.DeclaringType, info.Name)))
                    continue;
                if (TryCreate(info, out PropertyGridEntry? entry))
                    entries.Add(entry);
            }

            return entries.GroupBy(e => e.Category).SelectMany(group => group).ToList();
        }

        /// <summary>
        /// Attempts to build an entry for <paramref name="info"/>, rejecting a property excluded by
        /// <see cref="BrowsableAttribute"/>.
        /// </summary>
        /// <param name="info">The property to build an entry for.</param>
        /// <param name="entry">The created entry, when this method returns <see langword="true"/>.</param>
        /// <returns>
        /// <see langword="true"/> when an entry was created; <see langword="false"/> when
        /// <paramref name="info"/> carries <c>[Browsable(false)]</c>.
        /// </returns>
        private static bool TryCreate(PropertyInfo info, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PropertyGridEntry? entry)
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

            // Only apply Range to numeric types
            if (!IsNumericType(property.PropertyType))
                return null;

            try
            {
                double min = Convert.ToDouble(range.Minimum);
                double max = Convert.ToDouble(range.Maximum);

                // An inverted range describes no values at all - treat it as no range rather than handing a
                // consumer bounds it can't satisfy (Slider.Minimum/Maximum, for one, throw on min > max).
                return min <= max ? (min, max) : null;
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                return null;
            }
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }
    }
}
