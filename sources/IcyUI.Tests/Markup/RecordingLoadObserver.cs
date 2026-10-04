// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;
using System.Xml.Linq;
using Icy.Data.Bindings;
using Icy.Markup;

namespace Icy.Tests.Markup
{
    /// <summary>
    /// Records every notification as a short string, so tests can assert on the exact order.
    /// </summary>
    internal sealed class RecordingLoadObserver(MarkupLoadScopeKind observedKinds = MarkupLoadScopeKind.All) : IMarkupLoadObserver
    {
        public List<string> Events { get; } = [];

        public List<MarkupLoadScope> Scopes { get; } = [];

        public List<(XObject Node, object Target, MarkupMember Member, IBinding? Binding)> Members { get; } = [];

        public MarkupLoadScopeKind ObservedKinds { get; } = observedKinds;

        public void DocumentStarted(MarkupLoadScope scope)
        {
            Scopes.Add(scope);
            Events.Add($"Started:{scope.Kind}");
        }

        public void ObjectCreated(MarkupLoadScope scope, XElement node, object instance)
        {
            var info = (IXmlLineInfo)node;
            Events.Add($"Created:{instance.GetType().Name}@{info.LineNumber}:{info.LinePosition}");
        }

        public void MemberApplied(MarkupLoadScope scope, XObject node, object target, MarkupMember member, object? value, IBinding? binding)
        {
            Members.Add((node, target, member, binding));
            Events.Add($"Applied:{member.Name}");
        }

        public void DocumentCompleted(MarkupLoadScope scope, object root) => Events.Add($"Completed:{root.GetType().Name}");

        public void DocumentFailed(MarkupLoadScope scope, MarkupException error) => Events.Add("Failed");
    }
}
