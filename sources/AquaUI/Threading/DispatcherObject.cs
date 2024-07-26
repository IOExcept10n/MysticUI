// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;

namespace AquaUI.Threading
{
    /// <summary>
    /// Provides an object that should support synchronized data dispatch.
    /// </summary>
    public abstract class DispatcherObject
    {
        /// <summary>
        /// Gets the dispatcher for the current object. It is initialized on object initialization and bent to the thread in which the object was initialized.
        /// </summary>
        public Dispatcher Dispatcher { get; } = Dispatcher.GetCurrentThreadDispatcher();

        /// <summary>
        /// Checks if an object can be accessed with the current thread.
        /// </summary>
        /// <returns><see langword="true"/> if an object is accessible with current thread; <see langword="false"/> otherwise.</returns>
        public bool CheckAccess() => Dispatcher.CheckAccess();

        /// <summary>
        /// Checks if an object can be accessed with the current thread and throws the exception otherwise.
        /// </summary>
        public void VerifyAccess()
        {
            if (!Dispatcher.CheckAccess())
                ThrowHelper.ThrowInvalidOperationException("This object can't be accessed from the current thread.");
        }
    }
}