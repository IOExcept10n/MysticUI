// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup.Attributes
{
    /// <summary>
    /// Represents the attribute for the properties that affect element transform when updated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class AffectsTransformAttribute : Attribute
    {
    }
}