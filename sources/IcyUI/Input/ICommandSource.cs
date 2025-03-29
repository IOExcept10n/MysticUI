// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;

namespace Icy.Input
{
    /// <summary>
    /// Represents an interface for types that can invoke commands.
    /// </summary>
    public interface ICommandSource
    {
        /// <summary>
        /// Gets the command that will be executed when the class is "invoked".
        /// </summary>
        /// <remarks>
        /// If the command can't be executed according to <see cref="ICommand.CanExecute(object?)"/>,
        /// controls that implement this interface should be disabled.
        /// </remarks>
        ICommand? Command { get; }

        /// <summary>
        /// Gets the command parameter that will be passed to the command when executing.
        /// </summary>
        object? CommandParameter { get; }

        /// <summary>
        /// Called when <see cref="ICommand.CanExecuteChanged"/> is raised.
        /// </summary>
        /// <param name="sender">Command that sent an event.</param>
        /// <param name="e">Args for the event.</param>
        void CanExecuteChanged(object sender, EventArgs e);
    }
}