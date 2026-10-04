// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// Describes one replacement in a markup text: the characters in <see cref="Span"/> are replaced by
    /// <see cref="NewText"/>.
    /// </summary>
    /// <param name="Span">The range of the original text being replaced. A zero-length span is a pure insertion.</param>
    /// <param name="NewText">The replacement text. An empty string is a pure deletion.</param>
    public readonly record struct TextChange(TextSpan Span, string NewText);
}
