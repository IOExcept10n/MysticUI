// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Tests.Input
{
    /// <summary>
    /// A command whose <c>CanExecute</c> and <c>Execute</c> are delegates.
    /// </summary>
    internal sealed class FakeCommand(Func<bool> canExecute, Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter) => execute();
    }
}
