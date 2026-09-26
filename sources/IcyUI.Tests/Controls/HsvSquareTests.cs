// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Linq;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Rendering;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class HsvSquareTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input);
        }

        [Fact]
        public void Defaults_MatchSpec()
        {
            var square = new HsvSquare();

            Assert.Equal(0f, square.Hue);
            Assert.Equal(0f, square.Saturation);
            Assert.Equal(1f, square.Value);
        }

        [Fact]
        public void Saturation_Value_AreClampedTo0_1()
        {
            var square = new HsvSquare
            {
                Saturation = 1.5f,
                Value = -0.5f,
            };

            Assert.Equal(1f, square.Saturation);
            Assert.Equal(0f, square.Value);
        }

        [Fact]
        public void Dragging_UpdatesSaturationAndValue_FromThePointerPosition()
        {
            var (canvas, _) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var square = new HsvSquare
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            canvas.Add(square);
            canvas.Render();

            // Element should now be at (0, 0) to (100, 100) in canvas space = screen space
            var bounds = square.ActualBounds;
            bool raised = false;
            square.SaturationValueChanged += (_, _) => raised = true;

            // Drag to top-left corner (0, 0) in screen space -> Saturation=0, Value=1
            InvokeOnDragStarted(square, new Point(bounds.X, bounds.Y));

            Assert.True(raised);
            Assert.Equal(0f, square.Saturation, 2);
            Assert.Equal(1f, square.Value, 2);

            // Drag to bottom-right corner -> Saturation=1, Value=0
            InvokeOnDragPerforming(square, new Point(bounds.Right, bounds.Bottom));

            Assert.Equal(1f, square.Saturation, 2);
            Assert.Equal(0f, square.Value, 2);
        }

        [Fact]
        public void OnRender_NestedAtANonZeroOffset_DrawsInLocalSpace_NotAbsoluteActualBounds()
        {
            // Regression: OnRender used to build its TextureRenderingOptions/marker position directly from
            // ActualBounds, which is absolute/cumulative (see UIElement.UpdateTransformMatrix's own remarks) -
            // context.Transform already carries this element's own screen-position contribution by the time
            // OnRender runs, so using ActualBounds again here double-applied the offset, rendering the square (and
            // its marker) away from its own, correctly hit-tested area. A parent-less square at (0,0) can't catch
            // this (ActualBounds happens to equal local (0,0) there too) - nesting inside a padded Border, matching
            // how ColorPicker actually nests HsvSquare a few levels deep, gives it a real non-zero ActualBounds.
            var (canvas, _) = CreateCanvas();
            var square = new HsvSquare { Width = 100, Height = 100 };
            var border = new Border { Padding = new Thickness(20), Child = square };
            canvas.Add(border);
            canvas.Render();

            Assert.NotEqual(0, square.ActualBounds.X);
            Assert.NotEqual(0, square.ActualBounds.Y);

            var context = (FakeRenderContext)canvas.Configuration.RenderContext;

            // The square's own texture draw is the one sized to its full 100x100 bounds - DrawCircle's marker
            // (drawn afterward, in the same OnRender call) produces several small 1x1-ish line-segment draws.
            var squareDrawCall = context.DrawCalls.Single(call => call.Options.Destination is { Width: 100, Height: 100 });

            Assert.Equal(0, squareDrawCall.Options.Destination.X);
            Assert.Equal(0, squareDrawCall.Options.Destination.Y);
        }

        [Fact]
        public void EnsureTexture_DisposesThePreviousTexture_WhenTheHueChanges()
        {
            // Regression: EnsureTexture created a new ITexture whenever its cache key (Hue/width/height) changed,
            // but never disposed the texture it was replacing - since the cache key includes Hue, dragging the hue
            // slider allocates a fresh ~90KB texture on every value the drag passes through, leaking every one but
            // the last.
            var context = new DisposeTrackingRenderContext();
            var config = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), context, new ReflectionConfiguration());
            var canvas = new Canvas(config);
            var square = new HsvSquare { Width = 10, Height = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(square);

            canvas.Render();
            var first = (DisposeTrackingTexture)context.LastCreatedTexture!;

            square.Hue = 120f;
            canvas.Render();
            var second = (DisposeTrackingTexture)context.LastCreatedTexture!;

            Assert.NotSame(first, second);
            Assert.True(first.IsDisposed);
            Assert.False(second.IsDisposed);
        }

        [Fact]
        public void OnDetached_DisposesTheCachedTexture()
        {
            // Regression: HsvSquare had no OnDetached override, so the final cached texture was never released
            // when the element left the tree - nothing else would ever call EnsureTexture again to trigger the
            // disposal-of-the-superseded-texture path added alongside this test.
            var context = new DisposeTrackingRenderContext();
            var config = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), context, new ReflectionConfiguration());
            var canvas = new Canvas(config);
            var square = new HsvSquare { Width = 10, Height = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(square);
            canvas.Render();
            var texture = (DisposeTrackingTexture)context.LastCreatedTexture!;

            canvas.Remove(square);

            Assert.True(texture.IsDisposed);
        }

        private static void InvokeOnDragStarted(UIElement element, Point screenPoint) =>
            typeof(UIElement).GetMethod("OnDragStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(element, [screenPoint]);

        private static void InvokeOnDragPerforming(UIElement element, Point screenPoint) =>
            typeof(UIElement).GetMethod("OnDragPerforming", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(element, [screenPoint]);

        /// <summary>
        /// A minimal <see cref="IRenderContext"/> test double that, unlike <see cref="FakeRenderContext"/>/
        /// <see cref="Icy.Tests.Rendering.Brushes.RecordingFakeRenderContext"/>, returns textures that actually
        /// record whether <see cref="ITexture.Dispose"/> was called - neither existing test double supports that,
        /// and this is the minimum needed to assert on <see cref="HsvSquare"/>'s own texture-lifecycle fix.
        /// </summary>
        private sealed class DisposeTrackingRenderContext : IRenderContext
        {
            public event EventHandler? ViewportResize;

            public IRenderOptions Options { get; } = new FakeOptions();

            public ITexture WhiteTexture { get; } = new DisposeTrackingTexture(new Size(1, 1));

            public Transform2D Transform { get; set; }

            public Size ViewportSize { get; set; } = new(800, 600);

            public ITexture? LastCreatedTexture { get; private set; }

            public void ApplyEffect(IEffect effect)
            {
            }

            public void Begin()
            {
            }

            public void ClearEffects()
            {
            }

            public ITexture CreateTexture<TColor>(int width, int height, TColor[] data)
                where TColor : unmanaged
            {
                var texture = new DisposeTrackingTexture(new Size(width, height));
                LastCreatedTexture = texture;
                return texture;
            }

            public void Dispose()
            {
            }

            public void Draw(ITexture texture, in TextureRenderingOptions options)
            {
            }

            public void End()
            {
            }

            public void Flush()
            {
            }

            public IEffect GetBuiltInEffect(EffectCode code) => throw new NotImplementedException();

            private sealed class FakeOptions : IRenderOptions
            {
                public float Opacity { get; set; } = 1f;

                public Rectangle Scissor { get; set; }

                public bool EnableEffects { get; set; }
            }
        }

        private sealed class DisposeTrackingTexture(Size size) : ITexture
        {
            public Size Size { get; } = size;

            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;

            public void GetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int elementCount)
                where TColor : struct
            {
            }

            public void SetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int elementCount)
                where TColor : struct
            {
            }
        }
    }
}
