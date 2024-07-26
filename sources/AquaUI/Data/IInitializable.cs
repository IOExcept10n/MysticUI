namespace AquaUI.Data
{
    /// <summary>
    /// Provides an interface for all the objects that supports late initialization.
    /// </summary>
    public interface IInitializable
    {
        /// <summary>
        /// Gets a value indicating whether the object has been initialized.
        /// </summary>
        public bool IsInitialized { get; }

        /// <summary>
        /// Initializes an instance of an object.
        /// </summary>
        public void Initialize();
    }
}
