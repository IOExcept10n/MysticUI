// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Design;
using Icy.Design.Editor;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;

namespace Icy.Tests.Design.Editor
{
    /// <summary>
    /// A tracked page on a rendered 800×600 canvas with fake input, and an attached <see cref="EditorSession"/>.
    /// </summary>
    internal sealed class EditorTestHost : IDisposable
    {
        private readonly EditorSession? session;

        public EditorTestHost(string markup, bool attachSession = true)
        {
            Input = new FakeInputSystem();
            Configuration = new IcyConfiguration(Input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            Configuration.Types.Markup.RegisterShortName<PickyPanel>();
            Configuration.Types.Markup.RegisterShortName<ClrPanel>();
            Design = DesignSession.Attach(Configuration);
            Root = new MarkupLoader(Configuration).Load(markup.ReplaceLineEndings("\n"), "page.xml");
            Document = Design.FindDocument(Root, out _)!;
            Canvas = new Canvas(Configuration) { IsInputEnabled = true, IsVisible = true };
            Canvas.Add(Root);
            Canvas.Render();
            if (attachSession)
                session = EditorSession.Attach(Design, Canvas);
        }

        public FakeInputSystem Input { get; }

        public IcyConfiguration Configuration { get; }

        public DesignSession Design { get; }

        public UIElement Root { get; }

        public DesignDocument Document { get; }

        public Canvas Canvas { get; }

        public FakeRenderContext RenderContext => (FakeRenderContext)Configuration.RenderContext;

        /// <summary>
        /// Gets the session the host attached. Hosts built with <c>attachSession: false</c> have none: the test attaches
        /// its own session or frame, since a canvas takes one session at a time.
        /// </summary>
        public EditorSession Session => session ?? throw new InvalidOperationException("This host was built without a session.");

        public T Named<T>(string name)
            where T : UIElement =>
            (T)(MarkupNameScope.GetScope(Root)!.Find(name) ?? throw new InvalidOperationException($"No element named '{name}'."));

        public NodeId IdOf(UIElement element) =>
            Design.FindDocument(element, out NodeId id) != null ? id : throw new InvalidOperationException("Not tracked.");

        /// <summary>
        /// The screen point at <paramref name="element"/>'s local position (<paramref name="x"/>, <paramref name="y"/>).
        /// </summary>
        public Point At(UIElement element, int x = 1, int y = 1) => element.PointToScreen(new System.Numerics.Vector2(x, y));

        public void Render() => Canvas.Render();

        public void Dispose()
        {
            session?.Dispose();
            Design.Dispose();
        }
    }
}
