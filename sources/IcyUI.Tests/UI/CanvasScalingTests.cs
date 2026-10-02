using System.Drawing;
using System.Runtime.CompilerServices;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasScalingTests
    {
        internal static (Canvas Canvas, FakeRenderContext Context, IcyConfiguration Configuration) Create(float displayScale = 1f, Size? viewport = null)
        {
            var context = new FakeRenderContext { DisplayScale = displayScale, ViewportSize = viewport ?? new Size(800, 600) };
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), context, new ReflectionConfiguration());
            return (new Canvas(configuration), context, configuration);
        }

        private static UIElement Stretch() => new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

        [Fact]
        public void DefaultDpiMode_UsesDisplayScale_AndShrinksSurface()
        {
            var (canvas, _, _) = Create(displayScale: 2f);
            UIElement root = Stretch();
            canvas.Add(root);

            canvas.Render();

            Assert.Equal(2f, canvas.EffectiveScale);
            Assert.Equal(new Size(400, 300), canvas.SurfaceSize);
            Assert.Equal(new Rectangle(0, 0, 400, 300), canvas.ContentBounds);
            Assert.Equal(new Size(400, 300), root.ActualBounds.Size);
        }

        [Fact]
        public void ScaleModeOverride_None_IgnoresDisplayScale()
        {
            var (canvas, _, _) = Create(displayScale: 2f);
            canvas.ScaleMode = UIScaleMode.None;

            canvas.Render();

            Assert.Equal(1f, canvas.EffectiveScale);
            Assert.Equal(new Size(800, 600), canvas.SurfaceSize);
        }

        [Fact]
        public void ConfigurationChange_IsPickedUpOnNextRender_AndReArrangesRoots()
        {
            var (canvas, _, configuration) = Create(displayScale: 1f);
            UIElement root = Stretch();
            canvas.Add(root);
            canvas.Render();

            configuration.Scaling.UserScale = 2f;
            canvas.Render();

            Assert.Equal(2f, canvas.EffectiveScale);
            Assert.Equal(new Size(400, 300), root.ActualBounds.Size);
        }

        [Fact]
        public void DisplayScaleChange_IsPickedUpOnNextRender()
        {
            var (canvas, context, _) = Create(displayScale: 1f);
            canvas.Render();

            context.DisplayScale = 1.5f;
            canvas.Render();

            Assert.Equal(1.5f, canvas.EffectiveScale);
            Assert.Equal(new Size(533, 400), canvas.SurfaceSize);
        }

        [Fact]
        public void ReferenceResolutionOverride_FitsReference()
        {
            var (canvas, _, _) = Create(displayScale: 2f, viewport: new Size(2880, 1920));
            canvas.ScaleMode = UIScaleMode.ReferenceResolution;
            canvas.ReferenceSize = new Size(1920, 1080);

            canvas.Render();

            Assert.Equal(1.5f, canvas.EffectiveScale, 3);
            Assert.Equal(new Size(1920, 1280), canvas.SurfaceSize);
        }

        [Fact]
        public void EffectiveScaleChange_RaisesPropertyChanged()
        {
            var (canvas, context, _) = Create(displayScale: 1f);
            canvas.Render();
            var changed = new List<string?>();
            canvas.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            context.DisplayScale = 2f;
            canvas.Render();

            Assert.Contains(nameof(Canvas.EffectiveScale), changed);
            Assert.Contains(nameof(Canvas.SurfaceSize), changed);
        }

        [Fact]
        public void ZeroViewport_ReferenceMode_RendersWithoutThrowing()
        {
            var (canvas, _, _) = Create(displayScale: 2f, viewport: Size.Empty);
            canvas.ScaleMode = UIScaleMode.ReferenceResolution;
            canvas.Add(Stretch());

            canvas.Render();

            Assert.Equal(1f, canvas.EffectiveScale);
            Assert.Equal(Size.Empty, canvas.SurfaceSize);
        }

        [Fact]
        public void DiscardedCanvas_IsNotKeptAliveByConfiguration()
        {
            var (_, _, configuration) = Create();
            WeakReference weak = CreateAndDropCanvas(configuration);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(weak.IsAlive);
            GC.KeepAlive(configuration);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateAndDropCanvas(IcyConfiguration configuration)
        {
            var canvas = new Canvas(configuration);
            canvas.Render();
            return new WeakReference(canvas);
        }
    }
}
