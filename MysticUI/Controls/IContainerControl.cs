namespace MysticUI.Controls
{
    /// <summary>
    /// Provides an interface for controls that can contain a collection of other controls.
    /// </summary>
    public interface IContainerControl : IReadOnlyCollection<Control>
    {
        /// <summary>
        /// Provides a secure read-only access to the control's children.
        /// </summary>
        public IReadOnlyCollection<Control> Children { get; }

        /// <summary>
        /// Amount of controls in this container.
        /// </summary>
        int IReadOnlyCollection<Control>.Count => Children.Count;

        /// <summary>
        /// Determines whether the container is read-only.
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Adds a control to a container. If the container is read-only, this method will fail.
        /// </summary>
        /// <param name="control">A control to add to a container.</param>
        public void Add(Control control);

        /// <summary>
        /// Removes a control from a container. If the container is read-only, this method will fail.
        /// </summary>
        /// <param name="control">A control to remove from a container.</param>
        /// <returns><see langword="true"/> if the control was presented in the container and was removed successfully, <see langword="false"/> otherwise.</returns>
        public bool Remove(Control control);
    }
}