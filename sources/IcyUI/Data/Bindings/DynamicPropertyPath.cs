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

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicPropertyPath"/> class.
        /// </summary>
        /// <param name="path">The path to access properties.</param>
        public DynamicPropertyPath(string path)
        {
            pathSegments = path.Split('.');
            displayPath = path;
        }

        /// <inheritdoc/>
        public Type PropertyType => typeof(object);

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
        public void SetValue(object target, object? value)
        {
            try
            {
                object? current = target;
                for (int i = 0; i < pathSegments.Length; i++)
                {
                    string? segment = pathSegments[i];
                    if (current == null)
                        return;

                    if (i == pathSegments.Length - 1)
                    {
                        CallSet(current, segment, value);
                    }
                    else
                    {
                        current = CallGet(current, segment);
                    }
                }
            }
            catch
            {
            }
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
