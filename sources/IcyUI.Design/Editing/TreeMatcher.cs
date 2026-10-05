// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Text;
using Icy.Design.Syntax;
using Icy.Markup;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Diffs two markup trees structurally: <c>x:Name</c> anchors first, then a longest common subsequence of element
    /// names for the children of every pair.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named elements are paired wherever they are, which turns a named element in another parent into a move and keeps
    /// the live instance game code may hold. Unnamed elements are aligned per parent, so an unnamed element moved or
    /// reordered becomes a removal plus an insertion.
    /// </para>
    /// <para>
    /// Content under property elements (<c>&lt;Grid.RowDefinitions&gt;</c>, styles, templates) is opaque: it's paired
    /// structurally so ids survive, and any textual difference there only sets <see cref="TreeDiff.OpaqueChanged"/>.
    /// </para>
    /// </remarks>
    internal sealed class TreeMatcher
    {
        private readonly DocumentSyntax oldDocument;
        private readonly DocumentSyntax newDocument;
        private readonly TreeDiff diff = new();
        private readonly Dictionary<ElementSyntax, ElementSyntax> namedOldToNew = [];
        private readonly Dictionary<ElementSyntax, ElementSyntax> namedNewToOld = [];
        private readonly HashSet<ElementSyntax> handledOld = [];
        private readonly HashSet<ElementSyntax> alignedOld = [];

        private TreeMatcher(DocumentSyntax oldDocument, DocumentSyntax newDocument)
        {
            this.oldDocument = oldDocument;
            this.newDocument = newDocument;
        }

        /// <summary>
        /// Diffs <paramref name="oldDocument"/> against <paramref name="newDocument"/>. Both must have a root.
        /// </summary>
        public static TreeDiff Match(DocumentSyntax oldDocument, DocumentSyntax newDocument)
        {
            ArgumentNullException.ThrowIfNull(oldDocument);
            ArgumentNullException.ThrowIfNull(newDocument);
            ElementSyntax oldRoot = oldDocument.Root ?? throw new ArgumentException("The old tree has no root.", nameof(oldDocument));
            ElementSyntax newRoot = newDocument.Root ?? throw new ArgumentException("The new tree has no root.", nameof(newDocument));

            var matcher = new TreeMatcher(oldDocument, newDocument);
            if (oldRoot.Name != newRoot.Name)
            {
                matcher.diff.RootProblem = $"The root element changed from '{oldRoot.Name}' to '{newRoot.Name}'. Reload the page to see it.";
                return matcher.diff;
            }

            if (GetDirective(oldDocument, oldRoot, "Class") != GetDirective(newDocument, newRoot, "Class"))
            {
                matcher.diff.RootProblem = "The root element's x:Class changed. Reload the page to see it.";
                return matcher.diff;
            }

            matcher.IndexNames();
            matcher.Pair(oldRoot, newRoot);
            matcher.SweepDepartedNames();
            return matcher.diff;
        }

        /// <summary>
        /// Gets the value of an <c>x:</c> directive written on <paramref name="element"/>, such as <c>x:Name</c>.
        /// </summary>
        public static string? GetDirective(DocumentSyntax document, ElementSyntax element, string localName)
        {
            foreach (AttributeSyntax attribute in element.Attributes)
            {
                if (attribute.LocalName == localName && attribute.Prefix.Length > 0 && document.ResolvePrefix(element, attribute.Prefix) == MarkupNamespaces.Directives)
                    return attribute.Value;
            }

            return null;
        }

        private static bool IsInElementTree(ElementSyntax element)
        {
            for (ElementSyntax? current = element; current != null; current = current.Parent)
            {
                if (current.IsPropertyElement)
                    return false;
            }

            return true;
        }

        private static Dictionary<string, ElementSyntax> IndexNames(DocumentSyntax document)
        {
            var result = new Dictionary<string, ElementSyntax>(StringComparer.Ordinal);
            foreach (ElementSyntax element in document.Elements)
            {
                if (IsInElementTree(element) && GetDirective(document, element, MarkupDirectives.Name) is { } name)
                    result.TryAdd(name, element);
            }

            return result;
        }

        private static string NamespaceDeclarations(ElementSyntax element)
        {
            // Most elements declare nothing, so the builder only exists when there's something to collect.
            StringBuilder? builder = null;
            foreach (AttributeSyntax attribute in element.Attributes)
            {
                if (attribute.IsNamespaceDeclaration)
                    (builder ??= new StringBuilder()).Append(attribute.Name).Append('=').Append(attribute.Value).Append(';');
            }

            return builder?.ToString() ?? string.Empty;
        }

        private static string OwnText(DocumentSyntax document, ElementSyntax element)
        {
            StringBuilder? builder = null;
            foreach (MarkupSyntaxNode node in element.Content)
            {
                if (node is TextSyntax or CDataSyntax)
                    (builder ??= new StringBuilder()).Append(document.Text, node.Span.Start, node.Span.Length);
            }

            return builder?.ToString().Trim() ?? string.Empty;
        }

        private static string SpanText(DocumentSyntax document, ElementSyntax element) =>
            document.Text.Substring(element.Span.Start, element.Span.Length);

        private void IndexNames()
        {
            Dictionary<string, ElementSyntax> oldNames = IndexNames(oldDocument);
            Dictionary<string, ElementSyntax> newNames = IndexNames(newDocument);
            foreach ((string name, ElementSyntax old) in oldNames)
            {
                if (newNames.TryGetValue(name, out ElementSyntax? partner))
                {
                    namedOldToNew[old] = partner;
                    namedNewToOld[partner] = old;
                }
            }
        }

        private void Pair(ElementSyntax old, ElementSyntax current)
        {
            diff.Pairs[current] = old;
            handledOld.Add(old);

            if (old.Name != current.Name
                || NamespaceDeclarations(old) != NamespaceDeclarations(current)
                || OwnText(oldDocument, old) != OwnText(newDocument, current))
            {
                diff.Replaced.Add((old, current));
                return;
            }

            CompareAttributes(old, current);
            Align(old, current);
        }

        private void CompareAttributes(ElementSyntax old, ElementSyntax current)
        {
            foreach (AttributeSyntax attribute in current.Attributes)
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;

                AttributeSyntax? before = old.FindAttribute(attribute.Name);
                if (before == null || before.Value != attribute.Value)
                    diff.ChangedAttributes.Add((current, attribute.Name));
            }

            foreach (AttributeSyntax attribute in old.Attributes)
            {
                if (!attribute.IsNamespaceDeclaration && current.FindAttribute(attribute.Name) == null)
                    diff.ChangedAttributes.Add((current, attribute.Name));
            }
        }

        private void Align(ElementSyntax old, ElementSyntax current)
        {
            alignedOld.Add(old);

            // Leaves are most of a page and have nothing to align.
            if (!old.Elements.Any() && !current.Elements.Any())
                return;

            AlignPropertyElements(old, current);

            // Named elements whose partner lives under another parent are handled there, as moves.
            List<ElementSyntax> oldChildren = [.. old.ContentElements.Where(x => !(namedOldToNew.TryGetValue(x, out ElementSyntax? p) && !ReferenceEquals(p.Parent, current)))];
            List<ElementSyntax> newChildren = [.. current.ContentElements.Where(x => !(namedNewToOld.TryGetValue(x, out ElementSyntax? p) && !ReferenceEquals(p.Parent, old)))];

            var matchedOld = new HashSet<ElementSyntax>();
            var matchedNew = new HashSet<ElementSyntax>();
            foreach ((ElementSyntax a, ElementSyntax b) in LongestCommonSubsequence(oldChildren, newChildren))
            {
                matchedOld.Add(a);
                matchedNew.Add(b);
                Pair(a, b);
            }

            foreach (ElementSyntax b in newChildren)
            {
                if (matchedNew.Contains(b))
                    continue;

                if (namedNewToOld.TryGetValue(b, out ElementSyntax? partner) && !matchedOld.Contains(partner))
                {
                    // A named element reordered within this parent.
                    matchedOld.Add(partner);
                    MoveOrReplace(partner, b);
                }
                else
                {
                    diff.Inserted.Add(b);
                }
            }

            foreach (ElementSyntax a in oldChildren)
            {
                if (!matchedOld.Contains(a) && handledOld.Add(a))
                    diff.Removed.Add(a);
            }

            // Named elements that arrived here from another parent.
            foreach (ElementSyntax b in current.ContentElements)
            {
                if (namedNewToOld.TryGetValue(b, out ElementSyntax? partner) && !ReferenceEquals(partner.Parent, old) && !handledOld.Contains(partner))
                    MoveOrReplace(partner, b);
            }
        }

        private void MoveOrReplace(ElementSyntax old, ElementSyntax current)
        {
            if (old.Name == current.Name)
            {
                diff.Moved.Add((old, current));
                Pair(old, current);
                return;
            }

            // A different type somewhere else: there's nothing to keep.
            handledOld.Add(old);
            diff.Removed.Add(old);
            diff.Inserted.Add(current);
        }

        private void AlignPropertyElements(ElementSyntax old, ElementSyntax current)
        {
            List<ElementSyntax> oldProperties = [.. old.Elements.Where(x => x.IsPropertyElement)];
            foreach (ElementSyntax property in current.Elements.Where(x => x.IsPropertyElement))
            {
                int index = oldProperties.FindIndex(x => x.Name == property.Name);
                if (index < 0)
                {
                    diff.OpaqueChanged = true;
                    continue;
                }

                ElementSyntax match = oldProperties[index];
                oldProperties.RemoveAt(index);
                PairStructure(match, property);
                if (SpanText(oldDocument, match) != SpanText(newDocument, property))
                    diff.OpaqueChanged = true;
            }

            if (oldProperties.Count > 0)
                diff.OpaqueChanged = true;
        }

        private void PairStructure(ElementSyntax old, ElementSyntax current)
        {
            if (old.Name != current.Name)
                return;

            diff.Pairs[current] = old;
            handledOld.Add(old);
            using IEnumerator<ElementSyntax> oldChildren = old.Elements.GetEnumerator();
            using IEnumerator<ElementSyntax> newChildren = current.Elements.GetEnumerator();
            while (oldChildren.MoveNext() && newChildren.MoveNext())
                PairStructure(oldChildren.Current, newChildren.Current);
        }

        /// <summary>
        /// Removes named elements that left an aligned parent for one the diff never visits (inside an inserted or
        /// replaced element): they're rebuilt as part of that element, so the old instance has to go.
        /// </summary>
        private void SweepDepartedNames()
        {
            foreach ((ElementSyntax old, _) in namedOldToNew)
            {
                if (!handledOld.Contains(old) && old.Parent != null && alignedOld.Contains(old.Parent))
                {
                    handledOld.Add(old);
                    diff.Removed.Add(old);
                }
            }
        }

        private List<(ElementSyntax Old, ElementSyntax New)> LongestCommonSubsequence(List<ElementSyntax> oldChildren, List<ElementSyntax> newChildren)
        {
            // A common prefix and suffix always belong to some longest common subsequence, so they're paired directly
            // and only the middle pays for the table. A typical save touches a few children, which keeps a wide parent
            // linear instead of quadratic.
            int prefix = 0;
            int limit = Math.Min(oldChildren.Count, newChildren.Count);
            while (prefix < limit && Same(oldChildren[prefix], newChildren[prefix]))
                prefix++;

            int suffix = 0;
            while (suffix < limit - prefix && Same(oldChildren[^(suffix + 1)], newChildren[^(suffix + 1)]))
                suffix++;

            var pairs = new List<(ElementSyntax, ElementSyntax)>();
            for (int k = 0; k < prefix; k++)
                pairs.Add((oldChildren[k], newChildren[k]));

            int rows = oldChildren.Count - prefix - suffix;
            int columns = newChildren.Count - prefix - suffix;
            int[,] lengths = new int[rows + 1, columns + 1];
            for (int i = rows - 1; i >= 0; i--)
            {
                for (int j = columns - 1; j >= 0; j--)
                {
                    lengths[i, j] = Same(oldChildren[prefix + i], newChildren[prefix + j])
                        ? lengths[i + 1, j + 1] + 1
                        : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }

            for (int i = 0, j = 0; i < rows && j < columns;)
            {
                if (Same(oldChildren[prefix + i], newChildren[prefix + j]))
                {
                    pairs.Add((oldChildren[prefix + i], newChildren[prefix + j]));
                    i++;
                    j++;
                }
                else if (lengths[i + 1, j] >= lengths[i, j + 1])
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }

            for (int k = suffix; k > 0; k--)
                pairs.Add((oldChildren[^k], newChildren[^k]));

            return pairs;
        }

        private bool Same(ElementSyntax old, ElementSyntax current)
        {
            if (namedOldToNew.TryGetValue(old, out ElementSyntax? partner))
                return ReferenceEquals(partner, current);
            if (namedNewToOld.ContainsKey(current))
                return false;
            return old.Name == current.Name;
        }
    }
}
