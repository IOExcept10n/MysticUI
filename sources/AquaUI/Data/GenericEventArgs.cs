namespace AquaUI.Data
{
    /// <summary>
    /// A class for any event args with data of given type.
    /// </summary>
    /// <typeparam name="T">Type fo data stored in the event args.</typeparam>
    public class GenericEventArgs<T> : EventArgs, IDataEventArgs<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericEventArgs{T}"/> class.
        /// </summary>
        /// <param name="data">Data to keep.</param>
        public GenericEventArgs(T data)
        {
            Data = data;
        }

        /// <summary>
        /// Gets data to store.
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// Implicitly converts data to the <see cref="GenericEventArgs{T}"/> type.
        /// </summary>
        /// <param name="data">Data to store.</param>
        public static implicit operator GenericEventArgs<T>(T data) => new(data);

        /// <summary>
        /// Explicitly gets the data of the given <see cref="GenericEventArgs{T}"/> instance.
        /// </summary>
        /// <param name="e">Arguments to get data from.</param>
        public static explicit operator T(GenericEventArgs<T> e) => e.Data;
    }
}
