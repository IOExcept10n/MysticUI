using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icy.Data
{
    public static class CollectionExtensions
    {
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
