// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Markup;
using Icy.Design.Editor.Panels;
using Icy.Markup;
using Icy.Rendering;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The complete editor as a page: a document picker, Outline and Toolbox on the left, a preview of the open document
    /// in the middle, Properties on the right and the command bar on top. Put it on a game's debug or settings page.
    /// </summary>
    /// <remarks>
    /// <para>Using it in your game:</para>
    /// <code>
    /// #if DEBUG
    /// DesignSession design = DesignSession.Attach(configuration);           // before loading any screen
    /// if (DesignSession.FindSourceRoot("MyGame.csproj", "Assets/UI") is { } root)
    ///     design.UseSourceRoot(root);
    /// debugPage.Content = new EditorWorkspace { Design = design };
    /// #endif
    /// </code>
    /// <para>
    /// Reference <c>IcyUI.Design</c> from the game only in Debug builds, so release builds don't carry it:
    /// <c>&lt;ProjectReference Include="..\IcyUI.Design\IcyUI.Design.csproj" Condition="'$(Configuration)' == 'Debug'" /&gt;</c>.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// The picker lists the documents <see cref="Design"/> tracks, with "●" on unsaved ones. "Open file" loads a markup
    /// file the game hasn't loaded yet; the session tracks it from then on.
    /// </description></item>
    /// <item><description>
    /// Opening a document loads its current text again into the preview, under the same source path. The session joins
    /// that copy to the same <see cref="DesignDocument"/>, so every edit made here also reaches the game's live screen,
    /// and both share one undo history. Opening another document releases the previous preview.
    /// </description></item>
    /// <item><description>
    /// The editor attaches to the canvas, scoped to the preview, while a document is open and the workspace is on a
    /// canvas. A canvas takes one editor at a time: while the <see cref="EditorOverlay"/> is shown, the workspace waits
    /// with a notice and attaches once the overlay is hidden.
    /// </description></item>
    /// </list>
    /// </remarks>
    public class EditorWorkspace : ContentControl
    {
        private const string BusyNotice = "The editor overlay is active. Close it to edit here.";

        private readonly EditorCommandBar bar = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly OutlinePanel outline = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        private readonly ToolboxPanel toolbox = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        private readonly PropertiesPanel properties = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        private readonly ListBox picker = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Height = 120 };
        private readonly TextBox pathBox = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBlock status = new() { Margin = new Thickness(6, 2), Foreground = System.Drawing.Color.Salmon };
        private readonly ScrollViewer previewHost = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        private DesignSession? design;
        private EditorFrame? frame;
        private bool pendingAttach;
        private bool attachQueued;
        private bool syncingPicker;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorWorkspace"/> class.
        /// </summary>
        public EditorWorkspace()
        {
            picker.SelectionChanged += OnPickerSelection;

            var openRow = new UI.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 4) };
            openRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            openRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            openRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            openRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Button openButton = CreateButton("Open file", () => OpenFile(pathBox.Text));
            Button refreshButton = CreateButton("Refresh", RefreshDocuments);
            UI.Controls.Grid.SetColumn(openButton, 1);
            UI.Controls.Grid.SetColumn(refreshButton, 2);
            openRow.Children.Add(pathBox);
            openRow.Children.Add(openButton);
            openRow.Children.Add(refreshButton);

            var tabs = new TabControl
            {
                ItemsSource = new List<object>
                {
                    new TabItem { Header = "Outline", Content = outline },
                    new TabItem { Header = "Toolbox", Content = toolbox },
                },
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            var left = new UI.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            UI.Controls.Grid.SetRow(openRow, 1);
            UI.Controls.Grid.SetRow(tabs, 2);
            left.Children.Add(picker);
            left.Children.Add(openRow);
            left.Children.Add(tabs);

            var right = new SplitPane { First = previewHost, Second = properties, SplitterPosition = 0.7f };
            var split = new SplitPane { First = left, Second = right, SplitterPosition = 0.25f };

            var root = new UI.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            UI.Controls.Grid.SetRow(status, 1);
            UI.Controls.Grid.SetRow(split, 2);
            root.Children.Add(bar);
            root.Children.Add(status);
            root.Children.Add(split);
            Content = root;
        }

        /// <summary>
        /// Gets or sets the design session whose documents the workspace edits, or <see langword="null"/> for an empty
        /// workspace.
        /// </summary>
        public DesignSession? Design
        {
            get => design;
            set
            {
                if (ReferenceEquals(design, value))
                    return;
                ReleasePreview();
                CurrentDocument = null;
                design = value;
                RefreshDocuments();
            }
        }

        /// <summary>
        /// Gets the document shown in the preview, or <see langword="null"/>.
        /// </summary>
        public DesignDocument? CurrentDocument { get; private set; }

        /// <summary>
        /// Gets the preview's root element: a second live copy of <see cref="CurrentDocument"/>, or <see langword="null"/>.
        /// </summary>
        public UIElement? Preview { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the editor is attached to the preview.
        /// </summary>
        public bool IsEditing => frame != null;

        /// <summary>
        /// Gets the workspace's notice: why the editor is waiting, or why opening a document failed. Save outcomes are on
        /// the command bar.
        /// </summary>
        public string StatusText => status.Text;

        /// <summary>
        /// Opens <paramref name="document"/>: loads its current text into the preview and attaches the editor to it.
        /// </summary>
        /// <param name="document">A document of <see cref="Design"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException"><see cref="Design"/> isn't set.</exception>
        public void Open(DesignDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            DesignSession session = design ?? throw new InvalidOperationException("Set Design before opening a document.");

            UIElement loaded;
            try
            {
                loaded = new MarkupLoader(session.Configuration).Load(document.Text, document.SourcePath);
            }
            catch (MarkupException ex)
            {
                ReleasePreview();
                status.Text = ex.Message;
                return;
            }

            Show(loaded, document);
        }

        /// <summary>
        /// Loads a markup file the game hasn't loaded yet, and opens the document the session tracks for it.
        /// </summary>
        /// <param name="path">The file's path.</param>
        /// <returns><see langword="false"/> when the file doesn't exist or doesn't load; <see cref="StatusText"/> says why.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Design"/> isn't set.</exception>
        public bool OpenFile(string path)
        {
            DesignSession session = design ?? throw new InvalidOperationException("Set Design before opening a file.");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                status.Text = $"'{path}' doesn't exist.";
                return false;
            }

            UIElement loaded;
            try
            {
                loaded = new MarkupLoader(session.Configuration).Load(File.ReadAllText(path), path);
            }
            catch (Exception ex) when (ex is MarkupException or IOException or UnauthorizedAccessException)
            {
                status.Text = ex.Message;
                return false;
            }

            if (session.FindDocument(loaded, out _) is not { } document)
            {
                status.Text = "The design session didn't track the file.";
                return false;
            }

            Show(loaded, document);
            RefreshDocuments();
            return true;
        }

        /// <summary>
        /// Re-reads the documents <see cref="Design"/> tracks into the picker.
        /// </summary>
        public void RefreshDocuments()
        {
            syncingPicker = true;
            try
            {
                List<DocumentEntry> entries = design == null ? [] : [.. design.Documents.Select(x => new DocumentEntry(x))];
                picker.ItemsSource = entries;
                picker.SelectedItem = entries.FirstOrDefault(x => ReferenceEquals(x.Document, CurrentDocument));
            }
            finally
            {
                syncingPicker = false;
            }
        }

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            RefreshDocuments();
            if (CurrentDocument != null && Preview == null)
                Open(CurrentDocument);
            else
                TryAttach();
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            ReleasePreview();
            base.OnDetached();
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            base.OnRender(context);

            // One lookup per frame, and only while waiting for another editor to leave the canvas. The attach itself
            // runs after this frame's rendering, so the canvas's overlays aren't changed while they're drawn.
            if (pendingAttach && !attachQueued && Canvas is { } canvas && EditorSession.FindAttached(canvas) == null)
            {
                attachQueued = true;
                Dispatcher.GetCurrentThreadDispatcher().Invoke(() =>
                {
                    attachQueued = false;
                    TryAttach();
                });
            }
        }

        private static Button CreateButton(string text, Action action) => new()
        {
            Content = new TextBlock { Text = text },
            Padding = new Thickness(6, 2),
            Margin = new Thickness(4, 0, 0, 0),
            Command = new EditorCommand(() => true, action),
        };

        private void Show(UIElement loaded, DesignDocument document)
        {
            ReleasePreview();
            Preview = loaded;
            previewHost.Content = loaded;
            CurrentDocument = document;
            status.Text = ReferenceEquals(design?.FindDocument(loaded, out _), document) ? string.Empty : "The preview couldn't join the document.";
            RefreshDocuments();
            TryAttach();
        }

        private void TryAttach()
        {
            if (frame != null || design == null || CurrentDocument == null || Canvas is not { } canvas)
                return;

            if (EditorSession.FindAttached(canvas) != null)
            {
                pendingAttach = true;
                status.Text = BusyNotice;
                return;
            }

            try
            {
                frame = EditorFrame.Attach(canvas, design, previewHost);
            }
            catch (ArgumentException ex)
            {
                status.Text = ex.Message;
                return;
            }

            frame.ShowsToolbar = false;
            pendingAttach = false;
            if (status.Text == BusyNotice)
                status.Text = string.Empty;

            EditorSession session = frame.Session;
            bar.Session = session;
            toolbox.Session = session;
            properties.Session = session;
            outline.Session = session;
            outline.Document = CurrentDocument;
        }

        private void ReleasePreview()
        {
            pendingAttach = false;
            if (frame != null)
            {
                bar.Session = null;
                toolbox.Session = null;
                properties.Session = null;
                outline.Session = null;
                frame.Dispose();
                frame = null;
            }

            outline.Document = null;
            previewHost.Content = null;
            Preview = null;
            if (status.Text == BusyNotice)
                status.Text = string.Empty;
        }

        private void OnPickerSelection(object? sender, EventArgs e)
        {
            if (!syncingPicker && picker.SelectedItem is DocumentEntry entry && !ReferenceEquals(entry.Document, CurrentDocument))
                Open(entry.Document);
        }

        /// <summary>
        /// A picker row: the document's source path, marked when it has unsaved changes.
        /// </summary>
        /// <param name="Document">The document.</param>
        private sealed record DocumentEntry(DesignDocument Document)
        {
            /// <inheritdoc/>
            public override string ToString() => (Document.SourcePath ?? "(no source)") + (Document.IsModified ? " ●" : string.Empty);
        }
    }
}
