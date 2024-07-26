namespace AquaUI.Data
{
    /// <summary>
    /// Represents a common interface for the <see cref="EventArgs"/> implementations that can store data of a certain type.
    /// </summary>
    /// <typeparam name="T">Type of the data stored in arguments.</typeparam>
    public interface IDataEventArgs<T> : IDataEventArgs
    {
        /// <summary>
        /// Gets the data stored in this event args.
        /// </summary>
        new T Data { get; }

        /// <inheritdoc/>
        object? IDataEventArgs.Data => Data;
    }

    /// <summary>
    /// Represents a common interface for the <see cref="EventArgs"/> implementations that can store data of any type.
    /// </summary>
    public interface IDataEventArgs
    {
        /// <summary>
        /// Gets the data stored in this event args.
        /// </summary>
        object? Data { get; }
    }
}