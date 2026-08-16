// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Linq.Expressions;

// Style<TTarget> can't share a file named Style.cs with the non-generic Style it derives from ('<'/'>' aren't
// valid in file names), so this file is named for the generic arity instead of the type name.
#pragma warning disable SA1649 // File name should match first type name

namespace Icy.UI.Styles
{
    /// <summary>
    /// A strongly-typed, fluent builder over <see cref="Style"/> for defining styles in C# code with
    /// compile-time-checked, IntelliSense-friendly property references instead of string-keyed setters.
    /// </summary>
    /// <remarks>
    /// <see cref="Set{TValue}(Expression{Func{TTarget, TValue}}, TValue)"/> resolves the target property's name and
    /// writes it into the same underlying <see cref="Style.Setters"/> dictionary the string-keyed API and markup
    /// use, so both authoring styles stay interchangeable and go through the exact same
    /// <see cref="Style.Apply(UIElement)"/> logic.
    /// </remarks>
    /// <typeparam name="TTarget">The type of element this style is meant to be applied to.</typeparam>
    public class Style<TTarget> : Style
        where TTarget : UIElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Style{TTarget}"/> class.
        /// </summary>
        public Style()
            : base(typeof(TTarget))
        {
        }

        /// <summary>
        /// Sets the value a property this style targets should have, and returns this style for further chaining.
        /// </summary>
        /// <typeparam name="TValue">The type of the property's value.</typeparam>
        /// <param name="property">A simple property-access expression identifying the property to set, e.g. <c>x => x.Background</c>.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>This style, for fluent chaining.</returns>
        public Style<TTarget> Set<TValue>(Expression<Func<TTarget, TValue>> property, TValue value)
        {
            Setters[StylePropertyExpression.GetPropertyName(property)] = value;
            return this;
        }

        /// <summary>
        /// Sets the style this style inherits setters and state groups from, and returns this style for further chaining.
        /// </summary>
        /// <param name="baseStyle">The style to inherit from.</param>
        /// <returns>This style, for fluent chaining.</returns>
        public Style<TTarget> InheritsFrom(Style baseStyle)
        {
            BasedOn = baseStyle;
            return this;
        }

        /// <summary>
        /// Adds a visual-state group this style registers on every element it's applied to, and returns this style for further chaining.
        /// </summary>
        /// <param name="group">The group to add.</param>
        /// <returns>This style, for fluent chaining.</returns>
        public Style<TTarget> WithStateGroup(VisualStateGroup group)
        {
            StateGroups.Add(group);
            return this;
        }
    }
}
