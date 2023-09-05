using System.ComponentModel;

namespace MysticUI.Extensions
{
    /// <summary>
    /// A class for any event args that can be cancelled.
    /// </summary>
    /// <typeparam name="T">Type fo data stored in the event args.</typeparam>
    public class CancellableEventArgs<T> : CancelEventArgs
    {
        /// <summary>
        /// Data to store.
        /// </summary>
        public T Data { get; internal set; } = default!;

        /// <summary>
        /// Creates a new instance of the <see cref="CancellableEventArgs{T}"/> class.
        /// </summary>
        public CancellableEventArgs()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="CancellableEventArgs{T}"/> class.
        /// </summary>
        /// <param name="data">Data to keep.</param>
        public CancellableEventArgs(T data)
        {
            Data = data;
        }

        /// <summary>
        /// Implicitly converts data to the <see cref="CancellableEventArgs{T}"/> type.
        /// </summary>
        /// <param name="data">Data to store.</param>
        public static implicit operator CancellableEventArgs<T>(T data) => new(data);

        /// <summary>
        /// Explicitly gets the data of the given <see cref="CancellableEventArgs{T}"/> instance.
        /// </summary>
        /// <param name="e"></param>
        public static explicit operator T(CancellableEventArgs<T> e) => e.Data;
    }
}