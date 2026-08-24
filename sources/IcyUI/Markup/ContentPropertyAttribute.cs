// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;

namespace Icy.Markup
{
    /// <summary>
    /// Designates the property that a type's markup child elements and bare text are written into, letting markup
    /// omit the property name entirely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <c>[ContentProperty(nameof(Panel.Children))]</c> on <see cref="UI.Panel"/>, this markup:
    /// </para>
    /// <code language="xml">
    /// &lt;StackPanel&gt;
    ///   &lt;TextBlock&gt;Hello&lt;/TextBlock&gt;
    /// &lt;/StackPanel&gt;
    /// </code>
    /// <para>
    /// adds the <see cref="UI.Controls.TextBlock"/> to <c>Children</c> without naming it. The content property's
    /// kind decides what happens: an <see cref="System.Collections.IList"/> has children <em>added</em> to it, and
    /// anything else is <em>assigned</em> a single child - a second child is then an error.
    /// </para>
    /// <para>
    /// The attribute is inherited, so a type only declares its own when it differs from its base's.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the property markup content is written into.</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class ContentPropertyAttribute(string name) : Attribute
    {
        /// <summary>
        /// Gets the name of the property markup content is written into.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the name of the content property declared by <paramref name="type"/> or inherited from one of its
        /// base types.
        /// </summary>
        /// <param name="type">The type to look the content property up for.</param>
        /// <returns>The content property's name, or <see langword="null"/> when the type declares none.</returns>
        public static string? GetContentPropertyName(Type type) =>
            type.GetCustomAttribute<ContentPropertyAttribute>(inherit: true)?.Name;
    }
}
