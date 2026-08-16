// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Linq.Expressions;

// VisualState<TTarget> can't share a file named VisualState.cs with the non-generic VisualState it derives from
// ('<'/'>' aren't valid in file names), so this file is named for the generic arity instead of the type name.
#pragma warning disable SA1649 // File name should match first type name

namespace Icy.UI.Styles
{
    /// <summary>
    /// A strongly-typed, fluent builder over <see cref="VisualState"/>, mirroring <see cref="Style{TTarget}"/>'s
    /// compile-time-checked property references for defining state setters in C# code.
    /// </summary>
    /// <typeparam name="TTarget">The type of element this state is meant to be applied to.</typeparam>
    public class VisualState<TTarget> : VisualState
        where TTarget : UIElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VisualState{TTarget}"/> class.
        /// </summary>
        /// <param name="name">The name of this state.</param>
        /// <param name="state">The combination of <see cref="ControlState"/> flags this state responds to.</param>
        public VisualState(string name, ControlState state)
            : base(name, state)
        {
        }

        /// <summary>
        /// Sets the value a property this state should have while active, and returns this state for further chaining.
        /// </summary>
        /// <typeparam name="TValue">The type of the property's value.</typeparam>
        /// <param name="property">A simple property-access expression identifying the property to set, e.g. <c>x => x.Background</c>.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>This state, for fluent chaining.</returns>
        public VisualState<TTarget> Set<TValue>(Expression<Func<TTarget, TValue>> property, TValue value)
        {
            Setters[StylePropertyExpression.GetPropertyName(property)] = value;
            return this;
        }
    }
}
