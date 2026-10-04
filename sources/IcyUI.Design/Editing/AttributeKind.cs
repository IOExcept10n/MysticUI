// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// How an attribute is mirrored onto a live object.
    /// </summary>
    internal enum AttributeKind
    {
        /// <summary>A property or attached property, applied through the loader.</summary>
        Property,

        /// <summary><c>x:Name</c>, renamed in place in the name scope.</summary>
        Name,

        /// <summary>Any other <c>x:</c> directive, which only takes effect when the element is built.</summary>
        Directive,

        /// <summary>An <c>xmlns</c> declaration, which edits never touch.</summary>
        NamespaceDeclaration,
    }
}
