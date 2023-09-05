namespace MysticUI.Controls
{
    /// <summary>
    /// An interface for objects with string names.
    /// </summary>
    public interface INameIdentifiable
    {
        /// <summary>
        /// Name of an element.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Raises when name changed.
        /// </summary>
        public event EventHandler NameChanged;
    }
}