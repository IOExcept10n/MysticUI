// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using Icy.Design.Editor.Placement;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The headless part of the design-time editor for one <see cref="UI.Canvas"/>: the selection, the Edit/Interact
    /// mode, hit resolution, and (through <see cref="Commands"/> and the gesture methods) every edit the editor makes.
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
    /// <para>A canvas takes one session at a time; see <see cref="FindAttached"/>.</para>
    /// </remarks>
    public sealed class EditorSession : IDisposable
    {
        private static readonly ConditionalWeakTable<Canvas, EditorSession> AttachedSessions = new();
        private readonly HashSet<DesignDocument> watched = [];
        private EditorMode mode = EditorMode.Edit;
        private EditorSelection? selection;
        private UIElement? focusBeforeEdit;
        private bool navigationBeforeEdit;
        private bool blocked;
        private bool disposed;

        private EditorSession(DesignSession design, Canvas canvas, UIElement? scope)
        {
            Design = design;
            Canvas = canvas;
            Scope = scope;
            Commands = new EditorCommands(this);
            Bindings = new EditorBindings(Commands);
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
        /// Gets the subtree this editor is limited to, or <see langword="null"/> when it edits the whole canvas.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The editor sees only the scope's visible area: its bounds, clipped by every ancestor that
        /// <see cref="UIElement.ClipToBounds">clips</see> and by the canvas surface. <see cref="HitTest"/> finds nothing
        /// outside that area, and <see cref="Select(UIElement)"/> refuses elements that are neither in the scope nor in a
        /// canvas overlay (the page's popups and dialogs).
        /// </para>
        /// <para>The keyboard is not scoped: in <see cref="EditorMode.Edit"/> the editor's key bindings stay canvas-wide.</para>
        /// <para>The scope is fixed for the session's lifetime; dispose the session and attach a new one to change it.</para>
        /// </remarks>
        public UIElement? Scope { get; }

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

                ActiveResize?.Cancel();
                mode = value;
                if (value == EditorMode.Edit)
                    EnterEdit();
                else
                    LeaveEdit();
                ModeChanged?.Invoke(this, EventArgs.Empty);
                Commands.RaiseCanExecuteChanged();
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
        /// Gets the placement strategies, by container type. Register strategies for custom containers here.
        /// </summary>
        public PlacementRegistry Placement { get; } = new();

        /// <summary>
        /// Gets the editor's actions as commands.
        /// </summary>
        public EditorCommands Commands { get; }

        /// <summary>
        /// Gets the key gestures bound to <see cref="Commands"/>. Rebind them before or after attaching.
        /// </summary>
        public EditorBindings Bindings { get; }

        /// <summary>
        /// Gets or sets how far the large nudges move, in layout units. Defaults to 10.
        /// </summary>
        public int LargeNudge { get; set; } = 10;

        /// <summary>
        /// Gets the document the last gesture or command edited, or <see langword="null"/>. Undo and redo use it when
        /// nothing is selected.
        /// </summary>
        public DesignDocument? LastEdited { get; private set; }

        /// <summary>
        /// Gets the overlays that belong to the editor itself. They never take part in <see cref="HitTest"/>.
        /// </summary>
        internal ISet<UIElement> OwnLayers { get; } = new HashSet<UIElement>();

        /// <summary>
        /// Gets the resize in progress. It holds an undo transaction open, so undo, redo and other edits wait for it.
        /// </summary>
        internal ResizeGesture? ActiveResize { get; private set; }

        /// <summary>
        /// Attaches an editor to the whole of <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="design">The design session tracking the canvas's pages.</param>
        /// <param name="canvas">The canvas to edit.</param>
        /// <returns>The session; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="design"/> or <paramref name="canvas"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Another session is attached to <paramref name="canvas"/>.</exception>
        public static EditorSession Attach(DesignSession design, Canvas canvas) => Attach(design, canvas, null);

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, limited to <paramref name="scope"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="design">The design session tracking the canvas's pages.</param>
        /// <param name="canvas">The canvas to edit.</param>
        /// <param name="scope">The subtree to edit (see <see cref="Scope"/>), or <see langword="null"/> for the whole canvas.</param>
        /// <returns>The session; dispose it to detach.</returns>
        /// <remarks>
        /// A canvas takes one session at a time: two would both bind the editor's keys and fight over the focus and
        /// navigation state. Check <see cref="FindAttached"/> first when another tool might hold the canvas.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="design"/> or <paramref name="canvas"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="scope"/> is not on <paramref name="canvas"/>.</exception>
        /// <exception cref="InvalidOperationException">Another session is attached to <paramref name="canvas"/>.</exception>
        public static EditorSession Attach(DesignSession design, Canvas canvas, UIElement? scope)
        {
            ArgumentNullException.ThrowIfNull(design);
            ArgumentNullException.ThrowIfNull(canvas);
            if (scope != null && !ReferenceEquals(scope.Canvas, canvas))
                throw new ArgumentException("The scope must be on the canvas the editor attaches to.", nameof(scope));

            var session = new EditorSession(design, canvas, scope);
            if (!AttachedSessions.TryAdd(canvas, session))
                throw new InvalidOperationException("Another editor session is attached to this canvas; dispose it first.");

            session.EnterEdit();
            session.Bindings.Register(design.Configuration.Input.Events);
            return session;
        }

        /// <summary>
        /// Finds the editor session attached to <paramref name="canvas"/>.
        /// </summary>
        /// <param name="canvas">The canvas.</param>
        /// <returns>The attached session, or <see langword="null"/> when there's none.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="canvas"/> is <see langword="null"/>.</exception>
        public static EditorSession? FindAttached(Canvas canvas)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            return AttachedSessions.TryGetValue(canvas, out EditorSession? session) ? session : null;
        }

        /// <summary>
        /// Finds the element under a screen point the way the editor sees the canvas: past the editor's own layers, but
        /// including the page's popups and dialogs, and only inside the <see cref="Scope"/>'s visible area.
        /// </summary>
        /// <param name="screenPoint">A point in screen space, where pointer positions arrive.</param>
        /// <returns>The topmost hit element, or <see langword="null"/>.</returns>
        public UIElement? HitTest(Point screenPoint)
        {
            if (Scope != null)
            {
                Vector2 surface = AdornerGeometry.ScreenToSurface(screenPoint, Canvas.EffectiveScale);
                if (!Region().Contains((int)MathF.Floor(surface.X), (int)MathF.Floor(surface.Y)))
                    return null;
            }

            return Canvas.HitTest(screenPoint, overlay => !OwnLayers.Contains(overlay));
        }

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
        /// <returns>
        /// <see langword="false"/>, changing nothing, when no tracked document can edit it, or when it's outside the
        /// <see cref="Scope"/> and not in an overlay.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public bool Select(UIElement instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ThrowIfDisposed();
            if (!IsInScope(instance))
                return false;
            if (Design.FindDocument(instance, out NodeId node) is not { } document || document.GetNode(node) is not { } element || !document.IsEditable(element))
                return false;

            SetSelection(new EditorSelection(document, node, instance));
            return true;
        }

        /// <summary>
        /// Selects a document's element, picking its first live copy in the editor's scope on this canvas.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="node">The element's node.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// The element has no live copy in the editor's <see cref="Scope"/> on this canvas, or can't be edited.
        /// </exception>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public void Select(DesignDocument document, NodeId node)
        {
            ArgumentNullException.ThrowIfNull(document);
            ThrowIfDisposed();
            UIElement instance = document.GetObjects(node).OfType<UIElement>().FirstOrDefault(x => ReferenceEquals(x.Canvas, Canvas) && IsInScope(x))
                ?? throw new ArgumentException($"Element {node} has no live copy in this editor's scope.", nameof(node));
            if (!Select(instance))
                throw new ArgumentException($"Element {node} can't be edited.", nameof(node));
        }

        /// <summary>
        /// Clears the selection.
        /// </summary>
        public void Clear() => SetSelection(null);

        /// <summary>
        /// Starts moving the selected element from <paramref name="screenPoint"/>.
        /// </summary>
        /// <param name="screenPoint">Where the pointer went down, in screen space.</param>
        /// <returns>
        /// The gesture, or <see langword="null"/> when nothing is selected, the selection is the root, or the session is
        /// <see cref="IsBlocked"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public MoveGesture? BeginMove(Point screenPoint)
        {
            ThrowIfDisposed();
            ActiveResize?.Cancel();
            if (selection is not { } current || blocked || Syntax(current)?.Parent == null || ScopeOf(current.Document, current.Instance) is not { } scope)
                return null;

            return new MoveGesture(this, current, scope, FindLogicalParent(current), screenPoint);
        }

        /// <summary>
        /// Starts resizing the selected element from one of its handles.
        /// </summary>
        /// <param name="handle">The dragged handle.</param>
        /// <param name="screenPoint">Where the pointer went down, in screen space.</param>
        /// <returns>The gesture, or <see langword="null"/> when nothing is selected or the session is <see cref="IsBlocked"/>.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public ResizeGesture? BeginResize(ResizeHandle handle, Point screenPoint)
        {
            ThrowIfDisposed();
            ActiveResize?.Cancel();
            if (selection is not { } current || blocked || handle == ResizeHandle.None)
                return null;

            // The root has no markup parent; its canvas slot works like a generic panel.
            UIElement container = FindLogicalParent(current) ?? (UIElement?)current.Instance.Parent ?? current.Instance;
            var context = new PlacementContext(container, current.Instance, [], 0, PlacementContext.ToLocal(container, current.Instance.ActualBounds), isCurrentContainer: true);
            IResizeOperation operation = ReferenceEquals(container, current.Instance)
                ? new MarginPlacement().BeginResize(context, handle)
                : Placement.Resolve(container).BeginResize(context, handle);
            ActiveResize = new ResizeGesture(this, current, container, operation, screenPoint);
            Commands.RaiseCanExecuteChanged();
            return ActiveResize;
        }

        /// <summary>
        /// Moves the selected element by its margin, alignment-aware, in any container.
        /// </summary>
        /// <param name="dx">The horizontal offset, in layout units.</param>
        /// <param name="dy">The vertical offset, in layout units.</param>
        /// <returns>The outcome; a failure when nothing is selected or the session is <see cref="IsBlocked"/>.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public EditResult Nudge(int dx, int dy)
        {
            ThrowIfDisposed();
            if (selection is not { } current)
                return EditResult.Failure(default, "Nothing is selected.");
            if (blocked)
                return EditResult.Failure(default, BlockedReason!);
            if (ActiveResize != null)
                return EditResult.Failure(default, "A resize is in progress.");

            Thickness margin = LayoutMath.Move(current.Instance, current.Instance.Margin, dx, dy);
            using var gesture = new GestureEdits(current.Document, "Nudge");
            EditResult result = gesture.Apply(current.Node, [new AttributeEdit("Margin", MarkupValues.Format(margin))]);
            if (result.Succeeded)
                NoteEdited(current.Document);
            else
                gesture.Rollback();
            return result;
        }

        /// <summary>
        /// Removes the selected element and clears the selection.
        /// </summary>
        /// <returns>The outcome; a failure for the root, when nothing is selected, or while <see cref="IsBlocked"/>.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public EditResult DeleteSelection()
        {
            ThrowIfDisposed();
            if (selection is not { } current)
                return EditResult.Failure(default, "Nothing is selected.");
            if (blocked)
                return EditResult.Failure(default, BlockedReason!);
            if (ActiveResize != null)
                return EditResult.Failure(default, "A resize is in progress.");

            EditResult result = current.Document.Editor.RemoveElement(current.Node);
            if (result.Succeeded)
                NoteEdited(current.Document);
            return result;
        }

        /// <summary>
        /// Detaches the editor: restores the canvas's focus and navigation, and stops watching documents. Edits stay.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            ActiveResize?.Cancel();
            if (mode == EditorMode.Edit)
                LeaveEdit();
            SetSelection(null);
            foreach (DesignDocument document in watched)
                Unwatch(document);
            watched.Clear();
            Bindings.Unregister();
            if (AttachedSessions.TryGetValue(Canvas, out EditorSession? attached) && ReferenceEquals(attached, this))
                AttachedSessions.Remove(Canvas);
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

        internal void NoteEdited(DesignDocument document) => LastEdited = document;

        /// <summary>
        /// Forgets <paramref name="gesture"/> as the active resize once it completed or was cancelled.
        /// </summary>
        internal void EndResize(ResizeGesture gesture)
        {
            if (!ReferenceEquals(ActiveResize, gesture))
                return;

            ActiveResize = null;
            Commands.RaiseCanExecuteChanged();
        }

        internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

        /// <summary>
        /// Gets the <see cref="Scope"/>'s visible area in surface units: its bounds clipped by every clipping ancestor and
        /// by the canvas surface. Without a scope, the whole surface; empty while the scope is off the canvas or hidden.
        /// </summary>
        internal Rectangle Region()
        {
            var surface = new Rectangle(Point.Empty, Canvas.SurfaceSize);
            if (Scope is not { } scope)
                return surface;
            if (!ReferenceEquals(scope.Canvas, Canvas))
                return Rectangle.Empty;

            for (UIElement? current = scope; current != null; current = current.Parent)
            {
                if (!current.IsVisible)
                    return Rectangle.Empty;
            }

            Rectangle region = Rectangle.Intersect(surface, Rectangle.Round(AdornerGeometry.SurfaceBounds(scope)));
            for (UIElement? ancestor = scope.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor.ClipToBounds)
                    region = Rectangle.Intersect(region, Rectangle.Round(AdornerGeometry.SurfaceBounds(ancestor)));
            }

            return region.Width > 0 && region.Height > 0 ? region : Rectangle.Empty;
        }

        /// <summary>
        /// Gets whether the editor may select <paramref name="element"/>: it's in the <see cref="Scope"/>, or in one of the
        /// canvas's overlays (the page's popups and dialogs), or there's no scope.
        /// </summary>
        internal bool IsInScope(UIElement element)
        {
            if (Scope == null)
                return true;

            UIElement root = element;
            for (UIElement? current = element; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, Scope))
                    return true;
                root = current;
            }

            return Canvas.Overlays.Contains(root);
        }

        private static bool IsInside(UIElement element, UIElement ancestor)
        {
            for (UIElement? current = element.Parent; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
            }

            return false;
        }

        private void SetSelection(EditorSelection? value)
        {
            if (Equals(selection, value))
                return;

            selection = value;
            if (value != null && watched.Add(value.Document))
                Watch(value.Document);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            UpdateBlocked();
            Commands.RaiseCanExecuteChanged();
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
            if (selection is not { } current || !ReferenceEquals(sender, current.Document))
                return;

            if (current.Node == e.Node && ReferenceEquals(current.Instance, e.OldElement))
            {
                SetSelection(current with { Instance = e.NewElement });
                return;
            }

            // An ancestor was rebuilt: the selected copy went with it, so pick the node's copy in the new subtree.
            if (!IsInside(current.Instance, e.OldElement))
                return;

            UIElement? rebuilt = ScopeOf(current.Document, e.NewElement) is { } scope ? FindInstance(current.Document, current.Node, scope) : null;
            SetSelection(rebuilt != null ? current with { Instance = rebuilt } : null);
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
            Commands.RaiseCanExecuteChanged();
        }
    }
}
