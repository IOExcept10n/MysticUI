// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Icy.Data.Markup;
using Icy.Markup;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Adds, removes and finds children through an object's markup content property
    /// (<see cref="ContentPropertyAttribute"/>), the same way the loader adds them.
    /// </summary>
    /// <remarks>
    /// Always pass the <b>logical</b> parent: the object built from the parent markup element. A templated control's
    /// content is parented by its chrome or <c>ContentPresenter</c> in the visual tree, so <c>UIElement.Parent</c> is
    /// the wrong object to ask.
    /// </remarks>
    internal static class LiveContent
    {
        public static bool TryResolve(object parent, PropertyRegistry registry, [NotNullWhen(true)] out MarkupMember? member, out IList? list)
        {
            member = null;
            list = null;
            Type type = parent.GetType();
            if (ContentPropertyAttribute.GetContentPropertyName(type) is not { } name || MarkupMember.Resolve(type, name, registry) is not { } resolved)
                return false;

            list = resolved.GetValue(parent) as IList;
            if (list == null && !resolved.CanSet)
                return false;

            member = resolved;
            return true;
        }

        public static bool Accepts(MarkupMember member, IList? list, Type childType)
        {
            Type itemType = list == null
                ? member.PropertyType
                : list.GetType().GetInterfaces()
                    .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>))?
                    .GetGenericArguments()[0] ?? typeof(object);
            return itemType.IsAssignableFrom(childType);
        }

        public static int IndexOf(object parent, object child, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                return -1;

            if (list != null)
                return list.IndexOf(child);

            return ReferenceEquals(member.GetValue(parent), child) ? 0 : -1;
        }

        public static int Count(object parent, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                return 0;

            return list?.Count ?? (member.GetValue(parent) != null ? 1 : 0);
        }

        public static void Insert(object parent, int index, object child, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                throw new DesignEditException($"'{parent.GetType().Name}' can't hold child elements.");

            if (list != null)
                list.Insert(Math.Clamp(index, 0, list.Count), child);
            else
                member.SetValue(parent, child);
        }

        public static void Remove(object parent, object child, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                return;

            if (list != null)
                list.Remove(child);
            else if (ReferenceEquals(member.GetValue(parent), child))
                member.SetValue(parent, null);
        }

        public static void Replace(object parent, object oldChild, object newChild, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                throw new DesignEditException($"'{parent.GetType().Name}' can't hold child elements.");

            if (list == null)
            {
                member.SetValue(parent, newChild);
                return;
            }

            // Remove + Insert rather than the indexer: collections such as UIElementCollection wire Parent only in
            // InsertItem/RemoveItem.
            int index = list.IndexOf(oldChild);
            list.RemoveAt(index);
            list.Insert(index, newChild);
        }
    }
}
