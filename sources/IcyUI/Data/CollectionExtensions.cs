// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;

namespace Icy.Data
{
    /// <summary>
    /// Provides extension methods for working with collections.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Projects the elements of an <see cref="IReadOnlyCollection{TSource}"/> into a new
        /// <see cref="IReadOnlyCollection{TTarget}"/> using a specified projection function.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements in the source collection.</typeparam>
        /// <typeparam name="TTarget">The type of the elements in the target collection.</typeparam>
        /// <param name="values">The source collection of elements to be projected.</param>
        /// <param name="projection">A function that defines how to project each element of type <typeparamref name="TSource"/>
        /// into an element of type <typeparamref name="TTarget"/>.</param>
        /// <returns>A new <see cref="IReadOnlyCollection{TTarget}"/> containing the projected elements.</returns>
        public static IReadOnlyCollection<TTarget> AsCollection<TSource, TTarget>(this IReadOnlyCollection<TSource> values, Func<TSource, TTarget> projection) =>
            new CollectionWrapper<TSource, TTarget>(values, projection);

        private class CollectionWrapper<TSource, TTarget> : IReadOnlyCollection<TTarget>
        {
            private readonly IReadOnlyCollection<TSource> sourceValues;
            private readonly Func<TSource, TTarget> projection;

            public CollectionWrapper(IReadOnlyCollection<TSource> sourceValues, Func<TSource, TTarget> projection)
            {
                this.sourceValues = sourceValues;
                this.projection = projection;
            }

            public int Count => sourceValues.Count;

            public IEnumerator<TTarget> GetEnumerator()
            {
                foreach (var item in sourceValues)
                    yield return projection(item);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                foreach (var item in sourceValues)
                    yield return projection(item);
            }
        }
    }
}