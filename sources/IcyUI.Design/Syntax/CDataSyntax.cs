// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A <c>&lt;![CDATA[ ... ]]&gt;</c> section.
    /// </summary>
    public sealed class CDataSyntax : MarkupSyntaxNode
    {
        /// <summary>Initializes a new instance of the <see cref="CDataSyntax"/> class. Only the parser creates them.</summary>
        /// <param name="span">The section.</param>
        internal CDataSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
