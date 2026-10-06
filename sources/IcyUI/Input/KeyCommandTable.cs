// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;
using Icy.Input.Devices;

namespace Icy.Input
{
    /// <summary>
    /// Maps key gestures to commands. Commands that handle their gesture run first, newest first, and the first one
    /// that can execute stops dispatch; when none can, every ordinary command runs.
    /// </summary>
    internal sealed class KeyCommandTable
    {
        private readonly Dictionary<KeyGesture, List<Registration>> registrations = [];

        /// <summary>
        /// Registers <paramref name="command"/> for <paramref name="gesture"/>.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="gesture">The key combination that triggers it.</param>
        /// <param name="argument">The argument passed to <see cref="ICommand.CanExecute(object?)"/> and <see cref="ICommand.Execute(object?)"/>.</param>
        /// <param name="handlesGesture">Whether the command takes the gesture over (see <see cref="Dispatch"/>).</param>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
        public void Add(ICommand command, KeyGesture gesture, object? argument, bool handlesGesture)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (!registrations.TryGetValue(gesture, out List<Registration>? list))
                registrations[gesture] = list = [];

            list.Add(new Registration(command, argument, handlesGesture));
        }

        /// <summary>
        /// Removes every registration of <paramref name="command"/> for <paramref name="gesture"/>, keeping the gesture's other commands.
        /// </summary>
        /// <param name="command">The command to remove.</param>
        /// <param name="gesture">The key combination it was registered for.</param>
        public void Remove(ICommand command, KeyGesture gesture)
        {
            if (registrations.TryGetValue(gesture, out List<Registration>? list))
            {
                list.RemoveAll(x => ReferenceEquals(x.Command, command));
                if (list.Count == 0)
                    registrations.Remove(gesture);
            }
        }

        /// <summary>
        /// Removes every command registered for <paramref name="gesture"/>.
        /// </summary>
        /// <param name="gesture">The key combination to clear.</param>
        public void RemoveGesture(KeyGesture gesture) => registrations.Remove(gesture);

        /// <summary>
        /// Runs the commands registered for <paramref name="gesture"/>: the gesture-handling ones newest first until one
        /// executes; when none can, every ordinary one that can execute.
        /// </summary>
        /// <param name="gesture">The key combination that was pressed.</param>
        /// <returns><see langword="true"/> if at least one command executed.</returns>
        public bool Dispatch(KeyGesture gesture)
        {
            if (!registrations.TryGetValue(gesture, out List<Registration>? list))
                return false;

            // A command may unregister commands while running, so dispatch over a copy.
            Registration[] snapshot = [.. list];
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                Registration handler = snapshot[i];
                if (handler.HandlesGesture && handler.Command.CanExecute(handler.Argument))
                {
                    handler.Command.Execute(handler.Argument);
                    return true;
                }
            }

            bool executed = false;
            foreach (Registration ordinary in snapshot)
            {
                if (!ordinary.HandlesGesture && ordinary.Command.CanExecute(ordinary.Argument))
                {
                    ordinary.Command.Execute(ordinary.Argument);
                    executed = true;
                }
            }

            return executed;
        }

        private sealed record Registration(ICommand Command, object? Argument, bool HandlesGesture);
    }
}
