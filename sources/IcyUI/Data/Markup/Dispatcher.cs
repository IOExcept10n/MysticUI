// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a class that provides a functionality for dispatching all asynchronous calls synchronously.
    /// </summary>
    public class Dispatcher
    {
        private static readonly Dictionary<Thread, Dispatcher> Dispatchers = [];

        private readonly PriorityQueue<Action, DispatcherPriority> dispatchedActions = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Dispatcher"/> class.
        /// </summary>
        /// <param name="thread">A thread on which the dispatcher was made.</param>
        internal Dispatcher(Thread thread)
        {
            Thread = thread;
        }

        /// <summary>
        /// Gets or sets the amount of actions that can be performed per one update call.
        /// </summary>
        public int DispatchedAmount { get; set; } = 10;

        /// <summary>
        /// Gets the thread on which the dispatcher was made.
        /// </summary>
        public Thread Thread { get; }

        /// <summary>
        /// Gets the dispatcher for the current thread if available, otherwise creates the new one.
        /// </summary>
        /// <returns>The dispatcher for the current thread.</returns>
        public static Dispatcher GetCurrentThreadDispatcher()
        {
            if (!Dispatchers.TryGetValue(Thread.CurrentThread, out var result))
            {
                result = Dispatchers[Thread.CurrentThread] = new Dispatcher(Thread.CurrentThread);
            }

            return result;
        }

        /// <summary>
        /// Cancels all remaining actions before their execution.
        /// </summary>
        public void Cancel()
        {
            dispatchedActions.Clear();
        }

        /// <summary>
        /// Checks if the dispatcher can be accessed with the current thread.
        /// </summary>
        /// <returns><see langword="true"/> if a dispatcher is accessible with current thread; <see langword="false"/> otherwise.</returns>
        public bool CheckAccess() => Thread == Thread.CurrentThread;

        /// <summary>
        /// Adds an action to the synchronous queue.
        /// </summary>
        /// <param name="action">An action to complete synchronously.</param>
        /// <param name="priority">The priority for an action to complete.</param>
        public void Invoke(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (priority == DispatcherPriority.Send && CheckAccess())
            {
                action();
                return;
            }
            else
            {
                dispatchedActions.Enqueue(action, priority);
            }
        }

        /// <summary>
        /// Clears the dispatcher by calling all remaining actions.
        /// </summary>
        public void Reset()
        {
            while (dispatchedActions.TryDequeue(out var action, out _))
                action();
        }

        /// <summary>
        /// Updates the dispatcher, calls the first actions in the queue and stops the rendering until they're ended.
        /// </summary>
        public void Update()
        {
            for (int i = 0; i < DispatchedAmount; i++)
            {
                if (dispatchedActions.TryDequeue(out Action? action, out _))
                    action();
            }
        }

        /// <summary>
        /// Updated the dispatcher, calls the first actions in the queue with priorities more or equal to given.
        /// </summary>
        /// <param name="priority">The priority to execute operations with.</param>
        public void Update(DispatcherPriority priority)
        {
            for (int i = 0; i < DispatchedAmount; i++)
            {
                if (dispatchedActions.TryPeek(out var action, out var currentPriority))
                {
                    if (currentPriority > priority)
                        return;
                    dispatchedActions.Dequeue();
                    action();
                }
            }
        }

        /// <summary>
        /// Determines whether the current thread can access this <see cref="Dispatcher"/> instance.
        /// </summary>
        public void VerifyAccess()
        {
            if (!CheckAccess())
                ThrowHelper.ThrowInvalidOperationException("Current thread cannot access this dispatcher instance.");
        }
    }
}
