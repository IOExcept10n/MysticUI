namespace MysticUI.Extensions
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
        /// Gets or sets the value that indicates if the event was handled successfully by the handler.
        /// </summary>
        public bool Handled { get; set; }
    }
}