// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.Markup;

namespace Icy.Design.Tracking
{
    /// <summary>
    /// What one markup attribute did to one live object: the member it set, and the binding it attached, if any.
    /// </summary>
    internal sealed record AppliedMember(MarkupMember Member, IBinding? Binding);
}
