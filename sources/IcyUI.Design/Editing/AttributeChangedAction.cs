// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.Design.Syntax;
using Icy.Design.Tracking;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Makes every live copy of an element match one of its attributes as the current text has it.
    /// </summary>
    internal sealed class AttributeChangedAction(NodeId node, string name) : MirrorAction
    {
        private readonly List<(object Instance, AppliedMember Member)> droppedRecords = [];
        private readonly HashSet<object> touched = new(ReferenceEqualityComparer.Instance);
        private bool presentBefore;
        private bool presentAfter;
        private bool rebuilt;

        public override void Execute(MirrorContext context)
        {
            presentBefore = context.GetOldNode(node)?.FindAttribute(name) != null;
            presentAfter = context.Document.GetNode(node)?.FindAttribute(name) != null;
            Sync(context.Document, presentInOtherText: presentBefore, only: null);
        }

        public override void Revert(MirrorContext context)
        {
            // The document holds the old text again. Put back the records Execute dropped, without their bindings
            // (Execute already disposed those), so the old value is simply applied again and recreates any binding.
            foreach ((object instance, AppliedMember member) in droppedRecords)
                context.Document.Map.RecordMember(instance, name, member with { Binding = null });
            droppedRecords.Clear();

            // Only undo what Execute actually changed: a value that failed to convert changed nothing, and syncing
            // that copy anyway could rebuild it (or mark the document for reload) for an edit that never happened.
            if (rebuilt)
            {
                Sync(context.Document, presentInOtherText: presentAfter, only: null);
            }
            else if (touched.Count > 0)
            {
                var changed = new HashSet<object>(touched, ReferenceEqualityComparer.Instance);
                Sync(context.Document, presentInOtherText: presentAfter, only: changed);
            }

            touched.Clear();
            rebuilt = false;
        }

        public override MirrorAction CreateInverse(MirrorContext context) => new AttributeChangedAction(node, name);

        private static void SyncName(UIElement element, MarkupLoadScope scope, string? newName)
        {
            MarkupNameScope names = scope.NameScope
                ?? throw new DesignEditException("The page this document was loaded into no longer exists.");

            if (newName != null && names.TryFind(newName, out UIElement? other) && !ReferenceEquals(other, element))
                throw new DesignEditException($"The name '{newName}' is already used in this document.");

            if (element.Name is { } oldName && names.TryFind(oldName, out UIElement? registered) && ReferenceEquals(registered, element))
                names.Unregister(oldName);
            if (newName != null)
                names.Register(newName, element);

            element.Name = newName;
        }

        private void Sync(DesignDocument document, bool presentInOtherText, HashSet<object>? only)
        {
            if (document.GetNode(node) is not { } element)
                return;

            if (!document.IsEditable(element))
            {
                document.MarkNeedsReload();
                return;
            }

            AttributeKind kind = document.ClassifyAttribute(element, name);
            if (kind == AttributeKind.Directive)
            {
                RebuildAll(document, element);
                return;
            }

            AttributeSyntax? attribute = element.FindAttribute(name);
            foreach ((object instance, MarkupLoadScope scope) in document.Map.GetObjects(node))
            {
                if (only != null && !only.Contains(instance))
                    continue;

                if (kind == AttributeKind.Name)
                {
                    SyncName((UIElement)instance, scope, attribute?.Value);
                    touched.Add(instance);
                    continue;
                }

                AppliedMember? applied = DropRecord(document, instance);
                if (applied != null)
                    touched.Add(instance);

                if (attribute != null)
                {
                    if (applied == null && presentInOtherText)
                    {
                        // Present before but never recorded: consumed by a constructor, or not a plain member.
                        RebuildAll(document, element);
                        return;
                    }

                    document.ApplyAttribute(scope, instance, element, attribute);
                    touched.Add(instance);
                }
                else if (applied?.Member.Reference is { } reference)
                {
                    // ClearLocalValue alone only works once a style/state tier has touched the property; before
                    // that nothing remembers the default. Setting the default first makes it the fallback either
                    // way, and clearing then lets any active tier win over it.
                    reference.RawClearValue(instance);
                    reference.ClearLocalValue(instance);
                    touched.Add(instance);
                }
                else if (applied != null || presentInOtherText)
                {
                    // A plain CLR property has no "unset" to go back to; only a fresh object has its default.
                    RebuildAll(document, element);
                    return;
                }
            }
        }

        private void RebuildAll(DesignDocument document, ElementSyntax element)
        {
            // Set first: a rebuild that fails halfway may already have replaced some copies.
            rebuilt = true;
            document.Rebuild(element);
        }

        private AppliedMember? DropRecord(DesignDocument document, object instance)
        {
            if (!document.Map.TryGetEntry(instance, out ObjectMap.Entry? entry) || !entry.Members.Remove(name, out AppliedMember? applied))
                return null;

            droppedRecords.Add((instance, applied));
            if (applied.Binding is { } binding && instance is IBindingTarget target)
                target.Unbind(binding);
            return applied;
        }
    }
}
