using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    /// <summary>
    /// Covers <see cref="UIElement.Canvas"/> propagation down a pre-built subtree. The common construction pattern
    /// is "build the whole subtree first, then attach the root once" (e.g. <c>new Button { Content = new
    /// TextBlock(...) }</c> followed later by <see cref="Canvas.Add(UIElement)"/> on some ancestor) - at the time
    /// each nested relationship is established, the ancestor chain isn't attached yet, so descendants only get a
    /// correct <see cref="UIElement.Canvas"/> if attaching the root actually cascades down, not just to the
    /// immediate child. Without that cascade, every nested element's <see cref="UIElement.Configuration"/> (and
    /// anything depending on it, e.g. font resolution) silently stays <see langword="null"/> forever.
    /// </summary>
    public class CanvasAttachmentTests
    {
        private static Canvas CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return new Canvas(config);
        }

        [Fact]
        public void Add_PreBuiltDeepSubtree_PropagatesCanvasToEveryDescendant()
        {
            var canvas = CreateCanvas();
            var leaf = new UIElement();
            var middle = new Border { Child = leaf };
            var root = new Border { Child = middle };

            canvas.Add(root);

            Assert.Same(canvas, root.Canvas);
            Assert.Same(canvas, middle.Canvas);
            Assert.Same(canvas, leaf.Canvas);
        }

        [Fact]
        public void Add_ControlWithContentSetBeforeAttach_PropagatesCanvasIntoChrome()
        {
            var canvas = CreateCanvas();
            var label = new TextBlock { Text = "Hello" };
            var control = new ContentControl { Content = label };

            // Content was assigned while the control had no Canvas yet - the bug this guards against left
            // `label.Canvas` (and therefore `label.Configuration`) permanently null after this point.
            Assert.Null(label.Canvas);

            canvas.Add(control);

            Assert.Same(canvas, control.Canvas);
            Assert.Same(canvas, label.Canvas);
        }

        [Fact]
        public void Remove_DeepSubtree_ClearsCanvasFromEveryDescendant()
        {
            var canvas = CreateCanvas();
            var leaf = new UIElement();
            var middle = new Border { Child = leaf };
            var root = new Border { Child = middle };
            canvas.Add(root);

            canvas.Remove(root);

            Assert.Null(root.Canvas);
            Assert.Null(middle.Canvas);
            Assert.Null(leaf.Canvas);
        }
    }
}
