// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Reflection;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents the property path that is traversed dynamically, according to the provided object type.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class DynamicPropertyPath : IPropertyPath
    {
        private readonly string displayPath;
        private readonly string[] pathSegments;
        private readonly string? rootPropertyName;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicPropertyPath"/> class.
        /// </summary>
        /// <param name="path">The path to access properties.</param>
        public DynamicPropertyPath(string path)
        {
            pathSegments = path.Split('.');
            displayPath = path;
            rootPropertyName = PropertyPath.GetRootPropertyName(path);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// A dynamic path doesn't know its types until it walks a source object, so this is always
        /// <see cref="object"/>. Use <see cref="GetPropertyType(object)"/> to resolve the real type.
        /// </remarks>
        public Type PropertyType => typeof(object);

        /// <inheritdoc/>
        /// <remarks>
        /// Walks every segment but the last one on <paramref name="source"/>, then returns the declared type of the last
        /// property, indexer or array element. Returns <see cref="object"/> when the path can't be resolved, e.g. when an
        /// intermediate value is <see langword="null"/> or a property doesn't exist.
        /// </remarks>
        public Type GetPropertyType(object source)
        {
            try
            {
                object? current = source;
                for (int i = 0; i < pathSegments.Length - 1 && current != null; i++)
                    current = CallGet(current, pathSegments[i]);
                return current == null ? typeof(object) : GetSegmentType(current, pathSegments[^1]) ?? typeof(object);
            }
            catch
            {
                return typeof(object);
            }
        }

        /// <inheritdoc/>
        public bool DependsOn(string propertyName)
            => rootPropertyName == null || string.Equals(rootPropertyName, propertyName, StringComparison.Ordinal);

        /// <inheritdoc/>
        public object? GetValue(object source)
        {
            try
            {
                object? current = source;
                foreach (var segment in pathSegments)
                {
                    if (current == null)
                        return null;
                    current = CallGet(current, segment);
                }

                return current;
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <para>
        /// Does nothing when the path can't be resolved: an intermediate value is <see langword="null"/>, or a property
        /// along the path doesn't exist.
        /// </para>
        /// <para>
        /// Exceptions from the final write itself propagate: the setter rejecting the value (wrapped in a
        /// <see cref="TargetInvocationException"/>), or a value of the wrong type (<see cref="ArgumentException"/>).
        /// </para>
        /// </remarks>
        public void SetValue(object target, object? value)
        {
            object? current = target;
            try
            {
                for (int i = 0; i < pathSegments.Length - 1 && current != null; i++)
                    current = CallGet(current, pathSegments[i]);
            }
            catch
            {
                return;
            }

            if (current != null)
                CallSet(current, pathSegments[^1], value);
        }

        private static Type? GetSegmentType(object current, string segment)
        {
            int indexerDefinition = segment.IndexOf('[');
            if (indexerDefinition == -1)
            {
                return segment == "this"
                    ? null
                    : current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public)?.PropertyType;
            }

            string segmentName = segment[..indexerDefinition];
            object? container = segmentName == "this"
                ? current
                : current.GetType().GetProperty(segmentName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(current);
            if (container is Array array)
                return array.GetType().GetElementType();

            string indexerInfo = segment[(indexerDefinition + 1)..segment.LastIndexOf(']')];
            return container?.GetType().GetIndexer(indexerInfo.Split(','), out _).PropertyType;
        }

        private static void CallSet(object current, string segment, object? value)
        {
            int indexerDefinition = segment.IndexOf('[');
            if (indexerDefinition != -1)
            {
                string indexerInfo = segment[(indexerDefinition + 1)..segment.LastIndexOf(']')];
                string segmentName = segment[..indexerDefinition];
                if (segmentName != "this")
                {
                    var property = current?.GetType().GetProperty(segmentName, BindingFlags.Instance | BindingFlags.Public);
                    property?.SetValue(current, value);
                }

                var indexerArgs = indexerInfo.Split(',');
                if (current != null)
                {
                    if (current is Array arr)
                    {
                        long[] args = Array.ConvertAll(indexerArgs, long.Parse);
                        arr.SetValue(value, args);
                    }
                    else
                    {
                        var indexer = current.GetType().GetIndexer(indexerArgs, out var parsedArgs);
                        indexer?.SetValue(current, value, parsedArgs);
                    }
                }
            }
            else if (segment != "this")
            {
                var property = current?.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
                property?.SetValue(current, value);
            }
        }

        private static object? CallGet(object? current, string segment)
        {
            int indexerDefinition = segment.IndexOf('[');
            if (indexerDefinition != -1)
            {
                string indexerInfo = segment[(indexerDefinition + 1)..segment.LastIndexOf(']')];
                string segmentName = segment[..indexerDefinition];
                if (segmentName != "this")
                {
                    var property = current?.GetType().GetProperty(segmentName, BindingFlags.Instance | BindingFlags.Public);
                    current = property?.GetValue(current);
                }

                var indexerArgs = indexerInfo.Split(',');
                if (current != null)
                {
                    if (current is Array arr)
                    {
                        long[] args = Array.ConvertAll(indexerArgs, long.Parse);
                        current = arr.GetValue(args);
                    }
                    else
                    {
                        var indexer = current.GetType().GetIndexer(indexerArgs, out var parsedArgs);
                        current = indexer.GetValue(current, parsedArgs);
                    }
                }
            }
            else if (segment != "this")
            {
                var property = current?.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
                current = property?.GetValue(current);
            }

            return current;
        }

        private string GetDebuggerDisplay()
        {
            return $"(Dynamic) {displayPath}";
        }
    }
}
