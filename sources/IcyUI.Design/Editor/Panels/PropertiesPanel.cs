// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// Shows and edits the properties of an <see cref="EditorSession"/>'s selected element. Every edit becomes an undoable
    /// markup change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The panel follows <see cref="EditorSession.SelectionChanged"/>, and refreshes its values whenever the selected
    /// element's document changes, whether from this panel, a drag in the editor frame, an undo or new text. It does no
    /// per-frame work.
    /// </para>
    /// <para>
    /// It listens to the session only while it's on a canvas, so a panel that was removed doesn't keep reacting to edits
    /// (or stay reachable from the session). Adding it back catches up with the current selection.
    /// </para>
    /// <para>
    /// Place it anywhere: in an <see cref="EditorOverlay"/> dock, an <see cref="EditorWorkspace"/>, or your own debug page.
    /// </para>
    /// </remarks>
    public class PropertiesPanel : ContentControl
    {
        private readonly TextBlock header = new() { Margin = new Thickness(6, 4) };
        private readonly TextBlock status = new() { Margin = new Thickness(6, 2), Foreground = System.Drawing.Color.Salmon };
        private EditorSession? session;
        private EditorSession? listening;
        private DesignDocument? watched;
        private MarkupPropertyAdapter? adapter;
        private MarkupValueFormatter? formatter;
        private string? lastError;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertiesPanel"/> class.
        /// </summary>
        public PropertiesPanel()
        {
            Grid = new PropertyGrid();
            Grid.ActiveMessageChanged += (_, _) => ShowStatus();
            var layout = new UI.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var scroller = new ScrollViewer
            {
                Content = Grid,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            UI.Controls.Grid.SetRow(scroller, 1);
            UI.Controls.Grid.SetRow(status, 2);
            layout.Children.Add(header);
            layout.Children.Add(scroller);
            layout.Children.Add(status);
            Content = layout;
            ShowEmpty();
        }

        /// <summary>
        /// Gets the grid the panel shows the properties in.
        /// </summary>
        public PropertyGrid Grid { get; }

        /// <summary>
        /// Gets the status line: the editor's last refusal, or the focused row's validation reason or description, or
        /// empty.
        /// </summary>
        public string StatusText => status.Text;

        /// <summary>
        /// Gets or sets the session whose selection the panel shows, or <see langword="null"/> for an empty panel.
        /// </summary>
        public EditorSession? Session
        {
            get => session;
            set
            {
                if (ReferenceEquals(session, value))
                    return;
                session = value;
                formatter = value == null ? null : new MarkupValueFormatter(value.Design.Configuration.Types.TypeConverter);
                Listen();
            }
        }

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            Listen();
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            base.OnDetached();
            Listen();
        }

        /// <summary>
        /// Subscribes to <see cref="Session"/> while the panel is on a canvas, and unsubscribes otherwise.
        /// </summary>
        private void Listen()
        {
            EditorSession? wanted = Canvas != null ? session : null;
            if (!ReferenceEquals(listening, wanted))
            {
                if (listening != null)
                    listening.SelectionChanged -= OnSelectionChanged;
                listening = wanted;
                if (listening != null)
                    listening.SelectionChanged += OnSelectionChanged;
            }

            Retarget();
        }

        private void OnSelectionChanged(object? sender, EventArgs e) => Retarget();

        private void Retarget()
        {
            Watch(listening?.Selection?.Document);
            SetAdapter(null);
            if (listening?.Selection is not { } selection || formatter == null)
            {
                ShowEmpty();
                return;
            }

            var created = new MarkupPropertyAdapter(selection.Document, selection.Node, formatter);
            Grid.Target = null;
            Grid.ValueAdapter = created;
            SetAdapter(created);
            Grid.Target = selection.Instance;
            string name = selection.Instance.Name is { Length: > 0 } n ? $" \"{n}\"" : string.Empty;
            header.Text = $"{selection.Instance.GetType().Name}{name} — {selection.Document.SourcePath ?? "(no source)"}";
            lastError = null;
            ShowStatus();
        }

        private void ShowEmpty()
        {
            Grid.Target = null;
            header.Text = "Nothing selected";
            lastError = null;
            ShowStatus();
        }

        private void SetAdapter(MarkupPropertyAdapter? value)
        {
            if (adapter != null)
                adapter.ErrorChanged -= OnErrorChanged;
            adapter = value;
            if (adapter != null)
                adapter.ErrorChanged += OnErrorChanged;
        }

        private void Watch(DesignDocument? document)
        {
            if (ReferenceEquals(watched, document))
                return;
            if (watched != null)
                watched.Changed -= OnDocumentChanged;
            watched = document;
            if (watched != null)
                watched.Changed += OnDocumentChanged;
        }

        private void OnErrorChanged(string? error)
        {
            lastError = error;
            ShowStatus();
        }

        // The editor's own refusal (a value with no markup form, a failed edit) wins over the grid's help text.
        private void ShowStatus() => status.Text = lastError ?? Grid.ActiveMessage ?? string.Empty;

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e) => Grid.Refresh();
    }
}
