using System.ComponentModel;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// An interface for objects that store and handle property bindings as target.
    /// </summary>
    /// <remarks>
    /// Your binding target doesn't have to implement this interface. However,
    /// if it implements <see cref="IBindingTarget"/>, binder knows that your object is prepared for bindings and can utilize them correctly.
    /// </remarks>
    public interface IBindingTarget : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets all bindings set to this target.
        /// </summary>
        public IReadOnlyCollection<IBinding> Bindings { get; }

        /// <summary>
        /// Adds a binding to this object. Binding should have this object as a target.
        /// </summary>
        /// <param name="binding">Binding instance to add.</param>
        public void Bind(IBinding binding);

        /// <summary>
        /// Removes a binding from this object. This also will dispose the binding.
        /// </summary>
        /// <param name="binding">Binding instance to remove.</param>
        /// <returns><see langword="true"/> if the bindings was removed successfully, <see langword="false"/> otherwise.</returns>
        public bool Unbind(IBinding binding);

        /// <summary>
        /// Removes all the related bindings from this object.
        /// </summary>
        public void UnbindAll();

        /// <summary>
        /// Removes all bindings that match the specified <paramref name="predicate"/>.
        /// </summary>
        /// <param name="predicate">A predicate to select bindings for the removal.</param>
        /// <returns>Count of removed bindings.</returns>
        int UnbindWhere(Func<IBinding, bool> predicate);
    }
}
