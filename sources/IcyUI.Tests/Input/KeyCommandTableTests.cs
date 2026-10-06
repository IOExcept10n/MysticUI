// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input;
using Icy.Input.Devices;
using Xunit;

namespace Icy.Tests.Input
{
    public class KeyCommandTableTests
    {
        private static readonly KeyGesture Delete = new(Keys.Delete);

        [Fact]
        public void OrdinaryCommands_AllRun()
        {
            var table = new KeyCommandTable();
            int first = 0, second = 0;
            table.Add(new FakeCommand(() => true, () => first++), Delete, null, handlesGesture: false);
            table.Add(new FakeCommand(() => true, () => second++), Delete, null, handlesGesture: false);

            Assert.True(table.Dispatch(Delete));

            Assert.Equal(1, first);
            Assert.Equal(1, second);
        }

        [Fact]
        public void HandledCommand_WinsAndStopsDispatch()
        {
            var table = new KeyCommandTable();
            int game = 0, editor = 0;
            table.Add(new FakeCommand(() => true, () => game++), Delete, null, handlesGesture: false);
            table.Add(new FakeCommand(() => true, () => editor++), Delete, null, handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal(1, editor);
            Assert.Equal(0, game);
        }

        [Fact]
        public void HandledCommand_FallsThroughWhenItCantExecute()
        {
            var table = new KeyCommandTable();
            int game = 0, editor = 0;
            table.Add(new FakeCommand(() => true, () => game++), Delete, null, handlesGesture: false);
            table.Add(new FakeCommand(() => false, () => editor++), Delete, null, handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal(0, editor);
            Assert.Equal(1, game);
        }

        [Fact]
        public void TheLatestHandledRegistration_GoesFirst()
        {
            var table = new KeyCommandTable();
            var order = new List<string>();
            table.Add(new FakeCommand(() => true, () => order.Add("older")), Delete, null, handlesGesture: true);
            table.Add(new FakeCommand(() => true, () => order.Add("newer")), Delete, null, handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal(["newer"], order);
        }

        [Fact]
        public void RemovingOneCommand_KeepsTheOthersOnTheGesture()
        {
            var table = new KeyCommandTable();
            int game = 0;
            var editor = new FakeCommand(() => true, () => { });
            table.Add(new FakeCommand(() => true, () => game++), Delete, null, handlesGesture: false);
            table.Add(editor, Delete, null, handlesGesture: true);

            table.Remove(editor, Delete);
            table.Dispatch(Delete);

            Assert.Equal(1, game);
        }

        [Fact]
        public void TheArgument_IsPassedToTheCommand()
        {
            var table = new KeyCommandTable();
            object? received = null;
            var command = new ArgumentCommand(x => received = x);
            table.Add(command, Delete, "payload", handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal("payload", received);
        }

        private sealed class ArgumentCommand(Action<object?> execute) : System.Windows.Input.ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute(parameter);
        }

        private sealed class FakeCommand(Func<bool> canExecute, Action execute) : System.Windows.Input.ICommand
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
}
