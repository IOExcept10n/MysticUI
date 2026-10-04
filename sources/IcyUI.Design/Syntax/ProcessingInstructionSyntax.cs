// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A <c>&lt;? ... ?&gt;</c> processing instruction, or an unsupported <c>&lt;!DOCTYPE ...&gt;</c> declaration
    /// kept verbatim.
    /// </summary>
    public sealed class ProcessingInstructionSyntax : MarkupSyntaxNode
    {
        /// <summary>Initializes a new instance of the <see cref="ProcessingInstructionSyntax"/> class. Only the parser creates them.</summary>
        /// <param name="span">The instruction.</param>
        internal ProcessingInstructionSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
