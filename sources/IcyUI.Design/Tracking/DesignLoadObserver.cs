// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.Data.Bindings;
using Icy.Markup;

namespace Icy.Design.Tracking
{
    /// <summary>
    /// Forwards the loader's notifications to the session and to the document each scope belongs to.
    /// </summary>
    /// <remarks>
    /// It observes documents and merged dictionaries only. Template instantiations are opaque in this phase, and not
    /// observing them keeps pooled item containers free of any tracking cost.
    /// </remarks>
    internal sealed class DesignLoadObserver(DesignSession session) : IMarkupLoadObserver
    {
        public MarkupLoadScopeKind ObservedKinds => MarkupLoadScopeKind.Document | MarkupLoadScopeKind.MergedDictionary;

        public void DocumentStarted(MarkupLoadScope scope) => session.OnDocumentStarted(scope);

        public void ObjectCreated(MarkupLoadScope scope, XElement node, object instance) =>
            session.GetDocument(scope)?.OnObjectCreated(scope, node, instance);

        public void MemberApplied(MarkupLoadScope scope, XObject node, object target, MarkupMember member, object? value, IBinding? binding) =>
            session.GetDocument(scope)?.OnMemberApplied(node, target, member, binding);

        public void DocumentCompleted(MarkupLoadScope scope, object root) => session.OnDocumentCompleted(scope);

        public void DocumentFailed(MarkupLoadScope scope, MarkupException error) => session.OnDocumentFailed(scope);
    }
}
