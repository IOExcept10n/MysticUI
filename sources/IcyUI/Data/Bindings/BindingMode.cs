namespace Icy.Data.Bindings
{
    /// <summary>
    /// Declares type of property binding.
    /// </summary>
    public enum BindingMode
    {
        /// <summary>
        /// Updates value of one property when other property changes regardless source or target update.
        /// </summary>
        TwoWay,

        /// <summary>
        /// Updates target value when source value changes.
        /// </summary>
        OneWay,

        /// <summary>
        /// Only sets source value one time when <see cref="Binding.Source"/> is set.
        /// </summary>
        /// <remarks>
        /// After triggering, binding will try to dispose itself or detach from the target.
        /// </remarks>
        OneTime,

        /// <summary>
        /// updates source value when target value changes.
        /// </summary>
        OneWayToSource,
    }
}
