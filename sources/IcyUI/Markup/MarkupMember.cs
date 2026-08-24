// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;
using Icy.Data.Markup;

namespace Icy.Markup
{
    /// <summary>
    /// A property the loader can read or write, resolved either through the property registry or through plain
    /// reflection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Markup needs both paths. A registered property (<see cref="Data.Markup.Attributes.RegisterReferenceAttribute"/>)
    /// is preferred, because writing through its <see cref="IPropertyReference"/> puts the value into the same
    /// precedence system styles, visual states, and animations use - a markup-assigned value is a local one and
    /// outranks all of them, exactly as a code-assigned value does.
    /// </para>
    /// <para>
    /// But the registry only ever contains <em>writable</em> properties of types that opted in, and markup has to
    /// reach two kinds of property it therefore never sees: read-only collections that content and property
    /// elements populate (<see cref="UI.Panel.Children"/>, <see cref="UI.Controls.Grid.ColumnDefinitions"/>), and
    /// plain data objects that are not <see cref="DependencyObject"/>s at all
    /// (<see cref="UI.Controls.ColumnDefinition.Width"/>). Those resolve to a <see cref="PropertyInfo"/> instead.
    /// </para>
    /// </remarks>
    public sealed class MarkupMember
    {
        private readonly IPropertyReference? reference;
        private readonly PropertyInfo? property;

        private MarkupMember(string name, Type propertyType, IPropertyReference? reference, PropertyInfo? property)
        {
            Name = name;
            PropertyType = propertyType;
            this.reference = reference;
            this.property = property;
        }

        /// <summary>
        /// Gets the property's name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the property's declared type.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// Gets a value indicating whether this property can be assigned to.
        /// </summary>
        /// <remarks>
        /// A read-only collection property is readable but not settable - it is populated by adding to the
        /// collection it already holds.
        /// </remarks>
        public bool CanSet => reference != null || property?.CanWrite == true;

        /// <summary>
        /// Gets a value indicating whether writes go through the property registry rather than plain reflection.
        /// </summary>
        public bool IsRegistered => reference != null;

        /// <summary>
        /// Resolves a property on <paramref name="targetType"/>, preferring a registered one.
        /// </summary>
        /// <param name="targetType">The type declaring the property.</param>
        /// <param name="name">The property's name.</param>
        /// <param name="registry">The registry to resolve registered properties through.</param>
        /// <returns>The resolved property, or <see langword="null"/> when the type has no such public property.</returns>
        public static MarkupMember? Resolve(Type targetType, string name, PropertyRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(targetType);
            ArgumentNullException.ThrowIfNull(registry);

            if (registry.GetPropertyStore(targetType).TryGetProperty(name, searchInherited: true, out IPropertyReference? found))
                return new MarkupMember(name, found.PropertyType, found, null);

            PropertyInfo? info = targetType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return info == null ? null : new MarkupMember(name, info.PropertyType, null, info);
        }

        /// <summary>
        /// Enumerates the names markup could have meant on <paramref name="targetType"/>, for error suggestions.
        /// </summary>
        /// <param name="targetType">The type whose properties should be listed.</param>
        /// <param name="registry">The registry to enumerate registered properties from.</param>
        /// <returns>Every settable public property name, registered or not.</returns>
        public static IEnumerable<string> EnumerateNames(Type targetType, PropertyRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(targetType);
            ArgumentNullException.ThrowIfNull(registry);

            foreach (IPropertyReference property in registry.GetPropertyStore(targetType).EnumerateProperties())
            {
                yield return property.Name;
            }

            foreach (PropertyInfo info in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return info.Name;
            }
        }

        /// <summary>
        /// Reads the property's current value from <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The object to read from.</param>
        /// <returns>The current value.</returns>
        public object? GetValue(object target) => reference != null ? reference.GetRawValue(target) : property!.GetValue(target);

        /// <summary>
        /// Assigns <paramref name="value"/> to the property on <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The object to assign to.</param>
        /// <param name="value">The value to assign.</param>
        /// <exception cref="InvalidOperationException">The property has no setter.</exception>
        public void SetValue(object target, object? value)
        {
            if (reference != null)
            {
                reference.SetRawValue(target, value);
                return;
            }

            if (property?.CanWrite != true)
                throw new InvalidOperationException($"Property '{Name}' is read-only.");

            property.SetValue(target, value);
        }
    }
}
