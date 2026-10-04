// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Tracking
{
    /// <summary>
    /// Maps markup nodes to the live objects built from them, and back, without keeping any of them alive.
    /// </summary>
    /// <remarks>
    /// One node can have several objects: one per load of the same file. Node → objects uses weak references, and
    /// object → entry uses a <see cref="ConditionalWeakTable{TKey, TValue}"/>, so an element the game drops is
    /// collected as usual.
    /// </remarks>
    internal sealed class ObjectMap
    {
        private readonly Dictionary<NodeId, List<WeakReference<object>>> objectsByNode = [];
        private readonly ConditionalWeakTable<object, Entry> entries = new();

        public int Count
        {
            get
            {
                int count = 0;
                foreach (NodeId id in objectsByNode.Keys.ToList())
                    count += GetObjects(id).Count;
                return count;
            }
        }

        public void Add(NodeId id, object instance, MarkupLoadScope scope)
        {
            if (entries.TryGetValue(instance, out Entry? existing))
            {
                if (existing.Id == id)
                    return;
                Remove(instance);
            }

            entries.Add(instance, new Entry(id, scope));
            if (!objectsByNode.TryGetValue(id, out List<WeakReference<object>>? list))
                objectsByNode[id] = list = [];
            list.Add(new WeakReference<object>(instance));
        }

        public List<(object Instance, MarkupLoadScope Scope)> GetObjects(NodeId id)
        {
            var result = new List<(object, MarkupLoadScope)>();
            if (!objectsByNode.TryGetValue(id, out List<WeakReference<object>>? list))
                return result;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].TryGetTarget(out object? instance) && entries.TryGetValue(instance, out Entry? entry))
                    result.Add((instance, entry.Scope));
                else
                    list.RemoveAt(i);
            }

            if (list.Count == 0)
                objectsByNode.Remove(id);

            result.Reverse();
            return result;
        }

        public object? FindObject(NodeId id, MarkupLoadScope scope)
        {
            foreach ((object instance, MarkupLoadScope owner) in GetObjects(id))
            {
                if (ReferenceEquals(owner, scope))
                    return instance;
            }

            return null;
        }

        public object? FirstAlive()
        {
            foreach (NodeId id in objectsByNode.Keys.ToList())
            {
                if (GetObjects(id) is [var first, ..])
                    return first.Instance;
            }

            return null;
        }

        public bool TryGetEntry(object instance, [NotNullWhen(true)] out Entry? entry) => entries.TryGetValue(instance, out entry);

        public void RecordMember(object target, string attributeName, AppliedMember member)
        {
            if (entries.TryGetValue(target, out Entry? entry))
                entry.Members[attributeName] = member;
        }

        public void Remove(object instance)
        {
            if (!entries.TryGetValue(instance, out Entry? entry))
                return;

            entries.Remove(instance);
            if (objectsByNode.TryGetValue(entry.Id, out List<WeakReference<object>>? list))
                list.RemoveAll(x => !x.TryGetTarget(out object? target) || ReferenceEquals(target, instance));
        }

        public void RemoveNode(NodeId id)
        {
            foreach ((object instance, _) in GetObjects(id))
                entries.Remove(instance);
            objectsByNode.Remove(id);
        }

        /// <summary>
        /// Forgets <paramref name="root"/> and every mapped element whose parent chain leads to it.
        /// </summary>
        public void RemoveSubtree(UIElement root)
        {
            foreach (NodeId id in objectsByNode.Keys.ToList())
            {
                foreach ((object instance, _) in GetObjects(id))
                {
                    if (instance is UIElement element && IsSelfOrDescendant(element, root))
                        Remove(instance);
                }
            }
        }

        public void RemoveScope(MarkupLoadScope scope)
        {
            foreach (NodeId id in objectsByNode.Keys.ToList())
            {
                foreach ((object instance, MarkupLoadScope owner) in GetObjects(id))
                {
                    if (ReferenceEquals(owner, scope))
                        Remove(instance);
                }
            }
        }

        internal static bool IsSelfOrDescendant(UIElement candidate, UIElement root)
        {
            for (UIElement? element = candidate; element != null; element = element.Parent)
            {
                if (ReferenceEquals(element, root))
                    return true;
            }

            return false;
        }

        internal sealed class Entry(NodeId id, MarkupLoadScope scope)
        {
            public NodeId Id { get; } = id;

            public MarkupLoadScope Scope { get; } = scope;

            public Dictionary<string, AppliedMember> Members { get; } = new(StringComparer.Ordinal);
        }
    }
}
