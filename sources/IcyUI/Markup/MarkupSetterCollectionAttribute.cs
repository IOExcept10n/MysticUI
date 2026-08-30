// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;

namespace Icy.Markup
{
    /// <summary>
    /// Designates the property that holds a type's markup attributes that don't resolve to a real CLR member -
    /// each becomes an entry in the named <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>
    /// instead of raising an "unknown property" error.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="Icy.UI.Styles.Style"/> and <see cref="Icy.UI.Styles.VisualState"/>: an attribute like
    /// <c>Background="Blue"</c> on <c>&lt;Style TargetType="Button"&gt;</c> isn't a property of <c>Style</c>
    /// itself - it's a setter to apply to a <c>Button</c> once the style is applied. The value is converted
    /// against the nearest enclosing <c>&lt;Style&gt;</c>'s <c>TargetType</c> (see
    /// <see cref="MarkupLoadContext.SetterTargetType"/>) rather than against the marked type's own properties.
    /// </para>
    /// <para>
    /// Mirrors <see cref="ContentPropertyAttribute"/>'s shape and inheritance rule.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the dictionary property attribute overflow is written into.</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class MarkupSetterCollectionAttribute(string name) : Attribute
    {
        /// <summary>
        /// Gets the name of the dictionary property attribute overflow is written into.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the setter-collection property name declared by <paramref name="type"/> or inherited from one of
        /// its base types.
        /// </summary>
        /// <param name="type">The type to look the setter-collection property up for.</param>
        /// <returns>The property's name, or <see langword="null"/> when the type declares none.</returns>
        public static string? GetSetterCollectionName(Type type) =>
            type.GetCustomAttribute<MarkupSetterCollectionAttribute>(inherit: true)?.Name;
    }
}
