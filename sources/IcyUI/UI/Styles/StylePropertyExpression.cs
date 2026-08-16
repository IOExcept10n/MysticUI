// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Icy.UI.Styles
{
    /// <summary>
    /// Resolves a property-access expression (e.g. <c>x => x.Background</c>) to the property name it targets, for
    /// the fluent <see cref="Style{TTarget}"/>/<see cref="VisualState{TTarget}"/> builder APIs.
    /// </summary>
    internal static class StylePropertyExpression
    {
        /// <summary>
        /// Gets the name of the property a simple member-access expression targets.
        /// </summary>
        /// <typeparam name="TTarget">The type the property is declared on.</typeparam>
        /// <typeparam name="TValue">The type of the property's value.</typeparam>
        /// <param name="expression">A simple property-access expression, e.g. <c>x => x.Background</c>.</param>
        /// <returns>The name of the accessed property.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="expression"/> isn't a simple property access (e.g. it involves a method
        /// call, computation, or field access).
        /// </exception>
        public static string GetPropertyName<TTarget, TValue>(Expression<Func<TTarget, TValue>> expression)
        {
            if (expression.Body is MemberExpression { Member: PropertyInfo property })
                return property.Name;

            return ThrowHelper.ThrowArgumentException<string>(
                nameof(expression),
                $"Expected a simple property access (e.g. 'x => x.PropertyName'), got '{expression.Body}'.");
        }
    }
}
