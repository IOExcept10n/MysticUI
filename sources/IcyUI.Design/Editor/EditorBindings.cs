// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Windows.Input;
using Icy.Input;
using Icy.Input.Devices;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The key gestures an <see cref="EditorSession"/> registers for its <see cref="EditorCommands"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gestures are registered with <c>handlesGesture: true</c> (see
    /// <see cref="IInputEventSystem.RegisterCommand(ICommand, KeyGesture, object?, bool)"/>), so in
    /// <see cref="EditorMode.Edit"/> the editor's commands win over a game's own bindings for the same keys, and in
    /// <see cref="EditorMode.Interact"/> (when they can't execute) the game gets the keys back.
    /// </para>
    /// <para>The defaults:</para>
    /// <list type="table">
    /// <item><term>Ctrl+Shift+E</term><description><see cref="EditorCommands.ToggleMode"/>. F1/F2 are left to the game's debug tools.</description></item>
    /// <item><term>Esc</term><description><see cref="EditorCommands.Deselect"/></description></item>
    /// <item><term>Alt+Up / Alt+Down</term><description><see cref="EditorCommands.SelectParent"/> / <see cref="EditorCommands.SelectFirstChild"/></description></item>
    /// <item><term>Alt+Left / Alt+Right</term><description><see cref="EditorCommands.SelectPrevious"/> / <see cref="EditorCommands.SelectNext"/></description></item>
    /// <item><term>Delete</term><description><see cref="EditorCommands.Delete"/></description></item>
    /// <item><term>Ctrl+Z</term><description><see cref="EditorCommands.Undo"/></description></item>
    /// <item><term>Ctrl+Y, Ctrl+Shift+Z</term><description><see cref="EditorCommands.Redo"/></description></item>
    /// <item><term>Arrows / Shift+Arrows</term><description>The nudges, by 1 and by <see cref="EditorSession.LargeNudge"/>.</description></item>
    /// </list>
    /// <para>Changes made while the session is attached take effect immediately.</para>
    /// </remarks>
    public sealed class EditorBindings : IEnumerable<KeyValuePair<KeyGesture, ICommand>>
    {
        private readonly Dictionary<KeyGesture, ICommand> bindings = [];
        private IInputEventSystem? registered;

        internal EditorBindings(EditorCommands commands)
        {
            Bind(new KeyGesture(Keys.E, ModifierKeys.Ctrl | ModifierKeys.Shift), commands.ToggleMode);
            Bind(new KeyGesture(Keys.Escape), commands.Deselect);
            Bind(new KeyGesture(Keys.Up, ModifierKeys.Alt), commands.SelectParent);
            Bind(new KeyGesture(Keys.Down, ModifierKeys.Alt), commands.SelectFirstChild);
            Bind(new KeyGesture(Keys.Left, ModifierKeys.Alt), commands.SelectPrevious);
            Bind(new KeyGesture(Keys.Right, ModifierKeys.Alt), commands.SelectNext);
            Bind(new KeyGesture(Keys.Delete), commands.Delete);
            Bind(new KeyGesture(Keys.Z, ModifierKeys.Ctrl), commands.Undo);
            Bind(new KeyGesture(Keys.Y, ModifierKeys.Ctrl), commands.Redo);
            Bind(new KeyGesture(Keys.Z, ModifierKeys.Ctrl | ModifierKeys.Shift), commands.Redo);
            Bind(new KeyGesture(Keys.Left), commands.NudgeLeft);
            Bind(new KeyGesture(Keys.Right), commands.NudgeRight);
            Bind(new KeyGesture(Keys.Up), commands.NudgeUp);
            Bind(new KeyGesture(Keys.Down), commands.NudgeDown);
            Bind(new KeyGesture(Keys.Left, ModifierKeys.Shift), commands.NudgeLeftLarge);
            Bind(new KeyGesture(Keys.Right, ModifierKeys.Shift), commands.NudgeRightLarge);
            Bind(new KeyGesture(Keys.Up, ModifierKeys.Shift), commands.NudgeUpLarge);
            Bind(new KeyGesture(Keys.Down, ModifierKeys.Shift), commands.NudgeDownLarge);
        }

        /// <summary>
        /// Gets the command bound to <paramref name="gesture"/>, or <see langword="null"/>.
        /// </summary>
        /// <param name="gesture">The key gesture.</param>
        /// <returns>The bound command, or <see langword="null"/>.</returns>
        public ICommand? this[KeyGesture gesture] => bindings.GetValueOrDefault(gesture);

        /// <summary>
        /// Binds <paramref name="gesture"/> to <paramref name="command"/>, replacing any earlier binding for it.
        /// </summary>
        /// <param name="gesture">The key gesture.</param>
        /// <param name="command">The command; usually one of <see cref="EditorSession.Commands"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
        public void Bind(KeyGesture gesture, ICommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            Unbind(gesture);
            bindings[gesture] = command;
            registered?.RegisterCommand(command, gesture, null, handlesGesture: true);
        }

        /// <summary>
        /// Removes the binding for <paramref name="gesture"/>.
        /// </summary>
        /// <param name="gesture">The key gesture.</param>
        /// <returns><see langword="true"/> when a binding was removed.</returns>
        public bool Unbind(KeyGesture gesture)
        {
            if (!bindings.Remove(gesture, out ICommand? old))
                return false;

            registered?.UnregisterCommand(old, gesture);
            return true;
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<KeyGesture, ICommand>> GetEnumerator() => bindings.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal void Register(IInputEventSystem events)
        {
            registered = events;
            foreach ((KeyGesture gesture, ICommand command) in bindings)
                events.RegisterCommand(command, gesture, null, handlesGesture: true);
        }

        internal void Unregister()
        {
            if (registered == null)
                return;

            foreach ((KeyGesture gesture, ICommand command) in bindings)
                registered.UnregisterCommand(command, gesture);
            registered = null;
        }
    }
}
