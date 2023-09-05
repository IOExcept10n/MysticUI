namespace MysticUI
{
    /// <summary>
    /// An interface to store and handle property bindings.
    /// </summary>
    public interface IBindingHandler
    {
        /// <summary>
        /// All bindings set to this target.
        /// </summary>
        public IReadOnlyCollection<Binding> Bindings { get; }

        /// <summary>
        /// Adds a binding to this object. Binding should have this object as a target.
        /// </summary>
        /// <param name="binding">Binding instance to add.</param>
        public void SetBinding(Binding binding);

        /// <summary>
        /// Removes a binding from this object. This also will dispose the binding.
        /// </summary>
        /// <param name="binding">Binding instance to remove.</param>
        public void RemoveBinding(Binding binding);
    }
}