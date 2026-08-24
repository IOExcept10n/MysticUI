// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Markup
{
    /// <summary>
    /// Turns the bare text inside an element into a value its content property can actually hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Markup lets an element carry text directly:
    /// </para>
    /// <code language="xml">
    /// &lt;Button&gt;Click Me&lt;/Button&gt;
    /// </code>
    /// <para>
    /// <see cref="UI.Controls.ContentControl.Content"/> is a <see cref="UIElement"/>, not a
    /// <see cref="string"/>, so the text can't simply be converted - something has to decide what element
    /// represents it. That's what an adapter does; the built-in <see cref="UIElementTextAdapter"/> wraps the text in
    /// a <see cref="TextBlock"/>.
    /// </para>
    /// <para>
    /// Adapters are consulted only when the content property's type is not assignable from <see cref="string"/>,
    /// and only in <see cref="MarkupConfiguration.TextAdapters"/> order, first match winning - so a registration
    /// added later can take precedence over the built-in one by being inserted ahead of it.
    /// </para>
    /// </remarks>
    public interface IMarkupTextAdapter
    {
        /// <summary>
        /// Determines whether this adapter can produce a value assignable to <paramref name="targetType"/>.
        /// </summary>
        /// <param name="targetType">The type of the content property the text is being written into.</param>
        /// <returns><see langword="true"/> when <see cref="Adapt"/> can handle this type.</returns>
        bool CanAdapt(Type targetType);

        /// <summary>
        /// Produces the value to assign to a content property of <paramref name="targetType"/> for
        /// <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The element's text, already trimmed.</param>
        /// <param name="targetType">The type of the content property the text is being written into.</param>
        /// <returns>A value assignable to <paramref name="targetType"/>.</returns>
        object? Adapt(string text, Type targetType);
    }

    /// <summary>
    /// The built-in adapter for <see cref="UIElement"/>-typed content properties: wraps the text in a
    /// <see cref="TextBlock"/>.
    /// </summary>
    /// <remarks>
    /// The resulting <see cref="TextBlock"/> has no <see cref="TextBlock.FontFamily"/> of its own, and relies on
    /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> or
    /// <see cref="Icy.Rendering.Fonts.FontSystem.FallbackFont"/> to resolve one - configure at least one of those,
    /// or bare text renders as nothing.
    /// </remarks>
    public sealed class UIElementTextAdapter : IMarkupTextAdapter
    {
        /// <inheritdoc/>
        public bool CanAdapt(Type targetType) => targetType.IsAssignableFrom(typeof(TextBlock));

        /// <inheritdoc/>
        public object? Adapt(string text, Type targetType) => new TextBlock { Text = text };
    }
}
