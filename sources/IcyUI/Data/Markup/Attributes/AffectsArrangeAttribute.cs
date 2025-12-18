// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup.Attributes
{
    /// <summary>
    /// Represents the attribute for the properties that affect element arrange when updated.
    /// </summary>
    /// <remarks>
    /// This attribute is just an annotation for properties that can invalidate element arrange as a side-effect.
    /// It <b>doesn't</b> make properties call <see cref="UI.UIElement.InvalidateArrange"/> or similar methods automatically.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class AffectsArrangeAttribute : Attribute
    {
    }
}