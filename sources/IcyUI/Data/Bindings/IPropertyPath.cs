// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Bindings
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

        /// <summary>
        /// Gets the type of the last property in the chain, resolved against the specified source object.
        /// </summary>
        /// <param name="source">An object the path starts from.</param>
        /// <returns>
        /// The declared type of the last property in the chain. The default implementation returns
        /// <see cref="PropertyType"/>.
        /// </returns>
        /// <remarks>
        /// Paths that only learn their types at runtime, such as <see cref="DynamicPropertyPath"/>, override this method to
        /// report the real property type, so a <see cref="Binding"/> can convert typed text into it.
        /// </remarks>
        Type GetPropertyType(object source) => PropertyType;

        /// <summary>
        /// Determines whether a change of the specified property on the source object can change this path's value.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the changed property, as reported by <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the path starts with the specified property, or if it can't tell; otherwise,
        /// <see langword="false"/>. The default implementation always returns <see langword="true"/>.
        /// </returns>
        /// <remarks>
        /// A <see cref="Binding"/> uses this to ignore change notifications for unrelated properties of its source, so they
        /// don't overwrite the text a user is typing.
        /// </remarks>
        bool DependsOn(string propertyName) => true;
    }
}
