// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;
using Icy.Design.Syntax;

namespace Icy.Design.Editor
{
    /// <summary>
    /// Every action of an <see cref="EditorSession"/> as a command. Bind them to keys through
    /// <see cref="EditorSession.Bindings"/>, or invoke them from buttons, gamepads or touch gestures.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>In <see cref="EditorMode.Interact"/>, only <see cref="ToggleMode"/> can execute.</description></item>
    /// <item><description>Without a selection, only <see cref="ToggleMode"/>, <see cref="Undo"/> and <see cref="Redo"/> can execute.</description></item>
    /// <item><description>
    /// While <see cref="EditorSession.IsBlocked"/>, <see cref="Delete"/> and the nudges are refused; selection commands,
    /// <see cref="Undo"/> and <see cref="Redo"/> keep working, since undo is the way back out of broken markup.
    /// </description></item>
    /// </list>
    /// </remarks>
    public sealed class EditorCommands
    {
        private readonly EditorSession session;
        private readonly List<EditorCommand> all = [];

        internal EditorCommands(EditorSession session)
        {
            this.session = session;
            ToggleMode = Create(() => true, () => session.Mode = session.Mode == EditorMode.Edit ? EditorMode.Interact : EditorMode.Edit);
            Deselect = Create(Selecting, session.Clear);
            SelectParent = Create(Selecting, () => Move(x => x.Parent));
            SelectFirstChild = Create(Selecting, () => Move(x => x.ContentElements.FirstOrDefault()));
            SelectPrevious = Create(Selecting, () => Move(x => Sibling(x, -1)));
            SelectNext = Create(Selecting, () => Move(x => Sibling(x, 1)));
            Delete = Create(Editing, () => session.DeleteSelection());
            Undo = Create(() => Editable && Target()?.Editor.UndoStack.CanUndo == true, () => Target()!.Editor.UndoStack.Undo());
            Redo = Create(() => Editable && Target()?.Editor.UndoStack.CanRedo == true, () => Target()!.Editor.UndoStack.Redo());
            NudgeLeft = Nudge(-1, 0, large: false);
            NudgeRight = Nudge(1, 0, large: false);
            NudgeUp = Nudge(0, -1, large: false);
            NudgeDown = Nudge(0, 1, large: false);
            NudgeLeftLarge = Nudge(-1, 0, large: true);
            NudgeRightLarge = Nudge(1, 0, large: true);
            NudgeUpLarge = Nudge(0, -1, large: true);
            NudgeDownLarge = Nudge(0, 1, large: true);
        }

        /// <summary>Gets the command that switches between <see cref="EditorMode.Edit"/> and <see cref="EditorMode.Interact"/>.</summary>
        public ICommand ToggleMode { get; }

        /// <summary>Gets the command that clears the selection.</summary>
        public ICommand Deselect { get; }

        /// <summary>Gets the command that selects the nearest editable ancestor in markup.</summary>
        public ICommand SelectParent { get; }

        /// <summary>Gets the command that selects the first editable content child in markup.</summary>
        public ICommand SelectFirstChild { get; }

        /// <summary>Gets the command that selects the previous editable sibling in markup.</summary>
        public ICommand SelectPrevious { get; }

        /// <summary>Gets the command that selects the next editable sibling in markup.</summary>
        public ICommand SelectNext { get; }

        /// <summary>Gets the command that removes the selected element (never the root).</summary>
        public ICommand Delete { get; }

        /// <summary>Gets the command that undoes the selected document's last step, or the last edited document's when nothing is selected.</summary>
        public ICommand Undo { get; }

        /// <summary>Gets the command that redoes the selected document's last undone step, or the last edited document's when nothing is selected.</summary>
        public ICommand Redo { get; }

        /// <summary>Gets the command that moves the selected element one unit left.</summary>
        public ICommand NudgeLeft { get; }

        /// <summary>Gets the command that moves the selected element one unit right.</summary>
        public ICommand NudgeRight { get; }

        /// <summary>Gets the command that moves the selected element one unit up.</summary>
        public ICommand NudgeUp { get; }

        /// <summary>Gets the command that moves the selected element one unit down.</summary>
        public ICommand NudgeDown { get; }

        /// <summary>Gets the command that moves the selected element left by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeLeftLarge { get; }

        /// <summary>Gets the command that moves the selected element right by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeRightLarge { get; }

        /// <summary>Gets the command that moves the selected element up by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeUpLarge { get; }

        /// <summary>Gets the command that moves the selected element down by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeDownLarge { get; }

        private bool Editable => session.Mode == EditorMode.Edit;

        internal void RaiseCanExecuteChanged()
        {
            foreach (EditorCommand command in all)
                command.RaiseCanExecuteChanged();
        }

        private static ElementSyntax? Sibling(ElementSyntax element, int step)
        {
            if (element.Parent is not { } parent)
                return null;

            List<ElementSyntax> siblings = [.. parent.ContentElements];
            int index = siblings.IndexOf(element) + step;
            return index >= 0 && index < siblings.Count ? siblings[index] : null;
        }

        private bool Selecting() => Editable && session.Selection != null;

        private bool Editing() => Selecting() && !session.IsBlocked;

        private DesignDocument? Target() => session.Selection?.Document ?? session.LastEdited;

        private EditorCommand Create(Func<bool> canExecute, Action execute)
        {
            var command = new EditorCommand(canExecute, execute);
            all.Add(command);
            return command;
        }

        private EditorCommand Nudge(int x, int y, bool large) =>
            Create(Editing, () =>
            {
                int step = large ? session.LargeNudge : 1;
                session.Nudge(x * step, y * step);
            });

        /// <summary>
        /// Moves the selection along the markup tree to the first editable element <paramref name="next"/> reaches.
        /// </summary>
        private void Move(Func<ElementSyntax, ElementSyntax?> next)
        {
            EditorSelection current = session.Selection!;
            if (EditorSession.ScopeOf(current.Document, current.Instance) is not { } scope)
                return;

            // Walks past elements with no editable live copy (an untracked or opaque step) until one selects; the step
            // limit guards against a function that cycles.
            int limit = current.Document.Syntax.Elements.Count();
            ElementSyntax? element = EditorSession.Syntax(current) is { } start ? next(start) : null;
            for (int steps = 0; element != null && steps < limit; steps++, element = next(element))
            {
                if (current.Document.GetNodeId(element) is NodeId id
                    && EditorSession.FindInstance(current.Document, id, scope) is { } live
                    && session.Select(live))
                {
                    return;
                }
            }
        }
    }
}
