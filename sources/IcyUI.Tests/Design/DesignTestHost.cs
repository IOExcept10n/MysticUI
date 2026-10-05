// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Design;
using Icy.Markup;
using Icy.Tests.Markup;
using Icy.UI;

namespace Icy.Tests.Design
{
    /// <summary>
    /// A configuration with an attached <see cref="DesignSession"/>, and helpers to load tracked pages.
    /// </summary>
    internal sealed class DesignTestHost : IDisposable
    {
        public DesignTestHost()
        {
            Configuration = MarkupLoadObserverTests.CreateConfiguration();
            Configuration.Types.Markup.RegisterShortName<ClrBox>();
            Configuration.Types.Markup.RegisterShortName<Labeled>();
            Configuration.Types.Markup.RegisterShortName<ThrowingBox>();
            Configuration.Types.Markup.RegisterShortName<PickyPanel>();
            Session = DesignSession.Attach(Configuration);
            Loader = new MarkupLoader(Configuration);
        }

        public IcyConfiguration Configuration { get; }

        public DesignSession Session { get; }

        public MarkupLoader Loader { get; }

        public static T Named<T>(UIElement root, string name)
            where T : UIElement =>
            (T)(MarkupNameScope.GetScope(root)?.Find(name) ?? throw new InvalidOperationException($"No element named '{name}'."));

        public (UIElement Root, DesignDocument Document) Load(string markup, string? sourcePath = "test.xml")
        {
            UIElement root = Loader.Load(markup, sourcePath);
            DesignDocument document = Session.FindDocument(root, out _)
                ?? throw new InvalidOperationException("The page wasn't tracked.");
            return (root, document);
        }

        public NodeId IdOf(object instance) =>
            Session.FindDocument(instance, out NodeId id) != null ? id : throw new InvalidOperationException($"'{instance}' isn't tracked.");

        public void Dispose() => Session.Dispose();
    }
}
