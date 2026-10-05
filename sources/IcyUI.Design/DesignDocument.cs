// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.Design.Editing;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.Design.Tracking;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design
{
    /// <summary>
    /// One tracked markup file: its text, its syntax tree, and the live objects built from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is the source of truth. Every edit made through <see cref="Editor"/> changes the text with a minimal
    /// <see cref="TextChangeSet"/>, re-parses it, and mirrors the change onto every live tree built from this file.
    /// Saving writes the text as is, so formatting, comments and anything the editor doesn't understand survive.
    /// </para>
    /// <para>
    /// A document must be used on the thread that owns its pages, like the pages themselves.
    /// </para>
    /// </remarks>
    public sealed class DesignDocument
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

        private readonly List<MarkupLoadScope> scopes = [];
        private readonly Dispatcher owner = Dispatcher.GetCurrentThreadDispatcher();
        private readonly Dictionary<(NodeId Node, string Name), PendingValue> pending = [];
        private readonly HashSet<NodeId> outOfSync = [];
        private readonly List<Diagnostic> liveErrors = [];
        private MarkupText text;
        private DocumentSyntax syntax;
        private LineMap lineMap;
        private Dictionary<ElementSyntax, NodeId> ids = [];
        private Dictionary<NodeId, ElementSyntax> nodes = [];
        private int nextNodeId;
        private FragmentFrame? fragment;
        private int recordingSuppressed;
        private string parsedText;
        private List<object>? building;
        private DocumentSyntax liveSyntax;
        private bool rootProblem;
        private bool reportedInSync = true;
        private Diagnostic[] reportedErrors = [];

        internal DesignDocument(DesignSession session, string? sourcePath, string text)
        {
            Session = session;
            SourcePath = sourcePath;
            this.text = new MarkupText(text);
            parsedText = text;
            syntax = Parse(text);
            liveSyntax = syntax;
            lineMap = new LineMap(text);
            foreach (ElementSyntax element in syntax.Elements)
                AssignNewId(element, ids, nodes);

            Editor = new MarkupEditor(this);
        }

        /// <summary>
        /// Occurs after the text changed, with the exact changes made.
        /// </summary>
        public event EventHandler<DocumentChangedEventArgs>? Changed;

        /// <summary>
        /// Occurs when an edit had to rebuild a live subtree instead of updating it in place.
        /// </summary>
        public event EventHandler<SubtreeReplacedEventArgs>? SubtreeReplaced;

        /// <summary>
        /// Occurs when <see cref="NeedsReload"/> becomes <see langword="true"/>.
        /// </summary>
        public event EventHandler? NeedsReloadChanged;

        /// <summary>
        /// Occurs when the syntax tree's <see cref="DocumentSyntax.Diagnostics"/> changed.
        /// </summary>
        public event EventHandler? DiagnosticsChanged;

        /// <summary>
        /// Occurs when <see cref="IsInSync"/> or <see cref="LiveErrors"/> changed.
        /// </summary>
        public event EventHandler? SyncStateChanged;

        /// <summary>
        /// Gets the session tracking this document.
        /// </summary>
        public DesignSession Session { get; }

        /// <summary>
        /// Gets the path the document was loaded from, as passed to the loader, or <see langword="null"/>.
        /// </summary>
        public string? SourcePath { get; }

        /// <summary>
        /// Gets the current markup text.
        /// </summary>
        public string Text => text.Text;

        /// <summary>
        /// Gets the text's version: 0 when loaded, plus one for every change.
        /// </summary>
        public int Version => text.Version;

        /// <summary>
        /// Gets the current syntax tree.
        /// </summary>
        public DocumentSyntax Syntax
        {
            get
            {
                FlushPending();
                return syntax;
            }
        }

        /// <summary>
        /// Gets a value indicating whether an edit changed the text in a way the live pages couldn't follow (an edit
        /// inside a style, a template or a property element, or a change to the root that needs a new root object).
        /// The text is still right; reload the page to see it.
        /// </summary>
        public bool NeedsReload { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the live pages reflect the current text: the text has no syntax errors,
        /// every part of the last <see cref="ApplyText"/> was mirrored, and the root didn't change in a way that needs
        /// a reload.
        /// </summary>
        public bool IsInSync => !syntax.HasErrors && outOfSync.Count == 0 && !rootProblem;

        /// <summary>
        /// Gets why the live pages don't reflect the current text: the parts of the last <see cref="ApplyText"/> that
        /// failed to mirror, with the text they concern. Syntax errors are in <see cref="DocumentSyntax.Diagnostics"/>.
        /// </summary>
        public IReadOnlyList<Diagnostic> LiveErrors => liveErrors;

        /// <summary>
        /// Gets the editor that changes this document and mirrors every change onto its live pages.
        /// </summary>
        public MarkupEditor Editor { get; }

        internal int ParseCount { get; private set; }

        internal ObjectMap Map { get; } = new();

        internal IReadOnlyList<MarkupLoadScope> Scopes => scopes;

        internal PropertyRegistry Registry => Session.Configuration.Types.PropertyRegistry;

        internal bool IsAlive => Map.FirstAlive() != null;

        internal int TrackedObjectCount => Map.Count;

        /// <summary>
        /// Gets the markup element with the given id in the current syntax tree.
        /// </summary>
        /// <param name="id">The element's id.</param>
        /// <returns>The element, or <see langword="null"/> when no element of the current text has that id.</returns>
        public ElementSyntax? GetNode(NodeId id)
        {
            FlushPending();
            return nodes.GetValueOrDefault(id);
        }

        /// <summary>
        /// Gets the id of an element of the current syntax tree.
        /// </summary>
        /// <param name="element">An element of <see cref="Syntax"/>.</param>
        /// <returns>The id, or <see langword="null"/> when <paramref name="element"/> isn't part of the current tree.</returns>
        public NodeId? GetNodeId(ElementSyntax element) => ids.TryGetValue(element, out NodeId id) ? id : null;

        /// <summary>
        /// Gets the live objects built from a markup element: one per load of this file still alive.
        /// </summary>
        /// <param name="id">The element's id.</param>
        /// <returns>The live objects; empty when none are alive or the element is opaque (inside a template, say).</returns>
        public IReadOnlyList<object> GetObjects(NodeId id) => [.. Map.GetObjects(id).Select(x => x.Instance)];

        /// <summary>
        /// Finds the markup element a live object was built from.
        /// </summary>
        /// <param name="instance">The live object.</param>
        /// <param name="id">The element's id.</param>
        /// <returns><see langword="true"/> when <paramref name="instance"/> was built from this document.</returns>
        public bool TryGetNodeId(object instance, out NodeId id)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (Map.TryGetEntry(instance, out ObjectMap.Entry? entry))
            {
                id = entry.Id;
                return true;
            }

            id = default;
            return false;
        }

        /// <summary>
        /// Writes the text to the file <see cref="SourcePath"/> resolves to through
        /// <see cref="DesignSession.SourcePathResolver"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The document has no source path, or it doesn't resolve to a file.</exception>
        /// <exception cref="IOException">The file couldn't be written.</exception>
        public void Save()
        {
            string? path = SourcePath != null ? Session.SourcePathResolver(SourcePath) : null;
            if (path == null)
            {
                throw new InvalidOperationException(
                    $"'{SourcePath ?? "(no source path)"}' doesn't resolve to a file. Use SaveAs, or set DesignSession.SourcePathResolver.");
            }

            SaveAs(path);
        }

        /// <summary>
        /// Writes the text, exactly as it is, to <paramref name="filePath"/> as UTF-8 without a byte order mark.
        /// </summary>
        /// <param name="filePath">The file to write.</param>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="IOException">The file couldn't be written.</exception>
        public void SaveAs(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            File.WriteAllText(filePath, Text, Utf8WithoutBom);
        }

        /// <summary>
        /// Replaces the whole text and updates the live pages to match, keeping every element that still corresponds,
        /// with its runtime state. This is how saved files and typed text reach a running page.
        /// </summary>
        /// <param name="newText">The new markup text.</param>
        /// <returns>
        /// Always a success: the text is stored and recorded as one undo step even when the live pages can't follow
        /// all of it. Check <see cref="IsInSync"/> and <see cref="LiveErrors"/> for that.
        /// </returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Malformed text changes nothing live; the pages wait for the next well-formed text.</description></item>
        /// <item><description>
        /// Well-formed text is diffed against the last tree the pages followed. Each part is mirrored on its own: one
        /// part that fails (a value that doesn't convert, say) is reported in <see cref="LiveErrors"/> and doesn't stop
        /// the others, and the next <see cref="ApplyText"/> rebuilds what failed.
        /// </description></item>
        /// <item><description>Identical text changes nothing and records nothing.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="newText"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <exception cref="InvalidOperationException">The calling thread doesn't own the document's pages.</exception>
        public EditResult ApplyText(string newText)
        {
            ArgumentNullException.ThrowIfNull(newText);
            ThrowIfDisposed();
            VerifyAccess();
            FlushPending();

            string previous = text.Text;
            if (string.Equals(previous, newText, StringComparison.Ordinal))
                return EditResult.Success();

            ApplyTextCore(newText);
            Editor.UndoStack.RecordText(previous, "Apply text");
            return EditResult.Success();
        }

        /// <summary>
        /// Reads the file <see cref="SourcePath"/> resolves to through <see cref="DesignSession.SourcePathResolver"/>,
        /// and applies its text with <see cref="ApplyText"/>.
        /// </summary>
        /// <returns><see langword="false"/>, changing nothing, when the source path doesn't resolve to a file.</returns>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <exception cref="IOException">The file couldn't be read.</exception>
        public bool ReloadFromSource()
        {
            ThrowIfDisposed();
            string? path = SourcePath != null ? Session.SourcePathResolver(SourcePath) : null;
            if (path == null || !File.Exists(path))
                return false;

            ApplyText(File.ReadAllText(path));
            return true;
        }

        internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Session.IsDisposed, this);

        internal EditResult Apply(EditStep step, out EditStep? inverse)
        {
            inverse = null;
            VerifyAccess();
            FlushPending();

            var before = new DocumentSnapshot(text, syntax, lineMap, ids, nodes);
            MarkupText newText = text.Apply(step.Change);
            DocumentSyntax newSyntax = Parse(newText.Text);
            if (newSyntax.HasErrors && !syntax.HasErrors)
            {
                Diagnostic first = newSyntax.Diagnostics[0];
                return EditResult.Failure(first.Span, $"The edit would make the markup malformed: {first.Message}");
            }

            List<NodeId> vanished = Resync(newText, newSyntax, step.Change, step.Actions);
            var context = new MirrorContext(this, before);
            int executed = 0;
            try
            {
                for (; executed < step.Actions.Count; executed++)
                    step.Actions[executed].Execute(context);
            }
            catch (Exception ex)
            {
                Restore(before);
                for (int i = Math.Min(executed, step.Actions.Count - 1); i >= 0; i--)
                    step.Actions[i].Revert(context);

                if (!Failures.IsFatal(ex))
                    return EditResult.Failure(step.Change.Count > 0 ? new TextSpan(step.Change[0].Span.Start, 0) : default, Failures.Describe(ex));
                throw;
            }

            foreach (NodeId id in vanished)
                Map.RemoveNode(id);

            var inverseActions = new List<MirrorAction>(step.Actions.Count);
            for (int i = step.Actions.Count - 1; i >= 0; i--)
                inverseActions.Add(step.Actions[i].CreateInverse(context));
            inverse = new EditStep(step.Change.Invert(before.Text.Text), inverseActions, step.Description);

            Changed?.Invoke(this, new DocumentChangedEventArgs(step.Change, Version));
            if (before.Syntax.Diagnostics.Count != syntax.Diagnostics.Count)
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

            return EditResult.Success(context.ResultNode);
        }

        /// <summary>
        /// Throws when called from a thread other than the one owning this document's pages.
        /// </summary>
        /// <exception cref="InvalidOperationException">The calling thread doesn't own the pages.</exception>
        internal void VerifyAccess()
        {
            // The dispatcher of the thread that loaded the page, not a live object's: the check must still hold
            // once every page built from this document has been collected.
            owner.VerifyAccess();
        }

        /// <summary>
        /// Determines whether edits to <paramref name="element"/> can be mirrored onto the live pages in this phase.
        /// </summary>
        internal bool IsEditable(ElementSyntax element) => IsEditable(element, ids);

        internal bool IsEditable(ElementSyntax element, IReadOnlyDictionary<ElementSyntax, NodeId> treeIds)
        {
            // Styles, resources, templates and anything else under a property element are opaque in this phase.
            for (ElementSyntax? current = element; current != null; current = current.Parent)
            {
                if (current.IsPropertyElement)
                    return false;
            }

            if (!treeIds.TryGetValue(element, out NodeId id))
                return false;

            List<(object Instance, MarkupLoadScope Scope)> objects = Map.GetObjects(id);
            return objects.Count > 0 && objects.TrueForAll(x => x.Instance is UI.UIElement);
        }

        internal object? FindObject(NodeId id, MarkupLoadScope scope) => Map.FindObject(id, scope);

        /// <summary>
        /// Builds a fresh live subtree from <paramref name="element"/>'s current text, for <paramref name="liveParent"/>.
        /// If the build fails, every name it already registered is unregistered again.
        /// </summary>
        internal UIElement BuildElement(MarkupLoadScope scope, ElementSyntax element, UIElement liveParent)
        {
            MarkupNameScope names = scope.NameScope
                ?? throw new DesignEditException("The page this document was loaded into no longer exists.");
            var namesBefore = new HashSet<string>(names.Names.Keys, StringComparer.Ordinal);
            string elementText = Text.Substring(element.Span.Start, element.Span.Length);
            List<object>? outer = building;
            building = [];

            try
            {
                XElement fragment = Session.Builder.ParseFragment(elementText, syntax.GetNamespacesInScope(element));
                using (EnterFragment(element.Span.Start, elementText))
                {
                    object built = Session.Builder.BuildFragment(scope, fragment, liveParent);
                    return built as UIElement
                        ?? throw new DesignEditException($"'{element.Name}' isn't a UI element, so it can't be placed in the element tree.");
                }
            }
            catch
            {
                foreach (string name in names.Names.Keys.Where(x => !namesBefore.Contains(x)).ToList())
                    names.Unregister(name);

                // The objects created before the failure were tracked as they were made; they never reach a page.
                foreach (object orphan in building)
                    Map.Remove(orphan);
                throw;
            }
            finally
            {
                outer?.AddRange(building);
                building = outer;
            }
        }

        /// <summary>
        /// Computes where <paramref name="element"/>'s live copy goes among <paramref name="liveParent"/>'s children:
        /// right after the nearest earlier sibling that is live there, or else right before the nearest later one, or
        /// else at the end. Runtime content the game added therefore never shifts the markup order.
        /// </summary>
        internal int ComputeLiveIndex(ElementSyntax element, object liveParent, MarkupLoadScope scope)
        {
            List<ElementSyntax> siblings = [.. element.Parent!.ContentElements];
            int position = siblings.IndexOf(element);

            for (int i = position - 1; i >= 0; i--)
            {
                if (FindLiveIndex(siblings[i], liveParent, scope) is int index)
                    return index + 1;
            }

            for (int i = position + 1; i < siblings.Count; i++)
            {
                if (FindLiveIndex(siblings[i], liveParent, scope) is int index)
                    return index;
            }

            return LiveContent.Count(liveParent, Registry);
        }

        internal AttributeKind ClassifyAttribute(ElementSyntax element, string name)
        {
            if (name == "xmlns" || name.StartsWith("xmlns:", StringComparison.Ordinal))
                return AttributeKind.NamespaceDeclaration;

            int colon = name.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && syntax.ResolvePrefix(element, name[..colon]) == MarkupNamespaces.Directives)
                return name[(colon + 1)..] == MarkupDirectives.Name ? AttributeKind.Name : AttributeKind.Directive;

            return AttributeKind.Property;
        }

        /// <summary>
        /// Applies one attribute of the current text to a live object, through the loader, so the observer records it
        /// as it would during a load.
        /// </summary>
        internal void ApplyAttribute(MarkupLoadScope scope, object instance, ElementSyntax element, AttributeSyntax attribute)
        {
            // The start tag alone, closed, keeps the attribute at the same offset relative to the element.
            string tag = Text.Substring(element.Span.Start, element.StartTagEnd - element.Span.Start);
            if (!element.IsSelfClosing)
                tag = string.Concat(tag.AsSpan(0, tag.Length - 1), "/>");

            XElement fragment = Session.Builder.ParseFragment(tag, syntax.GetNamespacesInScope(element));
            var tagLines = new LineMap(tag);
            int relative = attribute.NameSpan.Start - element.Span.Start;
            XAttribute xml = fragment.Attributes().First(x =>
                x is IXmlLineInfo info && tagLines.TryToOffset(info.LineNumber, info.LinePosition, out int offset) && offset == relative);

            using (EnterFragment(element.Span.Start, tag))
                Session.Builder.ApplyAttribute(scope, instance, xml);
        }

        /// <summary>
        /// Stores <paramref name="newText"/> and mirrors it, without recording undo: <see cref="ApplyText"/> and undo
        /// replay both come through here.
        /// </summary>
        internal void ApplyTextCore(string newText)
        {
            FlushPending();
            int diagnosticsBefore = syntax.Diagnostics.Count;
            TextChange change = DiffText(text.Text, newText);
            text = new MarkupText(newText, text.Version + 1);
            parsedText = newText;
            syntax = Parse(newText);
            lineMap = new LineMap(newText);

            // Malformed text: the id map and the live pages stay on the last tree they could follow.
            if (!syntax.HasErrors)
                MirrorText();

            Changed?.Invoke(this, new DocumentChangedEventArgs(new TextChangeSet([change]), Version));
            if (diagnosticsBefore != syntax.Diagnostics.Count)
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            UpdateSyncState();
        }

        /// <summary>
        /// Replaces every live copy of <paramref name="element"/> with a fresh build of its current text. The root can't
        /// be swapped out of a parent, so for the root this only sets <see cref="NeedsReload"/>.
        /// </summary>
        internal void Rebuild(ElementSyntax element)
        {
            if (element.Parent is not { } parentElement || GetNodeId(element) is not NodeId id || GetNodeId(parentElement) is not NodeId parentId)
            {
                MarkNeedsReload();
                return;
            }

            foreach ((object instance, MarkupLoadScope scope) in Map.GetObjects(id))
            {
                if (instance is not UIElement old || FindObject(parentId, scope) is not UIElement liveParent || LiveContent.IndexOf(liveParent, old, Registry) < 0)
                    continue;

                List<(string Name, UIElement Element)> names = LiveTree.UnregisterNames(scope, old);
                UIElement built;
                try
                {
                    built = BuildElement(scope, element, liveParent);
                }
                catch
                {
                    LiveTree.RegisterNames(scope, names);
                    throw;
                }

                LiveContent.Replace(liveParent, old, built, Registry);
                Map.RemoveSubtree(old);
                RaiseSubtreeReplaced(new SubtreeReplacedEventArgs(id, old, built));
            }
        }

        /// <summary>
        /// Folds pending fast-path values into a real re-parse. Every id survives, because the values only changed
        /// inside start tags.
        /// </summary>
        internal void FlushPending()
        {
            if (pending.Count == 0)
                return;

            var change = new TextChangeSet(pending.Values.Select(x => new TextChange(x.ParsedSpan, x.Raw)));
            pending.Clear();
            foreach (NodeId id in Resync(text, Parse(text.Text), change, []))
                Map.RemoveNode(id);
        }

        /// <summary>
        /// Sets an existing literal attribute without re-parsing, when every live copy can take the new value in place.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when the edit needs the normal path. Otherwise <see langword="true"/>, with the outcome
        /// in <paramref name="result"/> and, on success, the value text before this edit in <paramref name="originalRaw"/>.
        /// </returns>
        internal bool TryFastSetAttribute(NodeId node, string name, string value, [NotNullWhen(true)] out EditResult? result, out string? originalRaw)
        {
            result = null;
            originalRaw = null;

            // Pending values must all belong to one element, so their parsed spans stay valid.
            if (pending.Keys.Any(x => x.Node != node))
                FlushPending();

            if (value.StartsWith('{')
                || !nodes.TryGetValue(node, out ElementSyntax? element)
                || element.FindAttribute(name) is not { IsMissingValue: false } attribute
                || attribute.Quote == '\0'
                || ClassifyAttribute(element, name) != AttributeKind.Property
                || !IsEditable(element))
            {
                return false;
            }

            var key = (node, name);
            string currentRaw = pending.TryGetValue(key, out PendingValue? current)
                ? current.Raw
                : parsedText.Substring(attribute.ValueSpan.Start, attribute.ValueSpan.Length);
            if (currentRaw.StartsWith('{'))
                return false;

            List<(object Instance, MarkupLoadScope Scope)> objects = Map.GetObjects(node);
            foreach ((object instance, _) in objects)
            {
                if (!Map.TryGetEntry(instance, out ObjectMap.Entry? entry) || !entry.Members.ContainsKey(name))
                    return false;
            }

            VerifyAccess();
            string raw = MarkupEscaping.EscapeAttributeValue(value, attribute.Quote);
            int applied = 0;
            try
            {
                for (; applied < objects.Count; applied++)
                    ApplyAttributeValue(objects[applied].Scope, objects[applied].Instance, element, name, raw, attribute.Quote);
            }
            catch (Exception ex) when (!Failures.IsFatal(ex))
            {
                for (int i = 0; i <= applied && i < objects.Count; i++)
                    ApplyAttributeValue(objects[i].Scope, objects[i].Instance, element, name, currentRaw, attribute.Quote);

                result = EditResult.Failure(attribute.ValueSpan, Failures.Describe(ex));
                return true;
            }

            var patch = new TextChangeSet([new TextChange(new TextSpan(CurrentOffset(attribute.ValueSpan.Start, key), currentRaw.Length), raw)]);
            pending[key] = new PendingValue(attribute.ValueSpan, raw);
            text = new MarkupText(patch.Apply(text.Text), text.Version + 1);
            originalRaw = currentRaw;

            Changed?.Invoke(this, new DocumentChangedEventArgs(patch, Version));
            result = EditResult.Success();
            return true;
        }

        internal void AddScope(MarkupLoadScope scope)
        {
            // A page loaded from the current text must correlate against it, not against the last parsed text.
            FlushPending();
            scopes.Add(scope);
        }

        internal void RemoveScope(MarkupLoadScope scope)
        {
            scopes.Remove(scope);
            Map.RemoveScope(scope);
        }

        internal void OnObjectCreated(MarkupLoadScope scope, XElement node, object instance)
        {
            if (recordingSuppressed > 0 || ToOffset(node) is not int offset)
                return;

            if (syntax.FindElementByNameStart(offset) is { } element && ids.TryGetValue(element, out NodeId id))
            {
                Map.Add(id, instance, scope);
                building?.Add(instance);
            }
        }

        internal void OnMemberApplied(XObject node, object target, MarkupMember member, IBinding? binding)
        {
            if (recordingSuppressed > 0 || node is not XAttribute || ToOffset(node) is not int offset)
                return;

            if (syntax.FindAttributeByNameStart(offset) is { } attribute)
                Map.RecordMember(target, attribute.Name, new AppliedMember(member, binding));
        }

        /// <summary>
        /// Makes the line information of objects built from a fragment of the current text correlate with the
        /// document: positions are taken relative to <paramref name="fragmentText"/>, which starts at
        /// <paramref name="baseOffset"/>.
        /// </summary>
        internal IDisposable EnterFragment(int baseOffset, string fragmentText)
        {
            FragmentFrame? previous = fragment;
            fragment = new FragmentFrame(baseOffset, new LineMap(fragmentText));
            return new Restorer(() => fragment = previous);
        }

        /// <summary>
        /// Stops recording builder notifications, for updates that must keep the existing records as they are.
        /// </summary>
        internal IDisposable SuppressRecording()
        {
            recordingSuppressed++;
            return new Restorer(() => recordingSuppressed--);
        }

        internal void RaiseSubtreeReplaced(SubtreeReplacedEventArgs args) => SubtreeReplaced?.Invoke(this, args);

        internal void MarkNeedsReload()
        {
            if (NeedsReload)
                return;

            NeedsReload = true;
            NeedsReloadChanged?.Invoke(this, EventArgs.Empty);
        }

        private static TextChange DiffText(string before, string after)
        {
            int limit = Math.Min(before.Length, after.Length);
            int prefix = 0;
            while (prefix < limit && before[prefix] == after[prefix])
                prefix++;

            int suffix = 0;
            while (suffix < limit - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
                suffix++;

            return new TextChange(new TextSpan(prefix, before.Length - prefix - suffix), after.Substring(prefix, after.Length - prefix - suffix));
        }

        private static bool HasRebuiltAncestor(ElementSyntax element, HashSet<ElementSyntax> rebuilt)
        {
            for (ElementSyntax? current = element.Parent; current != null; current = current.Parent)
            {
                if (rebuilt.Contains(current))
                    return true;
            }

            return false;
        }

        private void AssignNewId(ElementSyntax element, Dictionary<ElementSyntax, NodeId> targetIds, Dictionary<NodeId, ElementSyntax> targetNodes)
        {
            var id = new NodeId(++nextNodeId);
            targetIds[element] = id;
            targetNodes[id] = element;
        }

        private int? ToOffset(IXmlLineInfo node)
        {
            if (!node.HasLineInfo())
                return null;

            if (fragment is { } frame)
                return frame.LineMap.TryToOffset(node.LineNumber, node.LinePosition, out int local) ? frame.BaseOffset + local : null;

            return lineMap.TryToOffset(node.LineNumber, node.LinePosition, out int offset) ? offset : null;
        }

        private List<NodeId> Resync(MarkupText newText, DocumentSyntax newSyntax, TextChangeSet change, IReadOnlyList<MirrorAction> actions)
        {
            var newIds = new Dictionary<ElementSyntax, NodeId>();
            var newNodes = new Dictionary<NodeId, ElementSyntax>();

            // Moved elements first: their old text was deleted, so offset mapping alone can't find them.
            foreach (MirrorAction action in actions)
            {
                if (action.Hint is { } hint && nodes.TryGetValue(hint.Node, out ElementSyntax? moved) && newSyntax.FindElementAt(hint.NewStart) is { } target)
                    MatchSubtree(moved, target, newIds, newNodes);
            }

            // Removed elements coming back (undo of a remove) take their old ids, in document order.
            foreach (MirrorAction action in actions)
            {
                if (action.Restore is not { } restore || newSyntax.FindElementAt(restore.Start) is not { } restored)
                    continue;

                using IEnumerator<NodeId> oldIds = restore.Ids.GetEnumerator();
                foreach (ElementSyntax element in restored.DescendantsAndSelf())
                {
                    if (!oldIds.MoveNext())
                        break;
                    if (newIds.ContainsKey(element) || newNodes.ContainsKey(oldIds.Current) || ids.ContainsValue(oldIds.Current))
                        continue;

                    newIds[element] = oldIds.Current;
                    newNodes[oldIds.Current] = element;
                }
            }

            // Everything else: an element survives when its start offset survives the change.
            foreach ((ElementSyntax oldElement, NodeId id) in ids)
            {
                if (newNodes.ContainsKey(id) || change.MapPosition(oldElement.Span.Start) is not int mapped)
                    continue;

                if (newSyntax.FindElementAt(mapped) is { } candidate && candidate.Name == oldElement.Name && !newIds.ContainsKey(candidate))
                {
                    newIds[candidate] = id;
                    newNodes[id] = candidate;
                }
            }

            foreach (ElementSyntax element in newSyntax.Elements)
            {
                if (!newIds.ContainsKey(element))
                    AssignNewId(element, newIds, newNodes);
            }

            List<NodeId> vanished = [.. ids.Values.Where(x => !newNodes.ContainsKey(x))];
            text = newText;
            syntax = newSyntax;
            lineMap = new LineMap(newText.Text);
            ids = newIds;
            nodes = newNodes;
            parsedText = newText.Text;
            liveSyntax = newSyntax;
            return vanished;
        }

        private void MatchSubtree(ElementSyntax oldElement, ElementSyntax newElement, Dictionary<ElementSyntax, NodeId> newIds, Dictionary<NodeId, ElementSyntax> newNodes)
        {
            if (oldElement.Name != newElement.Name || !ids.TryGetValue(oldElement, out NodeId id))
                return;

            newIds[newElement] = id;
            newNodes[id] = newElement;

            // A move carries the element's text over verbatim, so its children line up one to one.
            using IEnumerator<ElementSyntax> oldChildren = oldElement.Elements.GetEnumerator();
            using IEnumerator<ElementSyntax> newChildren = newElement.Elements.GetEnumerator();
            while (oldChildren.MoveNext() && newChildren.MoveNext())
                MatchSubtree(oldChildren.Current, newChildren.Current, newIds, newNodes);
        }

        private void Restore(DocumentSnapshot snapshot)
        {
            // Apply flushed any pending values before taking the snapshot, so its text is also the last parsed one.
            text = snapshot.Text;
            parsedText = snapshot.Text.Text;
            syntax = snapshot.Syntax;
            liveSyntax = snapshot.Syntax;
            lineMap = snapshot.LineMap;
            ids = snapshot.Ids;
            nodes = snapshot.Nodes;
        }

        private int? FindLiveIndex(ElementSyntax sibling, object liveParent, MarkupLoadScope scope)
        {
            if (GetNodeId(sibling) is not NodeId id || FindObject(id, scope) is not { } instance)
                return null;

            int index = LiveContent.IndexOf(liveParent, instance, Registry);
            return index >= 0 ? index : null;
        }

        private void MirrorText()
        {
            TreeDiff diff = TreeMatcher.Match(liveSyntax, syntax);
            liveErrors.Clear();
            rootProblem = diff.RootProblem != null;
            if (diff.RootProblem is { } problem)
            {
                // The root can't be swapped in place: keep everything on the last tree until the root matches again.
                liveErrors.Add(new Diagnostic(syntax.Root!.NameSpan, problem));
                MarkNeedsReload();
                return;
            }

            if (diff.OpaqueChanged)
                MarkNeedsReload();

            var before = new DocumentSnapshot(new MarkupText(liveSyntax.Text), liveSyntax, new LineMap(liveSyntax.Text), ids, nodes);
            HashSet<NodeId> previouslyOutOfSync = [.. outOfSync];
            outOfSync.Clear();
            var newIds = new Dictionary<ElementSyntax, NodeId>();
            var newNodes = new Dictionary<NodeId, ElementSyntax>();
            foreach ((ElementSyntax current, ElementSyntax old) in diff.Pairs)
            {
                if (ids.TryGetValue(old, out NodeId id))
                {
                    newIds[current] = id;
                    newNodes[id] = current;
                }
            }

            foreach (ElementSyntax element in syntax.Elements)
            {
                if (!newIds.ContainsKey(element))
                    AssignNewId(element, newIds, newNodes);
            }

            List<NodeId> vanished = [.. ids.Values.Where(x => !newNodes.ContainsKey(x))];
            ids = newIds;
            nodes = newNodes;
            liveSyntax = syntax;

            var context = new MirrorContext(this, before);
            foreach (TextUnit unit in CreateUnits(diff, before, previouslyOutOfSync))
                RunUnit(unit, context, before);

            foreach (NodeId id in vanished)
                Map.RemoveNode(id);

            SyncNames();
        }

        /// <summary>
        /// Turns a diff into units, ordered so insertions anchor against the final set of siblings: removals, moves,
        /// replacements, insertions, then attribute changes. Elements left out of sync by an earlier apply are rebuilt,
        /// or inserted when they have no live copy yet.
        /// </summary>
        private List<TextUnit> CreateUnits(TreeDiff diff, DocumentSnapshot before, HashSet<NodeId> previouslyOutOfSync)
        {
            var oldToNew = new Dictionary<ElementSyntax, ElementSyntax>();
            foreach ((ElementSyntax current, ElementSyntax old) in diff.Pairs)
                oldToNew[old] = current;

            var rebuilt = new HashSet<ElementSyntax>();
            var replaced = new List<ElementSyntax>();
            var inserted = new List<ElementSyntax>();
            foreach ((_, ElementSyntax current) in diff.Replaced)
            {
                if (rebuilt.Add(current))
                    replaced.Add(current);
            }

            foreach (ElementSyntax current in diff.Inserted)
            {
                if (rebuilt.Add(current))
                    inserted.Add(current);
            }

            var healing = new List<ElementSyntax>();
            foreach (NodeId id in previouslyOutOfSync)
            {
                if (nodes.TryGetValue(id, out ElementSyntax? element) && rebuilt.Add(element))
                    healing.Add(element);
            }

            foreach (ElementSyntax element in healing)
            {
                // An ancestor rebuilt from the text brings this element along; healing it as well would build it twice.
                if (HasRebuiltAncestor(element, rebuilt))
                    continue;

                NodeId id = ids[element];
                if (Map.GetObjects(id).Count > 0)
                    replaced.Add(element);
                else if (element.Parent is { } parent && ids.TryGetValue(parent, out NodeId parentId) && Map.GetObjects(parentId).Count > 0)
                    inserted.Add(element);
            }

            var units = new List<TextUnit>();
            foreach (ElementSyntax old in diff.Removed)
            {
                TextSpan span = old.Parent != null && oldToNew.TryGetValue(old.Parent, out ElementSyntax? parent) ? parent.NameSpan : default;
                units.Add(new TextUnit(new ElementRemovedAction(before.Ids[old]), null, span));
            }

            foreach ((_, ElementSyntax current) in diff.Moved)
                units.Add(new TextUnit(new ElementMovedAction(ids[current], ids[current.Parent!], current.Span.Start), ids[current], current.NameSpan));

            foreach (ElementSyntax current in replaced)
                units.Add(new TextUnit(new ElementReplacedAction(ids[current]), ids[current], current.NameSpan));

            foreach (ElementSyntax current in inserted)
                units.Add(new TextUnit(new ElementInsertedAction(ids[current.Parent!], current.Span.Start), ids[current], current.NameSpan));

            foreach ((ElementSyntax element, string name) in diff.ChangedAttributes)
            {
                if (!rebuilt.Contains(element))
                    units.Add(new TextUnit(new AttributeChangedAction(ids[element], name), ids[element], element.FindAttribute(name)?.Span ?? element.NameSpan));
            }

            return units;
        }

        /// <summary>
        /// Makes every named live element resolvable by its <c>x:Name</c> again. An element moved out of a container the
        /// same apply removed had its name unregistered with that container.
        /// </summary>
        private void SyncNames()
        {
            foreach (ElementSyntax element in syntax.Elements)
            {
                if (TreeMatcher.GetDirective(syntax, element, MarkupDirectives.Name) is not { } name || !ids.TryGetValue(element, out NodeId id))
                    continue;

                foreach ((object instance, MarkupLoadScope scope) in Map.GetObjects(id))
                {
                    if (instance is not UIElement live || scope.NameScope is not { } names || ReferenceEquals(names.Find(name), live))
                        continue;

                    names.Unregister(name);
                    names.Register(name, live);
                }
            }
        }

        private void UpdateSyncState()
        {
            bool inSync = IsInSync;
            if (inSync == reportedInSync && liveErrors.SequenceEqual(reportedErrors))
                return;

            reportedInSync = inSync;
            reportedErrors = [.. liveErrors];
            SyncStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Runs one unit. A unit that fails is reverted on the old tree, reported, and remembered as out of sync, and
        /// the other units still run.
        /// </summary>
        private void RunUnit(TextUnit unit, MirrorContext context, DocumentSnapshot before)
        {
            try
            {
                unit.Action.Execute(context);
            }
            catch (Exception ex) when (!Failures.IsFatal(ex))
            {
                RevertOnOldTree(unit.Action, context, before);
                liveErrors.Add(new Diagnostic(unit.Span, Failures.Describe(ex)));
                if (unit.Node is NodeId failed)
                    outOfSync.Add(failed);
            }
        }

        /// <summary>
        /// Reverts a failed unit with the document showing the old tree, which is what every action's Revert expects,
        /// then puts the new tree back.
        /// </summary>
        private void RevertOnOldTree(MirrorAction action, MirrorContext context, DocumentSnapshot before)
        {
            var after = new DocumentSnapshot(text, syntax, lineMap, ids, nodes);
            Restore(before);
            try
            {
                action.Revert(context);
            }
            catch (Exception ex) when (!Failures.IsFatal(ex))
            {
                // A revert that fails too leaves the element behind the text; it's reported and rebuilt next time.
            }
            finally
            {
                Restore(after);
            }
        }

        private DocumentSyntax Parse(string value)
        {
            ParseCount++;
            return DocumentSyntax.Parse(value);
        }

        /// <summary>
        /// Maps an offset in the last parsed text to the current text, through every other pending value.
        /// </summary>
        private int CurrentOffset(int parsedOffset, (NodeId Node, string Name) key)
        {
            int offset = parsedOffset;
            foreach (((NodeId Node, string Name) other, PendingValue value) in pending)
            {
                if (other != key && value.ParsedSpan.Start < parsedOffset)
                    offset += value.Raw.Length - value.ParsedSpan.Length;
            }

            return offset;
        }

        private void ApplyAttributeValue(MarkupLoadScope scope, object instance, ElementSyntax element, string name, string raw, char quote)
        {
            // A one-attribute start tag is enough for the loader; the existing record of the member stays as it is.
            XElement fragment = Session.Builder.ParseFragment($"<{element.Name} {name}={quote}{raw}{quote}/>", syntax.GetNamespacesInScope(element, includeSelf: true));
            using (SuppressRecording())
                Session.Builder.ApplyAttribute(scope, instance, fragment.Attributes().Single(x => !x.IsNamespaceDeclaration));
        }

        private sealed record FragmentFrame(int BaseOffset, LineMap LineMap);

        private sealed record PendingValue(TextSpan ParsedSpan, string Raw);

        private sealed class Restorer(Action restore) : IDisposable
        {
            private Action? restore = restore;

            public void Dispose()
            {
                restore?.Invoke();
                restore = null;
            }
        }
    }
}
