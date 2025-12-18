// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Bindings;
using Icy.UI;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Declares type of update source trigger.
    /// </summary>
    public enum UpdateSourceTrigger : byte
    {
        /// <summary>
        /// Triggers an update when the property value changes.
        /// </summary>
        /// <remarks>
        /// To raise this update binding trigger, the binding source must implement <see cref="INotifyPropertyChanged"/> interface.
        /// </remarks>
        PropertyChanged,

        /// <summary>
        /// Triggers an update when the target loses focus.
        /// </summary>
        /// <remarks>
        /// To raise this update binding trigger, the binding source must implement the <see cref="INotifyFocusChanged"/> interface.
        /// </remarks>
        LostFocus,

        /// <summary>
        /// Triggers an update only when the <see cref="IBinding.UpdateSource()"/> or <see cref="IBinding.UpdateTarget()"/> methods are called manually.
        /// </summary>
        /// <remarks>
        /// This trigger allows for explicit control over when updates occur.
        /// </remarks>
        Explicit,
    }
}