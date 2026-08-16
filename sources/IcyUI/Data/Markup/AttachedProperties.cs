// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.CompilerServices;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Provides per-instance storage for attached property values, keyed by the owning object's lifetime.
    /// </summary>
    /// <remarks>
    /// Static getter/setter accessors declared for an <see cref="Attributes.AttachedPropertyAttribute"/>-marked
    /// type (e.g. <c>Grid.GetRow</c>/<c>Grid.SetRow</c>) should read and write through this store rather than
    /// maintaining their own storage, so values live and die with the target instance.
    /// </remarks>
    public static class AttachedProperties
    {
        private static readonly ConditionalWeakTable<object, Dictionary<string, object?>> Storage = [];

        /// <summary>
        /// Gets the attached value stored for <paramref name="target"/> under <paramref name="key"/>.
        /// </summary>
        /// <typeparam name="T">The type of the stored value.</typeparam>
        /// <param name="target">The object the value is attached to.</param>
        /// <param name="key">The name that identifies the attached property.</param>
        /// <param name="defaultValue">The value returned when nothing has been stored yet.</param>
        /// <returns>The stored value, or <paramref name="defaultValue"/> if none was set.</returns>
        public static T GetValue<T>(object target, string key, T defaultValue = default!)
        {
            if (Storage.TryGetValue(target, out var values) && values.TryGetValue(key, out object? value))
                return (T)value!;
            return defaultValue;
        }

        /// <summary>
        /// Sets the attached value stored for <paramref name="target"/> under <paramref name="key"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to store.</typeparam>
        /// <param name="target">The object the value is attached to.</param>
        /// <param name="key">The name that identifies the attached property.</param>
        /// <param name="value">The value to store.</param>
        public static void SetValue<T>(object target, string key, T value)
        {
            var values = Storage.GetValue(target, static _ => []);
            values[key] = value;
        }
    }
}
