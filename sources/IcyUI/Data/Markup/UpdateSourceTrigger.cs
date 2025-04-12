using System.ComponentModel;
using Icy.UI;
using Icy.Data.Bindings;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Declares type of update source trigger.
    /// </summary>
    public enum UpdateSourceTrigger : byte
    {
        /// <summary>
        /// Trigger on property value change.
        /// </summary>
        /// <remarks>
        /// To raise update binding trigger, binding endpoint should implement <see cref="INotifyPropertyChanged"/> interface.
        /// </remarks>
        PropertyChanged,

        /// <summary>
        /// Trigger on target focus losing.
        /// </summary>
        /// <remarks>
        /// To raise update binding trigger, binding endpoint should implement <see cref="INotifyFocusChanged"/> interface.
        /// </remarks>
        LostFocus,

        /// <summary>
        /// Trigger only on manual calling <see cref="IBinding.UpdateSource()"/> or <see cref="IBinding.UpdateTarget()"/> methods.
        /// </summary>
        Explicit,
    }
}
