// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using Icy.Design.Editing;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design
{
    /// <summary>
    /// Edits a <see cref="DesignDocument"/>: every edit changes the markup text minimally and is mirrored onto every
    /// live page built from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every edit is all-or-nothing. When it can't be made, or the live page rejects it (a value that doesn't
    /// convert, say), the method returns a failed <see cref="EditResult"/> and neither the text nor any page changed.
    /// </para>
    /// <para>
    /// Edits to opaque parts of the markup (styles, resources, templates, anything under a property element such as
    /// <c>&lt;Grid.RowDefinitions&gt;</c>) change the text only, and set <see cref="DesignDocument.NeedsReload"/>.
    /// </para>
    /// <para>
    /// Call the editor on the thread that owns the document's pages; other threads get an
    /// <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    public sealed class MarkupEditor
    {
        private readonly DesignDocument document;

        internal MarkupEditor(DesignDocument document)
        {
            this.document = document;
            UndoStack = new UndoStack(document);
        }

        /// <summary>
        /// Gets the document this editor changes.
        /// </summary>
        public DesignDocument Document => document;

        /// <summary>
        /// Gets the undo and redo history of this editor's edits.
        /// </summary>
        public UndoStack UndoStack { get; }

        /// <summary>
        /// Inserts an element written in markup as a content child of <paramref name="parent"/>.
        /// </summary>
        /// <param name="parent">The element to insert into.</param>
        /// <param name="index">
        /// The position among <paramref name="parent"/>'s content children (property elements such as
        /// <c>&lt;Grid.RowDefinitions&gt;</c> don't count), from 0 to their count.
        /// </param>
        /// <param name="markup">Exactly one element, such as <c>&lt;Button Padding="12,6"&gt;OK&lt;/Button&gt;</c>.</param>
        /// <returns>The outcome; on success <see cref="EditResult.Node"/> is the inserted element.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="markup"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <remarks>
        /// <para>Refused while <see cref="DesignDocument.IsInSync"/> is <see langword="false"/>: fix the markup first.</para>
        /// </remarks>
        public EditResult InsertElement(NodeId parent, int index, string markup)
        {
            document.ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(markup);
            if (RefuseWhileOutOfSync() is { } refused)
                return refused;

            if (document.GetNode(parent) is not { } parentElement)
                return UnknownNode(parent);

            string fragment = markup.Trim();
            DocumentSyntax parsed = DocumentSyntax.Parse(fragment);
            if (parsed.HasErrors || parsed.Root == null || parsed.Nodes.Count != 1)
                return EditResult.Failure(parentElement.NameSpan, "The inserted markup must be exactly one well-formed element.");

            List<ElementSyntax> children = [.. parentElement.ContentElements];
            if (Validate(parentElement, children.Count, index) is { } failure)
                return failure;

            (TextChange change, int offset) = MarkupFormatting.CreateInsertion(document.Syntax, parentElement, children, index, fragment, stripIndent: null);
            return Execute(new EditStep(new TextChangeSet([change]), [new ElementInsertedAction(parent, offset)], $"Insert {parsed.Root.Name}"));
        }

        /// <summary>
        /// Removes an element and everything in it.
        /// </summary>
        /// <param name="node">The element to remove. It can't be the root.</param>
        /// <returns>The outcome.</returns>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <remarks>
        /// <para>Refused while <see cref="DesignDocument.IsInSync"/> is <see langword="false"/>: fix the markup first.</para>
        /// </remarks>
        public EditResult RemoveElement(NodeId node)
        {
            document.ThrowIfDisposed();
            if (RefuseWhileOutOfSync() is { } refused)
                return refused;
            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (element.Parent == null)
                return EditResult.Failure(element.NameSpan, "The root element can't be removed.");

            TextSpan removal = MarkupFormatting.GetRemovalSpan(document.Text, element);
            return Execute(new EditStep(new TextChangeSet([new TextChange(removal, string.Empty)]), [new ElementRemovedAction(node)], $"Remove {element.Name}"));
        }

        /// <summary>
        /// Moves an element to another parent, or to another position in the same parent. The live element keeps its
        /// identity and runtime state.
        /// </summary>
        /// <param name="node">The element to move. It can't be the root.</param>
        /// <param name="newParent">The element to move it into. It can't be <paramref name="node"/> or inside it.</param>
        /// <param name="index">
        /// The position among <paramref name="newParent"/>'s content children, counted without <paramref name="node"/>.
        /// </param>
        /// <returns>The outcome.</returns>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <remarks>
        /// <para>Refused while <see cref="DesignDocument.IsInSync"/> is <see langword="false"/>: fix the markup first.</para>
        /// </remarks>
        public EditResult MoveElement(NodeId node, NodeId newParent, int index)
        {
            document.ThrowIfDisposed();
            if (RefuseWhileOutOfSync() is { } refused)
                return refused;
            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (document.GetNode(newParent) is not { } target)
                return UnknownNode(newParent);
            if (element.Parent == null)
                return EditResult.Failure(element.NameSpan, "The root element can't be moved.");
            if (ReferenceEquals(element, target) || element.IsAncestorOf(target))
                return EditResult.Failure(target.NameSpan, "An element can't be moved into itself.");

            List<ElementSyntax> children = [.. target.ContentElements.Where(x => !ReferenceEquals(x, element))];
            if (Validate(target, children.Count, index) is { } failure)
                return failure;

            // Dropping an element back where it already is (a common drag-and-drop outcome) changes nothing.
            if (ReferenceEquals(target, element.Parent) && target.ContentElements.ToList().IndexOf(element) == index)
                return EditResult.Success();

            string text = document.Text;
            TextSpan removal = MarkupFormatting.GetRemovalSpan(text, element);
            string fragment = text.Substring(element.Span.Start, element.Span.Length);
            string oldIndent = MarkupFormatting.StartsLine(text, element.Span.Start) ? MarkupFormatting.GetIndentation(text, element.Span.Start) : string.Empty;
            (TextChange insertion, int offset) = MarkupFormatting.CreateInsertion(document.Syntax, target, children, index, fragment, oldIndent);

            // Both changes are expressed against the current text; the moved element's new offset must account for the
            // removal when it comes first.
            int newStart = removal.End <= insertion.Span.Start ? offset - removal.Length : offset;
            TextChangeSet changes;
            try
            {
                changes = new TextChangeSet([new TextChange(removal, string.Empty), insertion]);
            }
            catch (ArgumentException)
            {
                return EditResult.Failure(element.NameSpan, $"'{element.Name}' can't be moved to that position.");
            }

            return Execute(new EditStep(changes, [new ElementMovedAction(node, newParent, newStart)], $"Move {element.Name}"));
        }

        /// <summary>
        /// Sets an attribute, adding it when the element doesn't have it yet.
        /// </summary>
        /// <param name="node">The element.</param>
        /// <param name="name">The attribute name as written in markup: <c>Width</c>, <c>Grid.Row</c>, <c>x:Name</c>.</param>
        /// <param name="value">
        /// The value as the property should receive it, or a markup extension such as <c>{Binding Path=Name}</c>. It is
        /// escaped for the attribute's quote character; don't escape it yourself.
        /// </param>
        /// <returns>The outcome.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <remarks>
        /// <para>Refused while <see cref="DesignDocument.IsInSync"/> is <see langword="false"/>: fix the markup first.</para>
        /// </remarks>
        public EditResult SetAttribute(NodeId node, string name, string value)
        {
            document.ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);
            if (RefuseWhileOutOfSync() is { } refused)
                return refused;

            if (MarkupParser.IsValidName(name) && document.TryFastSetAttribute(node, name, value, out EditResult? fast, out string? originalRaw))
            {
                if (fast.Succeeded)
                    UndoStack.RecordCoalesced(node, name, originalRaw!, $"Set {name}");
                return fast;
            }

            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (ValidateAttributeName(element, name) is { } failure)
                return failure;

            TextChange change = MarkupFormatting.CreateAttributeChange(document.Text, element, name, value);
            return Execute(new EditStep(new TextChangeSet([change]), [new AttributeChangedAction(node, name)], $"Set {name}"));
        }

        /// <summary>
        /// Removes an attribute, so the property falls back to its style or default value.
        /// </summary>
        /// <param name="node">The element.</param>
        /// <param name="name">The attribute name as written in markup.</param>
        /// <returns>The outcome; a success that changed nothing when the element has no such attribute.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <remarks>
        /// <para>Refused while <see cref="DesignDocument.IsInSync"/> is <see langword="false"/>: fix the markup first.</para>
        /// </remarks>
        public EditResult ClearAttribute(NodeId node, string name)
        {
            document.ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(name);
            if (RefuseWhileOutOfSync() is { } refused)
                return refused;

            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (ValidateAttributeName(element, name) is { } failure)
                return failure;
            if (element.FindAttribute(name) is not { } attribute)
                return EditResult.Success();

            TextSpan removal = MarkupFormatting.GetAttributeRemovalSpan(document.Text, attribute);
            return Execute(new EditStep(new TextChangeSet([new TextChange(removal, string.Empty)]), [new AttributeChangedAction(node, name)], $"Clear {name}"));
        }

        /// <summary>
        /// Groups every edit made until the returned object is disposed into one undo step.
        /// </summary>
        /// <param name="description">What the step does, as <see cref="UndoStack.UndoDescription"/> reports it.</param>
        /// <returns>The transaction; dispose it to close it. Transactions nest; only the outermost one records a step.</returns>
        /// <exception cref="ArgumentException"><paramref name="description"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        public IDisposable BeginTransaction(string description)
        {
            document.ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(description);
            return UndoStack.BeginTransaction(description);
        }

        internal EditResult Execute(EditStep step)
        {
            EditResult result = document.Apply(step, out EditStep? inverse);
            if (result.Succeeded)
                UndoStack.Record(inverse!, step.Description);
            return result;
        }

        private static EditResult UnknownNode(NodeId id) => EditResult.Failure(default, $"The document has no element {id}.");

        private EditResult? ValidateAttributeName(ElementSyntax element, string name)
        {
            if (!MarkupParser.IsValidName(name))
                return EditResult.Failure(element.NameSpan, $"'{name}' isn't a valid attribute name.");
            if (document.ClassifyAttribute(element, name) == AttributeKind.NamespaceDeclaration)
                return EditResult.Failure(element.NameSpan, "Namespace declarations can't be edited.");
            return null;
        }

        private EditResult? RefuseWhileOutOfSync() =>
            document.IsInSync ? null : EditResult.Failure(default, "The markup has errors; fix them first.");

        private EditResult? Validate(ElementSyntax parent, int contentCount, int index)
        {
            if ((uint)index > (uint)contentCount)
                return EditResult.Failure(parent.NameSpan, $"Index {index} is outside 0..{contentCount}.");

            string text = document.Text;
            foreach (MarkupSyntaxNode node in parent.Content)
            {
                if (node is TextSyntax or CDataSyntax && !text.AsSpan(node.Span.Start, node.Span.Length).IsWhiteSpace())
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}' holds text, so it can't also hold elements.");
            }

            // Opaque parents have no live copy to check; the text still changes and the page needs a reload.
            if (!document.IsEditable(parent))
                return null;

            foreach (object instance in document.GetObjects(document.GetNodeId(parent)!.Value))
            {
                if (!LiveContent.TryResolve(instance, document.Registry, out MarkupMember? member, out IList? list))
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}' can't hold child elements.");
                if (!LiveContent.Accepts(member, list, typeof(UIElement)))
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}.{member.Name}' can't hold elements.");
                if (list == null && contentCount > 0)
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}' holds a single child in '{member.Name}', and already has one.");
            }

            return null;
        }
    }
}
