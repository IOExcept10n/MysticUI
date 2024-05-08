using System.Collections.Concurrent;

namespace AquaUI
{
    /// <summary>
    /// Represents a class that provides a functionality for UI tasks synchronization.
    /// </summary>
    public class Dispatcher
    {
        private readonly ConcurrentQueue<Action> dispatchedActions = new();

        /// <summary>
        /// Amount of actions that can be performed per one update call.
        /// </summary>
        public int DispatchedAmount { get; set; } = 10;

        /// <summary>
        /// Updates the dispatcher and calls the <see cref="DispatchedAmount"/> of actions in the queue.
        /// </summary>
        public void Update()
        {
            for (int i = 0; i < DispatchedAmount && dispatchedActions.TryDequeue(out Action? action); i++)
            {
                action();
            }
        }

        /// <summary>
        /// Clears the dispatcher by calling all remaining actions.
        /// </summary>
        public void WaitAll()
        {
            while (dispatchedActions.TryDequeue(out var action))
                action();
        }

        /// <summary>
        /// Allows to wait all the remaining tasks asynchronously.
        /// </summary>
        /// <returns></returns>
        public Task WaitAllAsync()
        {
            TaskCompletionSource source = new();
            try
            {
                WaitAll();
            }
            catch (Exception ex)
            {
                source.SetException(ex);
            }
            source.SetResult();
            return source.Task;
        }

        /// <summary>
        /// Cancels all remaining actions before their execution.
        /// </summary>
        public void Cancel()
        {
            dispatchedActions.Clear();
        }

        /// <summary>
        /// Adds an action to the synchronous queue.
        /// </summary>
        /// <param name="action">An action to complete synchronously.</param>
        public void Dispatch(Action action)
        {
            dispatchedActions.Enqueue(action);
        }
    }
}
