using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Icy.Data
{
    /// <summary>
    /// Provides extensions for the reflection purposes.
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Gets a value that determines whether the type is <see cref="Nullable{T}"/>.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns><see langword="true"/> if the type is <see cref="Nullable{T}"/>; otherwise <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullable(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        /// <summary>
        /// Gets the type info without the <see cref="Nullable{T}"/> definition.
        /// </summary>
        /// <param name="type">The type to get non-nullable definition.</param>
        /// <returns>The type itself if it is not <see cref="Nullable{T}"/>; underlying type otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type IgnoreNullableDefinition(this Type type)
        {
            if (type.IsNullable())
                return type.GenericTypeArguments[0];
            return type;
        }

        /// <summary>
        /// Gets the indexer definition for the specified type according to the specified indexer arguments.
        /// </summary>
        /// <param name="targetType">The target type to get indexer definition.</param>
        /// <param name="arguments">The argument string array to search indexer.</param>
        /// <param name="convertedArguments">The result of the arguments conversion.</param>
        /// <returns>The indexer definition that supports the provided arguments.</returns>
        /// <exception cref="ArgumentException">Occurs when there are no indexers for the specified arguments.</exception>
        /// <exception cref="AmbiguousMatchException">Occurs when there are more than one indexer for the specified arguments.</exception>
        public static PropertyInfo GetIndexer(this Type targetType, string[] arguments, out object?[] convertedArguments)
        {
            var converter = TypeConversionManager.Instance;

            var indexers = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                     .Where(p => p.GetIndexParameters().Length == arguments.Length)
                                     .ToList();

            List<(PropertyInfo Property, object?[] ConvertedArguments)> matchingIndexers = [];

            foreach (var indexer in indexers)
            {
                var parameters = indexer.GetIndexParameters();
                convertedArguments = new object[arguments.Length];
                bool match = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i >= arguments.Length && parameters[i].HasDefaultValue)
                    {
                        convertedArguments[i] = parameters[i].DefaultValue;
                        continue;
                    }

                    try
                    {
                        convertedArguments[i] = converter.Convert(arguments[i].Trim(), parameters[i].ParameterType);
                        // Strings can be covered with quotes, they should be removed before getting an indexer.
                        if (convertedArguments[i] is string str)
                        {
                            convertedArguments[i] = str.Trim('"');
                        }
                    }
                    catch
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    matchingIndexers.Add((indexer, convertedArguments));
                }
            }

            if (matchingIndexers.Count == 0)
            {
                throw new ArgumentException("No suitable indexer found for the provided arguments.");
            }

            if (matchingIndexers.Count > 1)
            {
                throw new AmbiguousMatchException("Multiple indexers match the provided arguments.");
            }

            convertedArguments = matchingIndexers.Single().ConvertedArguments;
            return matchingIndexers.Single().Property;
        }
    }
}
