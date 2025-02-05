// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup.Attributes
{
    /// <summary>
    /// Represents an attribute for the properties that cannot be animated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NonAnimatableAttribute : Attribute
    {
    }
}
