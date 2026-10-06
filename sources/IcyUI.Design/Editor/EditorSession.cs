// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The headless part of the design-time editor for one <see cref="UI.Canvas"/>: the selection, the Edit/Interact
    /// mode, hit resolution, and (through its commands and gesture methods) every edit the editor makes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session works across every <see cref="DesignDocument"/> whose pages are on the canvas. Each gesture and command
    /// becomes <see cref="MarkupEditor"/> operations, so undo, saving and hot reload work as for any other edit.
    /// </para>
    /// <para>
    /// <see cref="Dispose"/> removes only the editor's own footprint: its key bindings, and the focus and navigation state
    /// it changed. Edits are never rolled back; save the documents through <see cref="DesignDocument.Save"/> before or
    /// after detaching.
    /// </para>
    /// <para>The visual frame (<c>EditorFrame</c>) is built on top of a session. Tools such as an outline panel can share the
    /// same session, and therefore the same selection.</para>
    /// </remarks>
    public sealed class EditorSession : IDisposable
    {
        private readonly HashSet<DesignDocument> watched = [];
        private EditorMode mode = EditorMode.Edit;
        private EditorSelection? selection;
        private UIElement? focusBeforeEdit;
        private bool navigationBeforeEdit;
        private bool blocked;
        private bool disposed;

        private EditorSession(DesignSession design, Canvas canvas)
        {
            Design = design;
            Canvas = canvas;
        }

        /// <summary>
        /// Occurs when <see cref="Mode"/> changed.
        /// </summary>
        public event EventHandler? ModeChanged;

        /// <summary>
        /// Occurs when <see cref="Selection"/> changed, including when its live instance was rebuilt.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Occurs when <see cref="IsBlocked"/> or <see cref="BlockedReason"/> changed.
        /// </summary>
        public event EventHandler? BlockedChanged;

        /// <summary>
        /// Gets the design session whose documents this editor edits.
        /// </summary>
        public DesignSession Design { get; }

        /// <summary>
        /// Gets the canvas this editor works on.
        /// </summary>
        public Canvas Canvas { get; }

        /// <summary>
        /// Gets or sets the editor's mode.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>
        /// Entering <see cref="EditorMode.Edit"/> remembers and clears the canvas's focused element, so no text box takes
        /// typing, and sets <see cref="Canvas.IsKeyboardNavigationEnabled"/> to <see langword="false"/>.
        /// </description></item>
        /// <item><description>
        /// Entering <see cref="EditorMode.Interact"/> puts both back; the focus only when that element is still on the canvas.
        /// </description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public EditorMode Mode
        {
            get => mode;
            set
            {
                ThrowIfDisposed();
                if (mode == value)
                    return;

                mode = value;
                if (value == EditorMode.Edit)
                    EnterEdit();
                else
                    LeaveEdit();
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the selected element, or <see langword="null"/>.
        /// </summary>
        public EditorSelection? Selection => selection;

        /// <summary>
        /// Gets a value indicating whether the selected element's document can't take edits right now, because its text
        /// has errors or the live page couldn't follow it (see <see cref="DesignDocument.IsInSync"/>). Selection, undo and
        /// redo still work; gestures and editing commands are refused.
        /// </summary>
        public bool IsBlocked => blocked;

        /// <summary>
        /// Gets why <see cref="IsBlocked"/> is <see langword="true"/>, or <see langword="null"/>.
        /// </summary>
        public string? BlockedReason => !blocked
            ? null
            : selection!.Document.LiveErrors.Count > 0 ? selection.Document.LiveErrors[0].Message : "The markup has errors; fix them first.";

        /// <summary>
        /// Gets the overlays that belong to the editor itself. They never take part in <see cref="HitTest"/>.
        /// </summary>
        internal ISet<UIElement> OwnLayers { get; } = new HashSet<UIElement>();

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="design">The design session tracking the canvas's pages.</param>
        /// <param name="canvas">The canvas to edit.</param>
        /// <returns>The session; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="design"/> or <paramref name="canvas"/> is <see langword="null"/>.</exception>
        public static EditorSession Attach(DesignSession design, Canvas canvas)
        {
            ArgumentNullException.ThrowIfNull(design);
            ArgumentNullException.ThrowIfNull(canvas);

            var session = new EditorSession(design, canvas);
            session.EnterEdit();
            return session;
        }

        /// <summary>
        /// Finds the element under a screen point the way the editor sees the canvas: past the editor's own layers, but
        /// including the page's popups and dialogs.
        /// </summary>
        /// <param name="screenPoint">A point in screen space, where pointer positions arrive.</param>
        /// <returns>The topmost hit element, or <see langword="null"/>.</returns>
        public UIElement? HitTest(Point screenPoint) => Canvas.HitTest(screenPoint, overlay => !OwnLayers.Contains(overlay));

        /// <summary>
        /// Finds the element a click on <paramref name="hit"/> selects: the nearest element, from <paramref name="hit"/>
        /// up through its visual parents, that a tracked document owns and can edit.
        /// </summary>
        /// <param name="hit">The element under the pointer, or <see langword="null"/>.</param>
        /// <returns>The selectable element, or <see langword="null"/> when there's none.</returns>
        /// <remarks>
        /// A control's template parts and an items control's generated containers aren't elements of the document (or
        /// are opaque inside a property element), so a click on them selects the control that owns them.
        /// </remarks>
        public UIElement? ResolveSelectable(UIElement? hit)
        {
            for (UIElement? current = hit; current != null; current = current.Parent)
            {
                if (OwnLayers.Contains(current))
                    return null;
                if (Design.FindDocument(current, out NodeId node) is { } document && document.GetNode(node) is { } element && document.IsEditable(element))
                    return current;
            }

            return null;
        }

        /// <summary>
        /// Selects <paramref name="instance"/>.
        /// </summary>
        /// <param name="instance">A live element.</param>
        /// <returns><see langword="false"/>, changing nothing, when no tracked document can edit it.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public bool Select(UIElement instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ThrowIfDisposed();
            if (Design.FindDocument(instance, out NodeId node) is not { } document || document.GetNode(node) is not { } element || !document.IsEditable(element))
                return false;

            SetSelection(new EditorSelection(document, node, instance));
            return true;
        }

        /// <summary>
        /// Selects a document's element, picking its first live copy on this canvas.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="node">The element's node.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The element has no live copy on this canvas, or can't be edited.</exception>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public void Select(DesignDocument document, NodeId node)
        {
            ArgumentNullException.ThrowIfNull(document);
            ThrowIfDisposed();
            UIElement instance = document.GetObjects(node).OfType<UIElement>().FirstOrDefault(x => ReferenceEquals(x.Canvas, Canvas))
                ?? throw new ArgumentException($"Element {node} has no live copy on this canvas.", nameof(node));
            if (!Select(instance))
                throw new ArgumentException($"Element {node} can't be edited.", nameof(node));
        }

        /// <summary>
        /// Clears the selection.
        /// </summary>
        public void Clear() => SetSelection(null);

        /// <summary>
        /// Detaches the editor: restores the canvas's focus and navigation, and stops watching documents. Edits stay.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            if (mode == EditorMode.Edit)
                LeaveEdit();
            SetSelection(null);
            foreach (DesignDocument document in watched)
                Unwatch(document);
            watched.Clear();
            disposed = true;
        }

        /// <summary>
        /// Gets the selected element's markup.
        /// </summary>
        internal static ElementSyntax? Syntax(EditorSelection selection) => selection.Document.GetNode(selection.Node);

        /// <summary>
        /// Gets the load scope a live instance was built in.
        /// </summary>
        internal static MarkupLoadScope? ScopeOf(DesignDocument document, UIElement instance) =>
            document.Map.TryGetEntry(instance, out var entry) ? entry.Scope : null;

        /// <summary>
        /// Finds <paramref name="node"/>'s live copy in <paramref name="scope"/>.
        /// </summary>
        internal static UIElement? FindInstance(DesignDocument document, NodeId node, MarkupLoadScope scope) =>
            document.FindObject(node, scope) as UIElement;

        /// <summary>
        /// Gets the live object of the selected element's markup parent, in the same load scope: the container that lays it
        /// out, even when a template's presenter is its visual parent.
        /// </summary>
        internal static UIElement? FindLogicalParent(EditorSelection selection)
        {
            if (Syntax(selection)?.Parent is not { } parent
                || selection.Document.GetNodeId(parent) is not NodeId parentId
                || ScopeOf(selection.Document, selection.Instance) is not { } scope)
            {
                return null;
            }

            return FindInstance(selection.Document, parentId, scope);
        }

        internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

        private void SetSelection(EditorSelection? value)
        {
            if (Equals(selection, value))
                return;

            selection = value;
            if (value != null && watched.Add(value.Document))
                Watch(value.Document);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            UpdateBlocked();
        }

        private void EnterEdit()
        {
            focusBeforeEdit = Canvas.FocusedElement;
            navigationBeforeEdit = Canvas.IsKeyboardNavigationEnabled;
            Canvas.Focus(null);
            Canvas.IsKeyboardNavigationEnabled = false;
        }

        private void LeaveEdit()
        {
            Canvas.IsKeyboardNavigationEnabled = navigationBeforeEdit;
            if (focusBeforeEdit is { } focus && ReferenceEquals(focus.Canvas, Canvas))
                Canvas.Focus(focus);
            focusBeforeEdit = null;
        }

        private void Watch(DesignDocument document)
        {
            document.SubtreeReplaced += OnSubtreeReplaced;
            document.Changed += OnDocumentChanged;
            document.SyncStateChanged += OnSyncStateChanged;
        }

        private void Unwatch(DesignDocument document)
        {
            document.SubtreeReplaced -= OnSubtreeReplaced;
            document.Changed -= OnDocumentChanged;
            document.SyncStateChanged -= OnSyncStateChanged;
        }

        private void OnSubtreeReplaced(object? sender, SubtreeReplacedEventArgs e)
        {
            if (selection is { } current && current.Node == e.Node && ReferenceEquals(current.Instance, e.OldElement))
                SetSelection(current with { Instance = e.NewElement });
        }

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
            // A removed element (by an edit, undo or applied text) can't stay selected. While the text is malformed the
            // ids still answer for the last valid tree, so the selection survives until the text is fixed.
            if (selection is { } current && ReferenceEquals(sender, current.Document) && current.Document.GetNode(current.Node) == null)
                SetSelection(null);
        }

        private void OnSyncStateChanged(object? sender, EventArgs e) => UpdateBlocked();

        private void UpdateBlocked()
        {
            bool value = selection is { } current && !current.Document.IsInSync;
            if (value == blocked && !value)
                return;

            blocked = value;
            BlockedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
