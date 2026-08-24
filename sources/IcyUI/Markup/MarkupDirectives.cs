// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// The names of the <c>x:</c> directives - attributes that instruct the loader rather than set a property.
    /// </summary>
    public static class MarkupDirectives
    {
        /// <summary>
        /// <c>x:Name</c> - names an element, setting <see cref="UI.UIElement.Name"/> and registering it in the
        /// document's <see cref="MarkupNameScope"/>.
        /// </summary>
        public const string Name = "Name";

        /// <summary>
        /// <c>x:Class</c> - names the type to instantiate in place of the root tag's own type.
        /// </summary>
        public const string Class = "Class";

        /// <summary>
        /// <c>x:Key</c> - the key an element is added under, when it is an entry of a dictionary-valued property.
        /// </summary>
        public const string Key = "Key";

        /// <summary>
        /// <c>x:DataType</c> - declares the type a subtree's data context is expected to have.
        /// </summary>
        /// <remarks>
        /// Resolved at load so a misspelling is caught, but with no runtime effect: it exists for tooling and the
        /// future source generator to validate binding paths against.
        /// </remarks>
        public const string DataType = "DataType";

        /// <summary>
        /// Gets every directive name, for error messages that suggest a near match.
        /// </summary>
        public static IReadOnlyList<string> All { get; } = [Name, Class, Key, DataType];

        /// <summary>
        /// Writes a directive name the way it appears in markup.
        /// </summary>
        /// <param name="name">The directive's local name.</param>
        /// <returns>The name with the conventional <c>x:</c> prefix.</returns>
        public static string Qualified(string name) => $"x:{name}";
    }
}
