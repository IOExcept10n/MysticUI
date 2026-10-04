// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;
using System.Xml.Linq;
using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.Design.Tracking;
using Icy.Markup;

namespace Icy.Design
{
    /// <summary>
    /// One tracked markup file: its text, its syntax tree, and the live objects built from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is the source of truth. Every edit made through <c>Editor</c> changes the text with a minimal
    /// <see cref="TextChangeSet"/>, re-parses it, and mirrors the change onto every live tree built from this file.
    /// Saving writes the text as is, so formatting, comments and anything the editor doesn't understand survive.
    /// </para>
    /// <para>
    /// A document must be used on the thread that owns its pages, like the pages themselves.
    /// </para>
    /// </remarks>
    public sealed class DesignDocument
    {
        private readonly List<MarkupLoadScope> scopes = [];
        private MarkupText text;
        private DocumentSyntax syntax;
        private LineMap lineMap;
        private Dictionary<ElementSyntax, NodeId> ids = [];
        private Dictionary<NodeId, ElementSyntax> nodes = [];
        private int nextNodeId;
        private FragmentFrame? fragment;
        private int recordingSuppressed;

        internal DesignDocument(DesignSession session, string? sourcePath, string text)
        {
            Session = session;
            SourcePath = sourcePath;
            this.text = new MarkupText(text);
            syntax = DocumentSyntax.Parse(text);
            lineMap = new LineMap(text);
            foreach (ElementSyntax element in syntax.Elements)
                AssignNewId(element, ids, nodes);
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
        public DocumentSyntax Syntax => syntax;

        /// <summary>
        /// Gets a value indicating whether an edit changed the text in a way the live pages couldn't follow (an edit
        /// inside a style, a template or a property element, or a change to the root that needs a new root object).
        /// The text is still right; reload the page to see it.
        /// </summary>
        public bool NeedsReload { get; private set; }

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
        public ElementSyntax? GetNode(NodeId id) => nodes.GetValueOrDefault(id);

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

        internal void AddScope(MarkupLoadScope scope) => scopes.Add(scope);

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
                Map.Add(id, instance, scope);
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

        private sealed record FragmentFrame(int BaseOffset, LineMap LineMap);

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
