using Icy.Assets;
using Icy.Configuration;
using Icy.Diagnostics;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Diagnostics
{
    /// <summary>
    /// Proves the debug-visualization toggle gate: zero extra draw calls when disabled, and that each activation
    /// path (canvas-wide, per-element override, per-element drill-in) does what it says.
    /// </summary>
    public class DebugVisualizationGatingTests
    {
        private static (Canvas Canvas, FakeRenderContext RenderContext) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), renderContext);
        }

        [Fact]
        public void Render_WithNothingActive_DrawsNothingExtra()
        {
            (Canvas canvas, FakeRenderContext context) = CreateCanvas();
            var element = new UIElement { Width = 50, Height = 50 };
            canvas.Add(element);

            canvas.Render();

            // A plain UIElement's OnRender is empty and nothing else is registered - a real zero baseline, not
            // just "unchanged from some other run".
            Assert.Empty(context.DrawCalls);
        }

        [Fact]
        public void Render_WithBoundsActive_DrawsTheBoxModelOverlay()
        {
            (Canvas canvas, FakeRenderContext context) = CreateCanvas();
            var element = new UIElement { Width = 50, Height = 50 };
            canvas.Add(element);
            canvas.ActiveDebugTools.Add("Bounds");

            canvas.Render();

            // Margin + border boxes (a plain UIElement has no padding/content box - see BoxModelResolverTests),
            // 4 strips each.
            Assert.Equal(8, context.DrawCalls.Count);
        }

        [Fact]
        public void Render_PerElementOverrideExcludesOneElement_WhileSiblingsStillGetTheOverlay()
        {
            (Canvas canvas, FakeRenderContext context) = CreateCanvas();
            var excluded = new UIElement { Width = 50, Height = 50 };
            var included = new UIElement { Width = 50, Height = 50 };
            Debug.SetVisualization(excluded, string.Empty);
            canvas.Add(excluded);
            canvas.Add(included);
            canvas.ActiveDebugTools.Add("Bounds");

            canvas.Render();

            Assert.Equal(8, context.DrawCalls.Count);
        }

        [Fact]
        public void Render_PerElementDrillIn_RequiresAllowPerElementDebugOverridesEvenWithNothingActiveCanvasWide()
        {
            (Canvas canvas, FakeRenderContext context) = CreateCanvas();
            var element = new UIElement { Width = 50, Height = 50 };
            Debug.SetVisualization(element, "Bounds");
            canvas.Add(element);

            canvas.Render();
            Assert.Empty(context.DrawCalls);

            canvas.AllowPerElementDebugOverrides = true;
            canvas.Render();
            Assert.Equal(8, context.DrawCalls.Count);
        }

        [Fact]
        public void Render_WithDiagnosticsHudActive_DrawsMoreThanWithoutIt()
        {
            (Canvas canvas, FakeRenderContext context) = CreateCanvas();
            var element = new UIElement { Width = 50, Height = 50 };
            canvas.Add(element);

            canvas.Render();
            int withoutHud = context.DrawCalls.Count;

            context.DrawCalls.Clear();
            canvas.ActiveDebugTools.Add("DiagnosticsHud");
            canvas.Render();

            Assert.True(context.DrawCalls.Count > withoutHud);
        }
    }
}
