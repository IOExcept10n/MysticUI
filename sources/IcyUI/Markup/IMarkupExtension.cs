// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Computes a value for a markup attribute or element written as <c>{Name ...}</c>, instead of the loader
    /// converting the text directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An extension is a plain object, constructed through <see cref="MarkupConfiguration.Activator"/> like any
    /// other markup type, with its properties set from the arguments written inside the braces - so
    /// <c>{Binding Path=Name, Mode=TwoWay}</c> constructs a <see cref="Extensions.BindingExtension"/> and sets its
    /// <c>Path</c> and <c>Mode</c> properties before calling <see cref="ProvideValue"/>. Register one under a name
    /// with <see cref="MarkupConfiguration.RegisterExtension{T}"/>.
    /// </para>
    /// <para>
    /// The single positional argument some extensions accept (<c>{Binding Name}</c> rather than
    /// <c>{Binding Path=Name}</c>) is declared with <see cref="MarkupExtensionDefaultPropertyAttribute"/>.
    /// </para>
    /// </remarks>
    public interface IMarkupExtension
    {
        /// <summary>
        /// Computes the value the loader should use.
        /// </summary>
        /// <param name="context">The property being resolved, and what the loader knows about the document.</param>
        /// <returns>
        /// The value to assign, converted the same way an ordinary attribute value would be; or
        /// <see cref="MarkupValue.Unset"/> when the extension already applied its own effect (typically by binding
        /// the property itself) and nothing further should be assigned.
        /// </returns>
        object? ProvideValue(MarkupExtensionContext context);
    }
}
