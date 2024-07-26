using System.ComponentModel;

namespace AquaUI.Data
{
    /// <summary>
    /// A class for any event args that can be cancelled.
    /// </summary>
    /// <typeparam name="T">Type fo data stored in the event args.</typeparam>
    public class CancellableEventArgs<T> : CancelEventArgs, IDataEventArgs<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CancellableEventArgs{T}"/> class.
        /// </summary>
        public CancellableEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CancellableEventArgs{T}"/> class.
        /// </summary>
        /// <param name="data">Data to keep.</param>
        public CancellableEventArgs(T data)
        {
            Data = data;
        }

        /// <summary>
        /// Gets data to store.
        /// </summary>
        public T Data { get; internal set; } = default!;

        /// <summary>
        /// Implicitly converts data to the <see cref="CancellableEventArgs{T}"/> type.
        /// </summary>
        /// <param name="data">Data to store.</param>
        public static implicit operator CancellableEventArgs<T>(T data) => new(data);

        /// <summary>
        /// Explicitly gets the data of the given <see cref="CancellableEventArgs{T}"/> instance.
        /// </summary>
        /// <param name="e">Event arguments to get data from.</param>
        public static explicit operator T(CancellableEventArgs<T> e) => e.Data;
    }
}
