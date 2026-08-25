// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;

namespace Icy.Markup
{
    /// <summary>
    /// Designates the property a markup extension's single positional argument sets, letting
    /// <c>{Binding Path=Name}</c> be written as the shorter <c>{Binding Name}</c>.
    /// </summary>
    /// <param name="name">The name of the property the positional argument is written into.</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class MarkupExtensionDefaultPropertyAttribute(string name) : Attribute
    {
        /// <summary>
        /// Gets the name of the property the positional argument is written into.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the default property's name declared by <paramref name="type"/> or inherited from one of its base
        /// types.
        /// </summary>
        /// <param name="type">The extension type to look the default property up for.</param>
        /// <returns>The property's name, or <see langword="null"/> when the type declares none.</returns>
        public static string? GetDefaultPropertyName(Type type) =>
            type.GetCustomAttribute<MarkupExtensionDefaultPropertyAttribute>(inherit: true)?.Name;
    }
}
