// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Concurrent;

namespace Icy.Data
{
    /// <summary>
    /// Caches <see cref="Enum.GetValues(Type)"/>'s result per <see cref="Type"/> - used by
    /// <see cref="UI.Controls.PropertyGrid"/> to populate an enum-typed property's <c>ComboBox</c> editor
    /// without re-reflecting the same type's values on every row rebuild.
    /// </summary>
    public static class EnumValueCache
    {
        private static readonly ConcurrentDictionary<Type, Array> Cache = new();

        /// <summary>
        /// Gets every defined value of <paramref name="enumType"/>, in declaration order.
        /// </summary>
        /// <param name="enumType">An enum type.</param>
        /// <returns>The cached array <see cref="Enum.GetValues(Type)"/> returned for <paramref name="enumType"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="enumType"/> is not an enum type.</exception>
        public static Array GetValues(Type enumType)
        {
            ArgumentNullException.ThrowIfNull(enumType);
            if (!enumType.IsEnum)
                throw new ArgumentException($"'{enumType}' is not an enum type.", nameof(enumType));

            return Cache.GetOrAdd(enumType, Enum.GetValues);
        }
    }
}
