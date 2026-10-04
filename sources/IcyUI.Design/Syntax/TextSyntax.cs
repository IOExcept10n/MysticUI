// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A run of character data: element text, indentation whitespace, or characters the parser couldn't place
    /// anywhere else (reported with a <see cref="Diagnostic"/>).
    /// </summary>
    public sealed class TextSyntax : MarkupSyntaxNode
    {
        /// <summary>Initializes a new instance of the <see cref="TextSyntax"/> class. Only the parser creates them.</summary>
        /// <param name="span">The text.</param>
        internal TextSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
