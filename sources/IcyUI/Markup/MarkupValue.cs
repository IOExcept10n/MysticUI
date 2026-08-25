// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Sentinel values a markup extension can return from <see cref="IMarkupExtension.ProvideValue"/>.
    /// </summary>
    public static class MarkupValue
    {
        /// <summary>
        /// Signals that the extension already applied its effect and the loader should not assign anything to the
        /// property it was resolving a value for.
        /// </summary>
        /// <remarks>
        /// <see cref="Extensions.BindingExtension"/> is the reason this exists: it binds the property itself (so
        /// the value can keep updating as the source changes) rather than producing a single value for the loader
        /// to assign once. A plain <see langword="null"/> return can't say this - it is also a legitimate value a
        /// future extension might want to actually assign, so it can't double as "I already handled it" without
        /// creating exactly the ambiguity this type exists to avoid.
        /// </remarks>
        public static readonly object Unset = new UnsetValue();

        private sealed class UnsetValue
        {
            public override string ToString() => "{Unset}";
        }
    }
}
