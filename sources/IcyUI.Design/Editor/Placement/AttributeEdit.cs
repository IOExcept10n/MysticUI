// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// One attribute a placement writes or removes.
    /// </summary>
    /// <param name="Name">The attribute name as written in markup, such as <c>Width</c> or <c>Grid.Row</c>.</param>
    /// <param name="Value">The value to write, or <see langword="null"/> to remove the attribute.</param>
    public readonly record struct AttributeEdit(string Name, string? Value);
}
