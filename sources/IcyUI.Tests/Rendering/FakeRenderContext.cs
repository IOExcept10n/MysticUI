using System.Drawing;
using Icy.Rendering;

namespace Icy.Tests.Rendering
{
    /// <summary>
    /// A minimal <see cref="IRenderContext"/> test double - just enough bookkeeping (Begin/End balance, a settable
    /// Transform, a real-ish white texture) for unit tests to exercise UIElement's layout/transform/draw pipeline
    /// without a real graphics device.
    /// </summary>
    public sealed class FakeRenderContext : IRenderContext
    {
        private bool began;

        /// <summary>
        /// Gets the list of textures drawn since the context was created, in draw order.
        /// </summary>
        public List<(ITexture Texture, TextureRenderingOptions Options, Transform2D TransformAtDrawTime)> DrawCalls { get; } = [];

        /// <inheritdoc/>
        public event EventHandler? ViewportResize;

        /// <inheritdoc/>
        public IRenderOptions Options { get; } = new FakeRenderOptions();

        /// <inheritdoc/>
        public ITexture WhiteTexture { get; } = new FakeTexture(new Size(1, 1));

        /// <inheritdoc/>
        public Transform2D Transform { get; set; }

        /// <inheritdoc/>
        public Size ViewportSize { get; set; } = new(800, 600);

        /// <inheritdoc/>
        public void ApplyEffect(IEffect effect)
        {
        }

        /// <inheritdoc/>
        public void Begin() => began = true;

        /// <inheritdoc/>
        public void ClearEffects()
        {
        }

        /// <inheritdoc/>
        public ITexture CreateTexture<TColor>(int width, int height, TColor[] data)
            where TColor : unmanaged => new FakeTexture(new Size(width, height));

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public void Draw(ITexture texture, in TextureRenderingOptions options) => DrawCalls.Add((texture, options, Transform));

        /// <inheritdoc/>
        public void End() => began = false;

        /// <inheritdoc/>
        public void Flush()
        {
            if (began)
                End();
            Begin();
        }

        /// <inheritdoc/>
        public IEffect GetBuiltInEffect(EffectCode code) => throw new NotImplementedException();

        /// <summary>
        /// Raises <see cref="ViewportResize"/>, for tests that need to exercise resize handling.
        /// </summary>
        public void NotifyViewportResize() => ViewportResize?.Invoke(this, EventArgs.Empty);

        private sealed class FakeRenderOptions : IRenderOptions
        {
            public float Opacity { get; set; } = 1f;

            public Rectangle Scissor { get; set; }

            public bool EnableEffects { get; set; }
        }

        private sealed class FakeTexture(Size size) : ITexture
        {
            public Size Size { get; } = size;

            public void Dispose()
            {
            }

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
