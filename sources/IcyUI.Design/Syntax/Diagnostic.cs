// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// Describes a problem in markup text, or a reason an edit couldn't be made.
    /// </summary>
    /// <param name="Span">The range of text the problem is about.</param>
    /// <param name="Message">A human-readable description of the problem.</param>
    public readonly record struct Diagnostic(TextSpan Span, string Message)
    {
        /// <inheritdoc/>
        public override string ToString() => $"{Span}: {Message}";
    }
}
