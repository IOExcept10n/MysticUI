using CommunityToolkit.Diagnostics;
using System.Linq.Expressions;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents the property path whose value is compiled for the faster execution.
    /// </summary>
    public sealed class CompiledPropertyPath : PropertyPath
    {
        private readonly Func<object, object?> getter;
        private readonly Action<object, object?>? setter;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompiledPropertyPath"/> class.
        /// </summary>
        /// <param name="path">The path to access an object.</param>
        /// <param name="typeContext">The type to get info about properties.</param>
        /// <param name="converter">An instance of the type converter to parse path parts.</param>
        public CompiledPropertyPath(string path, Type typeContext, ITypeConverter converter)
            : base(path, typeContext, converter)
        {
            var parameter = Expression.Parameter(typeof(object));
            Type setType = null!;
            Expression callChain = typeContext.IsValueType ? Expression.Unbox(parameter, typeContext) : Expression.TypeAs(parameter, typeContext);
            for (int i = 0; i < PathSegments.Count; i++)
            {
                PathSegment segment = PathSegments[i];
                if (i == PathSegments.Count - 1)
                {
                    setType = segment.ResultType;
                }

                callChain = GetAccessor(segment, callChain);
            }

            bool isReadOnly = PathSegments[^1].IsReadOnly;

            Expression cast = Expression.TypeAs(callChain, typeof(object));
            getter = Expression.Lambda<Func<object, object?>>(cast, parameter).Compile();
            if (!isReadOnly)
            {
                ParameterExpression setParameter = Expression.Parameter(typeof(object));
                Expression uncast = setType.IsValueType ? Expression.Unbox(setParameter, setType) : Expression.TypeAs(setParameter, setType);
                Expression set = Expression.Assign(callChain, uncast);
                setter = Expression.Lambda<Action<object, object?>>(set, parameter, setParameter).Compile();
            }
        }

        /// <inheritdoc/>
        public override object? GetValue(object source)
        {
            return getter(source);
        }

        /// <inheritdoc/>
        public override void SetValue(object target, object? value)
        {
            if (setter == null)
                ThrowHelper.ThrowInvalidOperationException("Can't assign value to the readonly path segment.");
            setter(target, value);
        }

        private static Expression GetAccessor(PathSegment segment, Expression argument)
        {
            if (segment.IsArray)
            {
                return Expression.ArrayAccess(argument, segment.Params!.Select(Expression.Constant));
            }
            else if (segment.Params != null)
            {
                return Expression.MakeIndex(argument, segment.Property, segment.Params.Select(Expression.Constant));
            }

            return Expression.Property(argument, segment.Property);
        }
    }
}
