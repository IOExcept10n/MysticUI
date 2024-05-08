namespace AquaUI.Data
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
        /// Unsubscribe from source and target objects.
        /// </summary>
        public void DestroyBinding();

        /// <summary>
        /// Force update source object from target object.
        /// </summary>
        public void UpdateSource();

        /// <summary>
        /// Force update target object from source object.
        /// </summary>
        public void UpdateTarget();
    }
}