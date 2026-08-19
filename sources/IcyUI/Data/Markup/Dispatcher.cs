// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;
using Icy.Animations;
using Icy.Data.Bindings;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a class that provides a functionality for completing calls from multiple threads synchronously.
    /// </summary>
    public class Dispatcher
    {
        private static readonly ConcurrentDictionary<Thread, Dispatcher> Dispatchers = new();
        private readonly object lockObj = new();
        private readonly PriorityQueue<Action, DispatcherPriority> dispatchedActions = new();
        private readonly HashSet<IBinding> frameBindings = [];
        private readonly HashSet<Animation> runningAnimations = [];

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
        public int DispatchedAmount { get; set; } = 32;

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
            return Dispatchers.GetOrAdd(Thread.CurrentThread, static thread => new Dispatcher(thread));
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
            lock (lockObj)
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
        /// <param name="priority">The maximal priority to execute operations with.</param>
        public void Update(DispatcherPriority priority)
        {
            lock (lockObj)
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
                    else
                    {
                        break;
                    }
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

        /// <summary>
        /// Registers a binding to have <see cref="IBinding.UpdateTarget()"/> called once per frame by
        /// <see cref="UpdateFrameBindings"/>, regardless of whether its source raises change notifications.
        /// </summary>
        /// <param name="binding">The binding to poll every frame.</param>
        /// <remarks>
        /// Intended for <see cref="UpdateTargetTrigger.EveryFrame"/> bindings — call
        /// <see cref="UnregisterFrameBinding(IBinding)"/> once the binding is disposed or switches away from that trigger.
        /// </remarks>
        public void RegisterFrameBinding(IBinding binding)
        {
            lock (lockObj)
            {
                frameBindings.Add(binding);
            }
        }

        /// <summary>
        /// Stops calling <see cref="IBinding.UpdateTarget()"/> once per frame for a binding previously passed to
        /// <see cref="RegisterFrameBinding(IBinding)"/>.
        /// </summary>
        /// <param name="binding">The binding to stop polling.</param>
        public void UnregisterFrameBinding(IBinding binding)
        {
            lock (lockObj)
            {
                frameBindings.Remove(binding);
            }
        }

        /// <summary>
        /// Calls <see cref="IBinding.UpdateTarget()"/> once for every binding registered through
        /// <see cref="RegisterFrameBinding(IBinding)"/>.
        /// </summary>
        /// <remarks>
        /// Called once per frame from <see cref="UI.Canvas.Render"/>, alongside <see cref="Update(DispatcherPriority)"/>
        /// for <see cref="DispatcherPriority.DataBind"/>, so <see cref="UpdateTargetTrigger.EveryFrame"/> bindings
        /// refresh at the same point in the frame as reactive ones.
        /// </remarks>
        public void UpdateFrameBindings()
        {
            IBinding[] bindings;
            lock (lockObj)
            {
                if (frameBindings.Count == 0)
                    return;
                bindings = [.. frameBindings];
            }

            foreach (IBinding binding in bindings)
                binding.UpdateTarget();
        }

        /// <summary>
        /// Registers an animation to have <see cref="Animation.Update(TimeSpan)"/> called once per frame by
        /// <see cref="UpdateAnimations(TimeSpan)"/>.
        /// </summary>
        /// <param name="animation">The animation to tick every frame.</param>
        /// <remarks>Called by <see cref="Animation.Start"/> - call <see cref="UnregisterAnimation(Animation)"/> once the animation stops or completes.</remarks>
        public void RegisterAnimation(Animation animation)
        {
            lock (lockObj)
            {
                runningAnimations.Add(animation);
            }
        }

        /// <summary>
        /// Stops calling <see cref="Animation.Update(TimeSpan)"/> once per frame for an animation previously passed
        /// to <see cref="RegisterAnimation(Animation)"/>.
        /// </summary>
        /// <param name="animation">The animation to stop ticking.</param>
        public void UnregisterAnimation(Animation animation)
        {
            lock (lockObj)
            {
                runningAnimations.Remove(animation);
            }
        }

        /// <summary>
        /// Calls <see cref="Animation.Update(TimeSpan)"/> once for every animation registered through
        /// <see cref="RegisterAnimation(Animation)"/>, advancing it by <paramref name="delta"/>.
        /// </summary>
        /// <param name="delta">The amount of time to advance every running animation by.</param>
        /// <remarks>
        /// Called once per frame from <see cref="UI.Canvas.Render"/>, alongside <see cref="Update(DispatcherPriority)"/>
        /// for <see cref="DispatcherPriority.DataBind"/> and <see cref="UpdateFrameBindings"/>, so animations advance
        /// at the same point in the frame as reactive and frame-driven bindings.
        /// </remarks>
        public void UpdateAnimations(TimeSpan delta)
        {
            Animation[] animations;
            lock (lockObj)
            {
                if (runningAnimations.Count == 0)
                    return;
                animations = [.. runningAnimations];
            }

            foreach (Animation animation in animations)
                animation.Update(delta);
        }
    }
}
