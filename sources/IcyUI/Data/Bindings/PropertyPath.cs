// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents a static-typed property path.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class PropertyPath : IPropertyPath
    {
        private readonly string displayPath;
        private readonly List<PathSegment> pathSegments;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyPath"/> class.
        /// </summary>
        /// <param name="path">The path to get the value by the property.</param>
        /// <param name="typeContext">Initial type to access the properties for.</param>
        public PropertyPath(string path, Type typeContext)
        {
            displayPath = path;
            pathSegments = ParsePath(path, typeContext);
        }

        /// <inheritdoc/>
        public Type PropertyType => pathSegments[^1].ResultType;

        /// <summary>
        /// Gets the path for this instance for the debug display.
        /// </summary>
        protected string DisplayPath => displayPath;

        /// <summary>
        /// Gets the list of the part segments for this property path.
        /// </summary>
        protected List<PathSegment> PathSegments => pathSegments;

        /// <inheritdoc/>
        public virtual object? GetValue(object source)
        {
            foreach (var segment in PathSegments)
            {
                source = segment.GetValue(source)!;
            }

            return source;
        }

        /// <inheritdoc/>
        public virtual void SetValue(object target, object? value)
        {
            for (int i = 0; i < pathSegments.Count; i++)
            {
                if (i == pathSegments.Count - 1)
                {
                    pathSegments[i].SetValue(target, value);
                }
                else
                {
                    target = pathSegments[i].GetValue(target)!;
                }
            }
        }

        /// <summary>
        /// Gets the list of path segments for the specified property path.
        /// </summary>
        /// <param name="path">The path string to parse.</param>
        /// <param name="typeContext">The type to get property info.</param>
        /// <returns>The list of <see cref="PathSegment"/> with all segments of the specified path.</returns>
        /// <exception cref="FormatException">Occurs if the indexer wasn't defined correctly.</exception>
        protected static List<PathSegment> ParsePath(string path, Type typeContext)
        {
            string[] pathParts = path.Split('.');
            var pathSegments = new List<PathSegment>(pathParts.Length);
            for (int i = 0; i < pathParts.Length; i++)
            {
                int indexerOccurance = pathParts[i].IndexOf('[');
                if (indexerOccurance != -1)
                {
                    try
                    {
                        string indexerContents = pathParts[i][(indexerOccurance + 1)..pathParts[i].LastIndexOf(']')];
                        pathParts[i] = pathParts[i][..indexerOccurance];
                        PathSegment segment;
                        if (pathParts[i] != "this")
                        {
                            segment = PathSegment.FromProperty(typeContext, pathParts[i]);
                            typeContext = segment.ResultType;
                            pathSegments.Add(segment);
                        }

                        segment = PathSegment.FromIndexer(typeContext, indexerContents.Split(','));
                        typeContext = segment.ResultType;
                        pathSegments.Add(segment);
                    }
                    catch (Exception ex)
                    {
                        throw new FormatException("Can't parse indexer part because of the wrong indexer format:", ex);
                    }
                }
                else if (pathParts[i] != "this")
                {
                    var segment = PathSegment.FromProperty(typeContext, pathParts[i]);
                    typeContext = segment.ResultType;
                    pathSegments.Add(segment);
                }
            }

            return pathSegments;
        }

        private string GetDebuggerDisplay()
        {
            return DisplayPath;
        }

        /// <summary>
        /// Represents a segment of the <see cref="PropertyPath"/>.
        /// </summary>
        protected readonly struct PathSegment
        {
            /// <summary>
            /// Gets the parameters to call the indexer.
            /// </summary>
            public readonly object?[]? Params;

            /// <summary>
            /// Gets the property info to get the value from.
            /// </summary>
            public readonly PropertyInfo? Property;

            /// <summary>
            /// Gets the registered property reference to get the value from, when the target type has one.
            /// </summary>
            /// <remarks>
            /// Set for properties registered through <see cref="PropertyRegistry"/> (see <see cref="Attributes.RegisterReferenceAttribute"/>),
            /// so that binding through a path segment participates in the same value-precedence system as
            /// styles, visual states, and animations. Falls back to <see cref="Property"/> for everything else,
            /// including plain BCL types that were never registered.
            /// </remarks>
            public readonly IPropertyReference? Reference;

            /// <summary>
            /// Gets the type of the calculation result.
            /// </summary>
            public readonly Type ResultType;

            private PathSegment(PropertyInfo property, object?[]? parameters)
            {
                Property = property;
                ResultType = property.PropertyType;
                Params = parameters;
            }

            private PathSegment(IPropertyReference reference)
            {
                Reference = reference;
                ResultType = reference.PropertyType;
            }

            private PathSegment(object[] arrayIndices, Type targetType)
            {
                Params = arrayIndices;
                ResultType = targetType;
                IsArray = true;
            }

            /// <summary>
            /// Gets a value indicating whether the target type is an array.
            /// </summary>
            [MemberNotNullWhen(true, nameof(Params))]
            public readonly bool IsArray { get; init; }

            /// <summary>
            /// Gets a value indicating whether the path segment can't be assigned to.
            /// </summary>
            public bool IsReadOnly { get; init; }

            /// <summary>
            /// Gets an indexer call segment.
            /// </summary>
            /// <param name="target">Target type to call the indexer for.</param>
            /// <param name="arguments">Array of arguments to access the indexer for.</param>
            /// <returns>An instance of the <see cref="PathSegment"/> struct for the indexer access.</returns>
            public static PathSegment FromIndexer(Type target, string[] arguments)
            {
                if (target.IsArray)
                {
                    return new PathSegment(Array.ConvertAll(arguments, x => (object)int.Parse(x)), target.GetElementType()!);
                }

                var indexer = target.GetIndexer(arguments, out var parsed);
                return new PathSegment(indexer, parsed)
                {
                    IsReadOnly = indexer?.CanWrite != true,
                };
            }

            /// <summary>
            /// Gets a property call segment.
            /// </summary>
            /// <param name="target">Target type to get the property for.</param>
            /// <param name="propertyName">A property name to search the property.</param>
            /// <returns>An instance of the <see cref="PathSegment"/> struct for the property access.</returns>
            public static PathSegment FromProperty(Type target, string propertyName)
            {
                // Prefer a property registered through PropertyRegistry (see RegisterReferenceAttribute), so that
                // binding through this segment participates in the same value-precedence system (Local > Animation
                // > VisualState > Style > Default) as styles, visual states, and animations. Every registered
                // property is guaranteed to have a setter (ResolveProperties skips read-only ones), so it's never read-only.
                if (PropertyRegistry.Instance.GetPropertyStore(target).TryGetProperty(propertyName, searchInherited: true, out IPropertyReference? reference))
                    return new PathSegment(reference);

                var property = target.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    return ThrowHelper.ThrowArgumentException<PathSegment>(nameof(propertyName), $"Can't find property {propertyName} on a {target} type.");
                return new PathSegment(property, null)
                {
                    IsReadOnly = !property.CanWrite,
                };
            }

            /// <summary>
            /// Gets the value of an object for the current path segment.
            /// </summary>
            /// <param name="obj">An object to get the value from.</param>
            /// <returns>Value of the defined property of the specified object.</returns>
            public object? GetValue(object obj)
            {
                if (IsArray)
                    return ((Array)obj).GetValue(Array.ConvertAll(Params!, x => (int)x!));
                if (Reference != null)
                    return Reference.GetRawValue(obj);
                return Property!.GetValue(obj, Params);
            }

            /// <summary>
            /// Sets the value of an object for the current path segment.
            /// </summary>
            /// <param name="target">An object to set the value to.</param>
            /// <param name="value">The value to set.</param>
            public void SetValue(object target, object? value)
            {
                if (IsReadOnly)
                    ThrowHelper.ThrowInvalidOperationException("Can't assign value to the readonly path segment.");

                if (IsArray)
                {
                    ((Array)target).SetValue(value, Array.ConvertAll(Params!, x => (int)x!));
                    return;
                }

                if (Reference != null)
                {
                    Reference.SetRawValue(target, value);
                    return;
                }

                Property!.SetValue(target, value, Params);
            }
        }
    }
}