// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editor;
using Icy.Input.Devices;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorCommandTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
              <StackPanel x:Name="group">
                <Border x:Name="first" Width="10" Height="10"/>
                <Border x:Name="second" Width="10" Height="10"/>
              </StackPanel>
            </StackPanel>
            """;

        [Fact]
        public void TheDefaultBindings_DriveTheCommands()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));

            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Down, ModifierKeys.Shift));

            Assert.Contains("Margin=\"1,10,0,0\"", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void SelectionCommands_WalkTheMarkupTree()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<StackPanel>("group"));

            Execute(host.Session.Commands.SelectFirstChild);
            Assert.Same(host.Named<Border>("first"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.SelectNext);
            Assert.Same(host.Named<Border>("second"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.SelectPrevious);
            Assert.Same(host.Named<Border>("first"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.SelectParent);
            Assert.Same(host.Named<StackPanel>("group"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.Deselect);
            Assert.Null(host.Session.Selection);
        }

        [Fact]
        public void Delete_And_Undo_Work_ThroughCommands()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            string before = host.Document.Text;

            Execute(host.Session.Commands.Delete);
            Assert.DoesNotContain("x:Name=\"a\"", host.Document.Text, StringComparison.Ordinal);

            // Nothing is selected now: undo uses the last edited document.
            Execute(host.Session.Commands.Undo);
            Assert.Equal(before, host.Document.Text);
        }

        [Fact]
        public void InteractMode_DisablesEverythingButTheToggle()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            host.Session.Mode = EditorMode.Interact;

            Assert.False(host.Session.Commands.Delete.CanExecute(null));
            Assert.False(host.Session.Commands.NudgeLeft.CanExecute(null));
            Assert.True(host.Session.Commands.ToggleMode.CanExecute(null));

            host.Input.Events.RaiseGesture(new KeyGesture(Keys.E, ModifierKeys.Ctrl | ModifierKeys.Shift));
            Assert.Equal(EditorMode.Edit, host.Session.Mode);
        }

        [Fact]
        public void WhileBlocked_EditingIsRefused_ButUndoAndSelectionWork()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);

            Assert.False(host.Session.Commands.Delete.CanExecute(null));
            Assert.False(host.Session.Commands.NudgeRight.CanExecute(null));
            Assert.True(host.Session.Commands.Undo.CanExecute(null));
            Assert.True(host.Session.Commands.Deselect.CanExecute(null));

            Execute(host.Session.Commands.Undo);
            Assert.False(host.Session.IsBlocked);
        }

        [Fact]
        public void EditorBindings_WinOverTheGamesBindingInEditMode()
        {
            using var host = new EditorTestHost(Page);
            int game = 0;
            var gameDelete = new FakeCommand(() => true, () => game++);
            host.Input.Events.RegisterCommand(gameDelete, new KeyGesture(Keys.Delete));
            host.Session.Select(host.Named<Border>("a"));

            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete));
            Assert.Equal(0, game);

            host.Session.Mode = EditorMode.Interact;
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete));
            Assert.Equal(1, game);
        }

        [Fact]
        public void Dispose_LeavesTheGamesOwnBindingsInPlace()
        {
            var host = new EditorTestHost(Page);
            int game = 0;
            host.Input.Events.RegisterCommand(new FakeCommand(() => true, () => game++), new KeyGesture(Keys.Delete));

            host.Dispose();
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete));

            Assert.Equal(1, game);
        }

        [Fact]
        public void RebindingWhileAttached_TakesEffectImmediately()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            var gesture = new KeyGesture(Keys.D, ModifierKeys.Ctrl);

            host.Session.Bindings.Bind(gesture, host.Session.Commands.Delete);
            host.Input.Events.RaiseGesture(gesture);

            Assert.DoesNotContain("x:Name=\"a\"", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void TheDefaults_AvoidF1AndF2()
        {
            using var host = new EditorTestHost(Page);

            Assert.Null(host.Session.Bindings[new KeyGesture(Keys.F1)]);
            Assert.Null(host.Session.Bindings[new KeyGesture(Keys.F2)]);
            Assert.Same(host.Session.Commands.ToggleMode, host.Session.Bindings[new KeyGesture(Keys.E, ModifierKeys.Ctrl | ModifierKeys.Shift)]);
        }

        private static void Execute(System.Windows.Input.ICommand command)
        {
            Assert.True(command.CanExecute(null));
            command.Execute(null);
        }
    }
}
