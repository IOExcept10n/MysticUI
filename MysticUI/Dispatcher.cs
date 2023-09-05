namespace MysticUI
{
    /// <summary>
    /// Represents a class that provides a functionality for dispatching all asynchronous calls synchronously.
    /// </summary>
    public class Dispatcher
    {
        private readonly Queue<Action> dispatchedActions = new();

        /// <summary>
        /// Amount of actions that can be performed per one update call.
        /// </summary>
        public int DispatchedAmount { get; set; } = 2;

        /// <summary>
        /// Updates the dispatcher, calls the first actions in the queue and stops the rendering until they're ended.
        /// </summary>
        public void Update()
        {
            for (int i = 0; i < DispatchedAmount; i++)
            {
                if (dispatchedActions.TryDequeue(out Action? action))
                    action();
            }
        }

        /// <summary>
        /// Clears the dispatcher by calling all remaining actions.
        /// </summary>
        public void Reset()
        {
            while (dispatchedActions.TryDequeue(out var action))
                action();
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