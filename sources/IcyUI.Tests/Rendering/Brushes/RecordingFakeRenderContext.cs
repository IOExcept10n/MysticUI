// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;

namespace Icy.Tests.Rendering.Brushes
{
    /// <summary>
    /// A minimal <see cref="IRenderContext"/> test double for <see cref="Icy.Rendering.Brushes.GradientBrush"/> tests -
    /// records what the last <see cref="CreateTexture{TColor}(int, int, TColor[])"/> call uploaded, so tests can assert
    /// on the actual pixel buffer a brush built rather than on a mock invocation.
    /// </summary>
    public class RecordingFakeRenderContext : IRenderContext
    {
        /// <inheritdoc/>
        public event EventHandler? ViewportResize;

        /// <summary>
        /// Gets the number of times <see cref="CreateTexture{TColor}(int, int, TColor[])"/> has been called.
        /// </summary>
        public int CreateTextureCallCount { get; private set; }

        /// <summary>
        /// Gets the texture instance returned by the most recent <see cref="CreateTexture{TColor}(int, int, TColor[])"/> call.
        /// </summary>
        public ITexture? LastCreatedTexture { get; private set; }

        /// <summary>
        /// Gets the pixel data passed to the most recent <see cref="CreateTexture{TColor}(int, int, TColor[])"/> call,
        /// or <see langword="null"/> if it wasn't called with <see cref="Rgba32"/> data.
        /// </summary>
        public Rgba32[]? LastTextureData { get; private set; }

        /// <inheritdoc/>
        public IRenderOptions Options { get; } = new FakeRenderOptions();

        /// <inheritdoc/>
        public ITexture WhiteTexture { get; } = new RecordedTexture(new Size(1, 1));

        /// <inheritdoc/>
        public Transform2D Transform { get; set; }

        /// <inheritdoc/>
        public Size ViewportSize { get; set; } = new(800, 600);

        /// <inheritdoc/>
        public void ApplyEffect(IEffect effect)
        {
        }

        /// <inheritdoc/>
        public void Begin()
        {
        }

        /// <inheritdoc/>
        public void ClearEffects()
        {
        }

        /// <inheritdoc/>
        public ITexture CreateTexture<TColor>(int width, int height, TColor[] data)
            where TColor : unmanaged
        {
            CreateTextureCallCount++;
            LastTextureData = data as Rgba32[];
            var texture = new RecordedTexture(new Size(width, height));
            LastCreatedTexture = texture;
            return texture;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public void Draw(ITexture texture, in TextureRenderingOptions options)
        {
        }

        /// <inheritdoc/>
        public void End()
        {
        }

        /// <inheritdoc/>
        public void Flush()
        {
        }

        /// <inheritdoc/>
        public virtual IEffect GetBuiltInEffect(EffectCode code) => throw new NotImplementedException();

        private sealed class FakeRenderOptions : IRenderOptions
        {
            public float Opacity { get; set; } = 1f;

            public Rectangle Scissor { get; set; }

            public bool EnableEffects { get; set; }
        }

        private sealed class RecordedTexture(Size size) : ITexture
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

    /// <summary>
    /// A <see cref="RecordingFakeRenderContext"/> that additionally counts how many times
    /// <see cref="GetBuiltInEffect(EffectCode)"/> is called, for tests asserting that
    /// <see cref="Icy.Rendering.Brushes.GradientBrush"/> only probes it once per instance.
    /// </summary>
    public sealed class ThrowingGetBuiltInEffectRenderContext : RecordingFakeRenderContext
    {
        /// <summary>
        /// Gets the number of times <see cref="GetBuiltInEffect(EffectCode)"/> has been called.
        /// </summary>
        public int GetBuiltInEffectCallCount { get; private set; }

        /// <inheritdoc/>
        public override IEffect GetBuiltInEffect(EffectCode code)
        {
            GetBuiltInEffectCallCount++;
            throw new NotImplementedException();
        }
    }
}
