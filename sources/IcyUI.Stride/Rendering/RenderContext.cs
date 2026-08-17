// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Rendering;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Icy.Stride.Rendering
{
    /// <summary>
    /// Represents the Stride implementation of the <see cref="IRenderContext"/> interface.
    /// </summary>
    /// <remarks>
    /// Unlike MonoGame's <c>SpriteBatch.Begin</c>, Stride's requires a <see cref="Stride.Graphics.GraphicsContext"/>
    /// (frame-scoped command-submission state), not just a <see cref="GraphicsDevice"/>. Since this render context
    /// is constructed once but the render stage that drives it (see the Configuration namespace) only receives a
    /// valid <see cref="GraphicsContext"/> while it is actually being drawn (through
    /// <c>SceneRendererBase.DrawCore(RenderDrawContext)</c>), <see cref="GraphicsContext"/> here is a settable
    /// property the render stage refreshes every frame before calling into the UI tree, rather than a constructor
    /// parameter captured once.
    /// </remarks>
    public sealed class RenderContext : IRenderContext
    {
        private static readonly RasterizerStateDescription UIRasterizerState = RasterizerStateDescription.Default with { ScissorTestEnable = true };

        private readonly GraphicsDevice device;
        private readonly SpriteBatch spriteBatch;

        private GraphicsContext? graphicsContext;
        private bool began;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderContext"/> class.
        /// </summary>
        /// <param name="device">An instance of the graphics device used in game.</param>
        public RenderContext(GraphicsDevice device)
        {
            this.device = device;
            spriteBatch = new SpriteBatch(device);
            WhiteTexture = device.GetSharedWhiteTexture().Wrap(this);
            Options = new RenderOptions(this);
        }

        /// <inheritdoc/>
        public event EventHandler? ViewportResize;

        /// <summary>
        /// Gets or sets the frame-scoped Stride <see cref="Stride.Graphics.GraphicsContext"/> to render with.
        /// </summary>
        /// <remarks>
        /// The render stage that owns this context must set this once per frame, before any drawing happens
        /// through it - see the remarks on <see cref="RenderContext"/> itself.
        /// </remarks>
        public GraphicsContext GraphicsContext
        {
            get => graphicsContext ?? ThrowHelper.ThrowInvalidOperationException<GraphicsContext>(
                $"{nameof(GraphicsContext)} hasn't been set for this frame yet - it must be assigned by the owning render stage before drawing.");
            set => graphicsContext = value;
        }

        /// <inheritdoc/>
        public IRenderOptions Options { get; }

        /// <inheritdoc/>
        public ITexture WhiteTexture { get; }

        /// <inheritdoc/>
        public Transform2D Transform { get; set; }

        /// <inheritdoc/>
        public System.Drawing.Size ViewportSize => new(device.Presenter?.BackBuffer.Width ?? 0, device.Presenter?.BackBuffer.Height ?? 0);

        /// <inheritdoc/>
        public void ApplyEffect(IEffect effect)
        {
            if (!Options.EnableEffects)
                return;

            // No built-in effect can be constructed yet (GetBuiltInEffect throws), so there is currently no valid
            // IEffect instance any caller could pass here - matching MonoGame's RenderContext, which is in the
            // same state. Threading a real Stride Effect through SpriteBatch once GetBuiltInEffect is implemented
            // is future work, not a blocker for this port.
            Flush();
        }

        /// <inheritdoc/>
        public void Begin()
        {
            if (began)
            {
                ThrowHelper.ThrowInvalidOperationException($"Cannot begin rendering when it has been already started. " +
                    $"Please call {nameof(Flush)} instead of {nameof(Begin)}.");
            }

            spriteBatch.Begin(GraphicsContext, SpriteSortMode.Deferred, BlendStates.AlphaBlend, device.SamplerStates.LinearClamp, null, UIRasterizerState);
            began = true;
        }

        /// <inheritdoc/>
        public void ClearEffects()
        {
            if (!Options.EnableEffects)
                return;
            Flush();
        }

        /// <inheritdoc/>
        public ITexture CreateTexture<TColor>(int width, int height, TColor[] data)
            where TColor : unmanaged
        {
            Texture texture = Texture.New2D(device, width, height, PixelFormat.R8G8B8A8_UNorm, data);
            return texture.Wrap(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public void Draw(ITexture texture, in TextureRenderingOptions options)
        {
            Texture tex = texture.Unwrap();
            Vector2 size;
            if (options.Source != null)
            {
                size = new Vector2(options.Source.Value.Width, options.Source.Value.Height);
            }
            else
            {
                size = new Vector2(tex.Width, tex.Height);
            }

            var pos = new Vector2(options.Destination.X, options.Destination.Y);
            var scale = new Vector2(options.Destination.Width / size.X, options.Destination.Height / size.Y);

            spriteBatch.Draw(
                tex,
                Transform.Apply(pos.AsSystemVector()).AsEngineVector(),
                options.Source?.AsEngineRectangle(),
                options.Color.AsEngineColor() * Options.Opacity,
                options.Rotation + Transform.Rotation,
                Vector2.Zero,
                scale * Transform.Scale.AsEngineVector(),
                SpriteEffects.None,
                ImageOrientation.AsIs,
                options.Depth);
        }

        /// <inheritdoc/>
        public void End()
        {
            if (!began)
            {
                ThrowHelper.ThrowInvalidOperationException($"Cannot end rendering when it hasn't been started yet.");
            }

            spriteBatch.End();
            began = false;
        }

        /// <inheritdoc/>
        public void Flush()
        {
            // A no-op when not mid-render is required, not just a nicety: Canvas.RenderVisual() restores the
            // previous Options.Scissor *after* it has already called End() for the frame, and the Scissor setter
            // below calls Flush() on every assignment. Unconditionally re-Begin()-ing here (as MonoGame's Flush
            // does - safe there only because its Scissor setter never calls Flush) would silently reopen a batch
            // with no matching End(), leaking `began = true` into the next frame and making its first Begin() throw.
            if (!began)
                return;
            End();
            Begin();
        }

        /// <inheritdoc/>
        public IEffect GetBuiltInEffect(EffectCode code)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Raises <see cref="ViewportResize"/> - call this from the owning render stage when the back buffer
        /// (window/presenter) size changes, since Stride has no per-<see cref="GraphicsDevice"/> resize event to
        /// subscribe to the way MonoGame's <c>GraphicsDevice.DeviceReset</c> provides.
        /// </summary>
        public void NotifyViewportResize() => ViewportResize?.Invoke(this, EventArgs.Empty);

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    spriteBatch.Dispose();
                }

                disposedValue = true;
            }
        }

        private class RenderOptions(RenderContext context) : IRenderOptions
        {
            public float Opacity { get; set; } = 1f;

            public System.Drawing.Rectangle Scissor
            {
                get => scissor;
                set
                {
                    scissor = value;
                    context.Flush();
                    context.GraphicsContext.CommandList.SetScissorRectangle(value.AsEngineRectangle());
                }
            }

            public bool EnableEffects { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            private System.Drawing.Rectangle scissor;
        }
    }
}
