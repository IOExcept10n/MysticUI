// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Data.Bindings
{
    /// <summary>
    /// Represents an interface for the runtime property chain path.
    /// </summary>
    public interface IPropertyPath : IReadOnlyPropertyPath
    {
        /// <summary>
        /// Sets the value by calling the property chain for the specified target object.
        /// </summary>
        /// <param name="target">An object to set the value to.</param>
        /// <param name="value">The value to set to the property.</param>
        void SetValue(object target, object? value);
    }

    /// <summary>
    /// Represents an interface for the runtime property chain path with read-only access.
    /// </summary>
    public interface IReadOnlyPropertyPath
    {
        /// <summary>
        /// Gets the type of the last property in the chain.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// Gets the value by calling the property chain for the specified source object.
        /// </summary>
        /// <param name="source">An object to retrieve value from.</param>
        /// <returns>The value by the specified property path.</returns>
        object? GetValue(object source);
    }
}