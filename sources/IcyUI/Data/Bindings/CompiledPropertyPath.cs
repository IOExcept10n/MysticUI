// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents the property path whose value is compiled for the faster execution.
    /// </summary>
    public sealed class CompiledPropertyPath : PropertyPath
    {
        private static readonly MethodInfo GetRawValueMethod = typeof(IPropertyReference).GetMethod(nameof(IPropertyReference.GetRawValue))!;
        private static readonly MethodInfo SetRawValueMethod = typeof(IPropertyReference).GetMethod(nameof(IPropertyReference.SetRawValue))!;

        private readonly Func<object, object?> getter;
        private readonly Action<object, object?>? setter;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompiledPropertyPath"/> class.
        /// </summary>
        /// <param name="path">The path to access an object.</param>
        /// <param name="typeContext">The type to get info about properties.</param>
        public CompiledPropertyPath(string path, Type typeContext)
            : base(path, typeContext)
        {
            var parameter = Expression.Parameter(typeof(object));
            Expression callChain = typeContext.IsValueType ? Expression.Unbox(parameter, typeContext) : Expression.TypeAs(parameter, typeContext);
            Expression chainBeforeLast = callChain;
            for (int i = 0; i < PathSegments.Count; i++)
            {
                PathSegment segment = PathSegments[i];
                if (i == PathSegments.Count - 1)
                {
                    chainBeforeLast = callChain;
                }

                callChain = GetAccessor(segment, callChain);
            }

            PathSegment lastSegment = PathSegments[^1];
            bool isReadOnly = lastSegment.IsReadOnly;

            Expression cast = Expression.TypeAs(callChain, typeof(object));
            getter = Expression.Lambda<Func<object, object?>>(cast, parameter).Compile();
            if (!isReadOnly)
            {
                ParameterExpression setParameter = Expression.Parameter(typeof(object));
                Expression set;
                if (lastSegment.Reference != null)
                {
                    // A reference-backed segment (e.g. a property registered through PropertyRegistry, or an
                    // attached/synthetic property) has no CLR member to assign to directly — route the value
                    // through IPropertyReference.SetRawValue so it still participates in the value-precedence
                    // system, the same way PathSegment.SetValue does for the non-compiled path.
                    set = Expression.Call(
                        Expression.Constant(lastSegment.Reference),
                        SetRawValueMethod,
                        Expression.Convert(chainBeforeLast, typeof(object)),
                        setParameter);
                }
                else
                {
                    Expression uncast = lastSegment.ResultType.IsValueType ? Expression.Unbox(setParameter, lastSegment.ResultType) : Expression.TypeAs(setParameter, lastSegment.ResultType);
                    set = Expression.Assign(callChain, uncast);
                }

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
            else if (segment.Reference != null)
            {
                Expression call = Expression.Call(Expression.Constant(segment.Reference), GetRawValueMethod, Expression.Convert(argument, typeof(object)));
                return Expression.Convert(call, segment.ResultType);
            }
            else if (segment.Params != null)
            {
                return Expression.MakeIndex(argument, segment.Property, segment.Params.Select(Expression.Constant));
            }

            return Expression.Property(argument, segment.Property);
        }
    }
}
