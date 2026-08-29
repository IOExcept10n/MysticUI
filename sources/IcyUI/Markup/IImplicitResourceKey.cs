// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Lets an object populated into a keyless dictionary entry (no <c>x:Key</c>) supply its own resource key,
    /// instead of the loader requiring one.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="UI.Styles.Style"/> so a keyless <c>&lt;Style TargetType="Button"&gt;</c>
    /// registers as an implicit, type-targeted style (see <see cref="UI.ResourceDictionary.GetImplicitStyleKey"/>)
    /// instead of requiring an explicit <c>x:Key</c>.
    /// </remarks>
    public interface IImplicitResourceKey
    {
        /// <summary>
        /// Gets the key this object should register under when added to a dictionary with no explicit
        /// <c>x:Key</c>, or <see langword="null"/> if it has none.
        /// </summary>
        string? ImplicitResourceKey { get; }
    }
}
