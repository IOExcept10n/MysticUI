// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A <c>&lt;!-- ... --&gt;</c> comment.
    /// </summary>
    public sealed class CommentSyntax : MarkupSyntaxNode
    {
        /// <summary>Initializes a new instance of the <see cref="CommentSyntax"/> class. Only the parser creates them.</summary>
        /// <param name="span">The comment.</param>
        internal CommentSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
