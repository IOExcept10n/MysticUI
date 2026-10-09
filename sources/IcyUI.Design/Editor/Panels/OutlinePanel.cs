// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using Icy.Data;
using Icy.Design.Syntax;
using Icy.Input.Devices;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// Where <see cref="OutlinePanel.Move"/> puts an element relative to the target row.
    /// </summary>
    public enum OutlineDropPosition
    {
        /// <summary>
        /// Before the target, in the target's parent.
        /// </summary>
        Before,

        /// <summary>
        /// After the target, in the target's parent.
        /// </summary>
        After,

        /// <summary>
        /// Inside the target, as its last content element.
        /// </summary>
        Inside,
    }

    /// <summary>
    /// Shows a document's element tree and keeps its selection in step with an <see cref="EditorSession"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree is built from the document's markup, not from the live visuals, so elements whose live copy failed to
    /// update still appear (see <see cref="OutlineItem.IsHealthy"/>).
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Document"/> follows the session's selection, and can be set explicitly to show another tracked document.
    /// </description></item>
    /// <item><description>
    /// Selecting a row selects its element in the session; selecting in the editor frame reveals and selects the row.
    /// </description></item>
    /// <item><description>
    /// Every document change updates the tree in place, once per layout pass however many edits arrived (and not at
    /// all while the panel is hidden): rows are kept per element, so expansion and selection survive
    /// edits. Switching to another document starts from fresh rows, so nothing of the previous document is reused;
    /// switching back restores which of its elements were expanded.
    /// </description></item>
    /// <item><description>
    /// While the tree has focus, Ctrl+D runs <see cref="Duplicate"/>. Delete is the editor's own binding
    /// (<see cref="EditorCommands.Delete"/>).
    /// </description></item>
    /// </list>
    /// <para>
    /// The panel listens to the session and the document only while it's on a canvas, and catches up when it's added
    /// back.
    /// </para>
    /// </remarks>
    public class OutlinePanel : ContentControl
    {
        private readonly Dictionary<NodeId, OutlineItem> items = [];
        private readonly ObservableCollection<OutlineItem> roots = [];
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DesignDocument, HashSet<NodeId>> expansion = new();
        private EditorSession? session;
        private EditorSession? listening;
        private DesignDocument? document;
        private DesignDocument? watched;
        private IKeyboardInput? keyboard;
        private bool syncing;
        private bool dirty;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutlinePanel"/> class.
        /// </summary>
        public OutlinePanel()
        {
            Tree = new TreeView
            {
                ItemsSource = roots,
                ChildrenSelector = static x => ((OutlineItem)x).Children,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            Tree.SelectionChanged += OnTreeSelection;
            Content = Tree;
        }

        /// <summary>
        /// Gets the tree the outline is shown in.
        /// </summary>
        public TreeView Tree { get; }

        /// <summary>
        /// Gets the top-level rows: the document's root element, or nothing.
        /// </summary>
        public IReadOnlyList<OutlineItem> Roots
        {
            get
            {
                EnsureBuilt();
                return roots;
            }
        }

        /// <summary>
        /// Gets or sets the session whose selection the panel follows and changes, or <see langword="null"/>.
        /// </summary>
        public EditorSession? Session
        {
            get => session;
            set
            {
                if (ReferenceEquals(session, value))
                    return;
                session = value;
                Listen(followSelection: true);
            }
        }

        /// <summary>
        /// Gets or sets the document the outline shows.
        /// </summary>
        /// <remarks>
        /// It changes to the selected element's document whenever the session's selection moves to another document.
        /// </remarks>
        public DesignDocument? Document
        {
            get => document;
            set
            {
                if (ReferenceEquals(document, value))
                    return;
                if (document != null)
                    expansion.AddOrUpdate(document, [.. items.Where(x => Tree.IsExpanded(x.Value)).Select(x => x.Key)]);
                document = value;
                items.Clear();
                roots.Clear();
                Listen(followSelection: false);
                if (document != null && expansion.TryGetValue(document, out HashSet<NodeId>? expanded))
                {
                    foreach (NodeId node in expanded)
                    {
                        if (items.TryGetValue(node, out OutlineItem? item))
                            Tree.Expand(item);
                    }
                }
            }
        }

        internal int RebuildCount { get; private set; }

        /// <summary>
        /// Inserts a copy of the selected element right after it. Names (<c>x:Name</c>) are left out of the copy, so the
        /// page stays loadable.
        /// </summary>
        /// <returns>The edit's result; a failure when nothing below the root is selected.</returns>
        public EditResult Duplicate()
        {
            if (Session?.Selection is not { } selection
                || selection.Document.GetNode(selection.Node) is not { } element
                || element.Parent is not { } parent
                || selection.Document.GetNodeId(parent) is not { } parentId)
            {
                return EditResult.Failure(default, "Select an element below the root to duplicate it.");
            }

            DesignDocument owner = selection.Document;
            string text = owner.Text;
            int start = element.Span.Start;
            string copy = text.Substring(start, element.Span.Length);

            // Remove every x:Name of the copied subtree, last first so earlier offsets stay valid, with the whitespace
            // that separated it from the previous attribute.
            var names = element.DescendantsAndSelf()
                .Select(x => x.FindAttribute("x:Name"))
                .OfType<AttributeSyntax>()
                .OrderByDescending(x => x.Span.Start);
            foreach (AttributeSyntax name in names)
            {
                int from = name.Span.Start;
                while (from > start && char.IsWhiteSpace(text[from - 1]))
                    from--;
                copy = copy.Remove(from - start, name.Span.End - from);
            }

            int index = parent.ContentElements.ToList().IndexOf(element) + 1;
            return owner.Editor.InsertElement(parentId, index, copy);
        }

        /// <summary>
        /// Moves <paramref name="item"/>'s element next to or into <paramref name="target"/>'s element.
        /// </summary>
        /// <param name="item">The row to move.</param>
        /// <param name="target">The row to drop it on.</param>
        /// <param name="position">Where relative to <paramref name="target"/>.</param>
        /// <returns>The edit's result; a failure for a move into itself, a descendant, or beside the root.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> or <paramref name="target"/> is <see langword="null"/>.</exception>
        public EditResult Move(OutlineItem item, OutlineItem target, OutlineDropPosition position)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(target);
            if (document == null
                || document.GetNode(item.Node) is not { } element
                || document.GetNode(target.Node) is not { } destination)
            {
                return EditResult.Failure(default, "The rows don't belong to the shown document.");
            }

            if (ReferenceEquals(element, destination) || element.IsAncestorOf(destination))
                return EditResult.Failure(destination.NameSpan, "An element can't be moved into itself.");

            if (position == OutlineDropPosition.Inside)
                return document.Editor.MoveElement(item.Node, target.Node, destination.ContentElements.Count(x => !ReferenceEquals(x, element)));

            if (destination.Parent is not { } parent || document.GetNodeId(parent) is not { } parentId)
                return EditResult.Failure(destination.NameSpan, "Nothing can be placed beside the root element.");

            List<ElementSyntax> siblings = [.. parent.ContentElements.Where(x => !ReferenceEquals(x, element))];
            int index = siblings.IndexOf(destination) + (position == OutlineDropPosition.After ? 1 : 0);
            return document.Editor.MoveElement(item.Node, parentId, index);
        }

        /// <inheritdoc/>
        /// <remarks>Brings the tree up to date with the document's edits since the last layout pass.</remarks>
        protected override System.Drawing.Size MeasureContent()
        {
            EnsureBuilt();
            return base.MeasureContent();
        }

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            Listen(followSelection: true);
        }

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            base.OnDetached();
            Listen(followSelection: false);
        }

        private static string LabelOf(ElementSyntax element)
        {
            string? name = element.FindAttribute("x:Name")?.Value ?? element.FindAttribute("Name")?.Value;
            return name is { Length: > 0 } ? $"{element.LocalName} \"{name}\"" : element.LocalName;
        }

        /// <summary>
        /// Makes <paramref name="target"/> equal to <paramref name="desired"/> with few collection changes, so the tree
        /// keeps its realized rows.
        /// </summary>
        private static void Sync(ObservableCollection<OutlineItem> target, IReadOnlyList<OutlineItem> desired)
        {
            for (int i = 0; i < desired.Count; i++)
            {
                if (i < target.Count && ReferenceEquals(target[i], desired[i]))
                    continue;
                int existing = target.IndexOf(desired[i]);
                if (existing > i)
                    target.Move(existing, i);
                else
                    target.Insert(i, desired[i]);
            }

            while (target.Count > desired.Count)
                target.RemoveAt(target.Count - 1);
        }

        private static bool IsWithin(UIElement ancestor, UIElement? element)
        {
            for (UIElement? current = element; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Subscribes to the session, the document and the keyboard while the panel is on a canvas, unsubscribes
        /// otherwise, and brings the tree up to date.
        /// </summary>
        /// <param name="followSelection">
        /// Whether to switch to the selection's document first. An explicit <see cref="Document"/> assignment passes
        /// <see langword="false"/>, so it isn't undone while the selection sits in another document.
        /// </param>
        private void Listen(bool followSelection)
        {
            bool attached = Canvas != null;

            EditorSession? wantedSession = attached ? session : null;
            if (!ReferenceEquals(listening, wantedSession))
            {
                if (listening != null)
                    listening.SelectionChanged -= OnSelectionChanged;
                listening = wantedSession;
                if (listening != null)
                    listening.SelectionChanged += OnSelectionChanged;
            }

            IKeyboardInput? wantedKeyboard = attached ? Configuration?.Input.Keyboard : null;
            if (!ReferenceEquals(keyboard, wantedKeyboard))
            {
                if (keyboard != null)
                    keyboard.KeyDown -= OnKeyDown;
                keyboard = wantedKeyboard;
                if (keyboard != null)
                    keyboard.KeyDown += OnKeyDown;
            }

            // Following the selection switches the document, whose setter calls back into Listen and finishes the job.
            if (followSelection && listening?.Selection is { } selection && !ReferenceEquals(selection.Document, document))
            {
                Document = selection.Document;
                return;
            }

            DesignDocument? wantedDocument = attached ? document : null;
            if (!ReferenceEquals(watched, wantedDocument))
            {
                if (watched != null)
                {
                    watched.Changed -= OnDocumentChanged;
                    watched.SyncStateChanged -= OnSyncStateChanged;
                }

                watched = wantedDocument;
                if (watched != null)
                {
                    watched.Changed += OnDocumentChanged;
                    watched.SyncStateChanged += OnSyncStateChanged;
                }
            }

            Rebuild();
            SelectRow();
        }

        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            if (listening?.Selection is { } selection && !ReferenceEquals(selection.Document, document))
                Document = selection.Document;
            else
                SelectRow();
        }

        private void SelectRow()
        {
            EnsureBuilt();
            object? row = listening?.Selection is { } selection && ReferenceEquals(selection.Document, document)
                ? items.GetValueOrDefault(selection.Node)
                : null;
            if (ReferenceEquals(Tree.SelectedItem, row))
                return;

            syncing = true;
            try
            {
                Tree.SelectedItem = row;
            }
            finally
            {
                syncing = false;
            }
        }

        private void OnTreeSelection(object? sender, EventArgs e)
        {
            if (syncing || document == null || listening == null || Tree.SelectedItem is not OutlineItem item)
                return;

            try
            {
                listening.Select(document, item.Node);
            }
            catch (ArgumentException)
            {
                // An element with no live copy in the editor's scope can't be selected; the row stays highlighted.
            }
        }

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
            // Rebuilding walks the whole element tree, so a burst of edits (a drag, typing) waits for the next layout pass
            // and is handled once.
            dirty = true;
            InvalidateMeasure();
        }

        private void EnsureBuilt()
        {
            if (!dirty)
                return;

            Rebuild();
            SelectRow();
        }

        private void OnSyncStateChanged(object? sender, EventArgs e)
        {
            bool healthy = document?.IsInSync ?? true;
            foreach (OutlineItem item in items.Values)
                item.IsHealthy = healthy;
        }

        private void OnKeyDown(object? sender, GenericEventArgs<Keys> e)
        {
            if (e.Data == Keys.D && keyboard?.ModifierKeys == ModifierKeys.Ctrl && IsWithin(Tree, Canvas?.FocusedElement))
                Duplicate();
        }

        private void Rebuild()
        {
            RebuildCount++;
            dirty = false;
            if (document?.Syntax.Root is not { } root)
            {
                roots.Clear();
                items.Clear();
                return;
            }

            var alive = new HashSet<NodeId>();
            OutlineItem? top = Build(root, alive, document.IsInSync);
            Sync(roots, top == null ? [] : [top]);
            foreach (NodeId gone in items.Keys.Where(x => !alive.Contains(x)).ToList())
                items.Remove(gone);
        }

        private OutlineItem? Build(ElementSyntax element, HashSet<NodeId> alive, bool healthy)
        {
            if (document!.GetNodeId(element) is not { } id)
                return null;

            alive.Add(id);
            string label = LabelOf(element);
            if (!items.TryGetValue(id, out OutlineItem? item))
                items[id] = item = new OutlineItem(id, label);
            item.Label = label;
            item.IsHealthy = healthy;

            var children = new List<OutlineItem>();
            foreach (ElementSyntax child in element.ContentElements)
            {
                if (Build(child, alive, healthy) is { } built)
                    children.Add(built);
            }

            Sync(item.Children, children);
            return item;
        }
    }
}
