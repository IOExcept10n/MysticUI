// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents an interface for the binding objects.
    /// </summary>
    /// <remarks>
    /// <b>Binding</b> is an object that makes relationship between one property of an object to the other property of the another (or the same) object.
    /// </remarks>
    /// <seealso cref="Binding"/>.
    public interface IBinding : IDisposable
    {
        /// <summary>
        /// Sets the target for the binding.
        /// </summary>
        /// <param name="target">A target to set.</param>
        void SetTarget(IBindingTarget target);

        /// <summary>
        /// Sets the source for the binding.
        /// </summary>
        /// <param name="source">A source to set.</param>
        void SetSource(object source);

        /// <summary>
        /// Unsubscribe from source and target objects.
        /// </summary>
        void DestroyBinding();

        /// <summary>
        /// Force update source object from target object.
        /// </summary>
        void UpdateSource();

        /// <summary>
        /// Force update target object from source object.
        /// </summary>
        void UpdateTarget();
    }
}
