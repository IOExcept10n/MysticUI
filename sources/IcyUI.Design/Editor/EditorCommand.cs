// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;

namespace Icy.Design.Editor
{
    /// <summary>
    /// An editor action as an <see cref="ICommand"/>, so keys, gamepads and toolbar buttons all invoke it the same way.
    /// </summary>
    internal sealed class EditorCommand(Func<bool> canExecute, Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter)
        {
            if (canExecute())
                execute();
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
