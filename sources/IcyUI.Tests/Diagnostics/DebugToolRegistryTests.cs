using Icy.Diagnostics;
using Icy.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Diagnostics
{
    public class DebugToolRegistryTests
    {
        [Fact]
        public void Constructor_SeedsTheBuiltInTools()
        {
            var registry = new DebugToolRegistry();

            Assert.True(registry.Overlays.ContainsKey("Bounds"));
            Assert.True(registry.Overlays.ContainsKey("Focus"));
            Assert.True(registry.Panels.ContainsKey("DiagnosticsHud"));
        }

        [Fact]
        public void RegisterOverlay_Instance_IsRetrievableByName()
        {
            var registry = new DebugToolRegistry();
            var overlay = new FakeOverlay("Custom");

            registry.RegisterOverlay(overlay);

            Assert.Same(overlay, registry.Overlays["Custom"]);
        }

        [Fact]
        public void RegisterOverlay_SameTypeUnderSameName_IsIdempotent()
        {
            var registry = new DebugToolRegistry();
            registry.RegisterOverlay(new FakeOverlay("Custom"));

            registry.RegisterOverlay(new FakeOverlay("Custom"));

            Assert.IsType<FakeOverlay>(registry.Overlays["Custom"]);
        }

        [Fact]
        public void RegisterOverlay_DifferentTypeUnderSameName_Throws()
        {
            var registry = new DebugToolRegistry();
            registry.RegisterOverlay(new FakeOverlay("Custom"));

            Assert.Throws<DiagnosticsException>(() => registry.RegisterOverlay(new OtherFakeOverlay("Custom")));
        }

        [Fact]
        public void RegisterOverlay_TypeThatDoesNotImplementIDebugOverlay_Throws()
        {
            var registry = new DebugToolRegistry();

            Assert.Throws<DiagnosticsException>(() => registry.RegisterOverlay(typeof(string)));
        }

        [Fact]
        public void RegisterPanel_Instance_IsRetrievableByName()
        {
            var registry = new DebugToolRegistry();
            var panel = new FakePanel("Custom");

            registry.RegisterPanel(panel);

            Assert.Same(panel, registry.Panels["Custom"]);
        }

        [Fact]
        public void RegisterPanel_DifferentTypeUnderSameName_Throws()
        {
            var registry = new DebugToolRegistry();
            registry.RegisterPanel(new FakePanel("Custom"));

            Assert.Throws<DiagnosticsException>(() => registry.RegisterPanel(new OtherFakePanel("Custom")));
        }

        [Fact]
        public void RegisterPanel_TypeThatDoesNotImplementIDebugHudPanel_Throws()
        {
            var registry = new DebugToolRegistry();

            Assert.Throws<DiagnosticsException>(() => registry.RegisterPanel(typeof(string)));
        }

        private class FakeOverlay(string name) : IDebugOverlay
        {
            public string Name { get; } = name;

            public void Render(UIElement element, IRenderContext context)
            {
            }
        }

        private sealed class OtherFakeOverlay(string name) : FakeOverlay(name)
        {
        }

        private class FakePanel(string name) : IDebugHudPanel
        {
            public string Name { get; } = name;

            public IReadOnlyList<IDebugHudSection> Sections { get; } = [];
        }

        private sealed class OtherFakePanel(string name) : FakePanel(name)
        {
        }
    }
}
