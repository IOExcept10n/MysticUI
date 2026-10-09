// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// The editor's top bar: mode, undo and redo, Save and Save all, an unsaved-changes badge and a status line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Save writes the selected element's document, or the last edited one when nothing is selected. When that document
    /// can't be saved, the Save button is disabled and the status line says why (see
    /// <see cref="DesignDocument.SaveBlockedReason"/>). A failed write shows its error in the status line instead of
    /// throwing, so the game keeps running.
    /// </para>
    /// <para>
    /// While the selection's document can't take edits (see <see cref="EditorSession.BlockedReason"/>), the status line
    /// shows why, ahead of any save problem.
    /// </para>
    /// <para>
    /// The bar listens to its session from the moment <see cref="Session"/> is set. Set it back to
    /// <see langword="null"/> when the bar is discarded.
    /// </para>
    /// </remarks>
    public class EditorCommandBar : ContentControl
    {
        private readonly HashSet<DesignDocument> subscribed = [];
        private readonly Button modeButton;
        private readonly Button undoButton;
        private readonly Button redoButton;
        private readonly TextBlock modeLabel = new();
        private readonly TextBlock badge = new() { Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = System.Drawing.Color.Gold };
        private readonly TextBlock status = new() { Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly EditorCommand saveCommand;
        private readonly EditorCommand saveAllCommand;
        private EditorSession? session;
        private string? shownBlockedReason;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorCommandBar"/> class.
        /// </summary>
        public EditorCommandBar()
        {
            saveCommand = new EditorCommand(() => CanSave, Save);
            saveAllCommand = new EditorCommand(() => session != null, SaveAll);
            modeButton = CreateButton(modeLabel, null);
            undoButton = CreateButton(new TextBlock { Text = "Undo" }, null);
            redoButton = CreateButton(new TextBlock { Text = "Redo" }, null);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            bar.Children.Add(modeButton);
            bar.Children.Add(undoButton);
            bar.Children.Add(redoButton);
            bar.Children.Add(CreateButton(new TextBlock { Text = "Save" }, saveCommand));
            bar.Children.Add(CreateButton(new TextBlock { Text = "Save all" }, saveAllCommand));
            bar.Children.Add(badge);
            bar.Children.Add(status);
            Content = bar;
            UpdateState();
        }

        /// <summary>
        /// Gets or sets the session the bar works on, or <see langword="null"/> for a disabled bar.
        /// </summary>
        public EditorSession? Session
        {
            get => session;
            set
            {
                if (ReferenceEquals(session, value))
                    return;
                Unsubscribe();
                session = value;
                Subscribe();
                status.Text = string.Empty;
                shownBlockedReason = null;
                UpdateState();
            }
        }

        /// <summary>
        /// Gets the status line's text: the last save's outcome, or why Save is disabled.
        /// </summary>
        public string StatusText => status.Text;

        /// <summary>
        /// Gets a value indicating whether <see cref="Save"/> would write a file.
        /// </summary>
        public bool CanSave => Target is { CanSave: true };

        /// <summary>
        /// Gets a value indicating whether any tracked document has unsaved changes.
        /// </summary>
        public bool HasUnsavedChanges => session?.Design.Documents.Any(x => x.IsModified) == true;

        /// <summary>
        /// Gets the document <see cref="Save"/> writes: the selection's, or the last edited one.
        /// </summary>
        private DesignDocument? Target => session?.Selection?.Document ?? session?.LastEdited;

        /// <summary>
        /// Saves the selected element's document, or the last edited one. Never throws for a failed write; the status line
        /// shows the outcome.
        /// </summary>
        public void Save()
        {
            if (Target is not { } target)
            {
                status.Text = "Nothing to save.";
                return;
            }

            if (target.SaveBlockedReason is { } reason)
            {
                ShowBlocked(reason);
                return;
            }

            try
            {
                target.Save();
                status.Text = $"Saved {target.SourcePath}.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                status.Text = ex.Message;
            }

            shownBlockedReason = null;
            UpdateState();
        }

        /// <summary>
        /// Saves every modified document that can be saved, and reports the ones it skipped in the status line.
        /// </summary>
        public void SaveAll()
        {
            if (session == null)
                return;

            IReadOnlyList<(DesignDocument Document, string Reason)> skipped = session.Design.SaveAll();
            status.Text = skipped.Count switch
            {
                0 => "Saved all.",
                1 => $"1 page not saved: {skipped[0].Reason}",
                _ => $"{skipped.Count} pages not saved: {skipped[0].Reason}",
            };
            shownBlockedReason = null;
            UpdateState();
        }

        private static Button CreateButton(TextBlock label, System.Windows.Input.ICommand? command) => new()
        {
            Content = label,
            Command = command,
            Padding = new Thickness(8, 3),
            Margin = new Thickness(2, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        private void Subscribe()
        {
            if (session == null)
                return;

            session.SelectionChanged += OnSessionChanged;
            session.ModeChanged += OnSessionChanged;
            session.BlockedChanged += OnSessionChanged;
            modeButton.Command = session.Commands.ToggleMode;
            undoButton.Command = session.Commands.Undo;
            redoButton.Command = session.Commands.Redo;
            foreach (DesignDocument document in session.Design.Documents)
                Watch(document);
        }

        private void Unsubscribe()
        {
            if (session != null)
            {
                session.SelectionChanged -= OnSessionChanged;
                session.ModeChanged -= OnSessionChanged;
                session.BlockedChanged -= OnSessionChanged;
            }

            foreach (DesignDocument document in subscribed)
                document.ModifiedChanged -= OnModifiedChanged;
            subscribed.Clear();
            modeButton.Command = null;
            undoButton.Command = null;
            redoButton.Command = null;
        }

        private void Watch(DesignDocument? document)
        {
            if (document != null && subscribed.Add(document))
                document.ModifiedChanged += OnModifiedChanged;
        }

        private void OnSessionChanged(object? sender, EventArgs e)
        {
            Watch(session?.Selection?.Document);
            UpdateState();
        }

        private void OnModifiedChanged(object? sender, EventArgs e) => UpdateState();

        private void ShowBlocked(string reason)
        {
            status.Text = reason;
            shownBlockedReason = reason;
        }

        private void UpdateState()
        {
            Watch(session?.LastEdited);
            modeLabel.Text = session == null ? "Mode" : session.Mode == EditorMode.Edit ? "Edit" : "Interact";
            badge.Text = HasUnsavedChanges ? "● unsaved" : string.Empty;

            string? reason = session?.BlockedReason ?? Target?.SaveBlockedReason;
            if (status.Text.Length == 0 || status.Text == shownBlockedReason)
            {
                if (reason != null)
                {
                    ShowBlocked(reason);
                }
                else if (shownBlockedReason != null)
                {
                    status.Text = string.Empty;
                    shownBlockedReason = null;
                }
            }

            saveCommand.RaiseCanExecuteChanged();
            saveAllCommand.RaiseCanExecuteChanged();
        }
    }
}
