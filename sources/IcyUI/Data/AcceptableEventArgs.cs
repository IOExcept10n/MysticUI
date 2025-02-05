namespace Icy.Data
{
    /// <summary>
    /// Represents an event args with the ability to accept the argument.
    /// </summary>
    /// <remarks>
    /// Unlike common <see cref="CancellableEventArgs{T}"/>, these event args have three possible states to indicate.
    /// </remarks>
    /// <typeparam name="T">Type of the event data.</typeparam>
    public class AcceptableEventArgs<T> : CancellableEventArgs<T>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the event was handled successfully.
        /// </summary>
        public bool Handled { get; set; }
    }
}
