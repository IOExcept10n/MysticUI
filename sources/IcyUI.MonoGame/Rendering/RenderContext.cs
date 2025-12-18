// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Icy.MonoGame.Rendering
{
    /// <summary>
    /// Represents MonoGame implementation of the <see cref="IRenderContext"/> interface.
    /// </summary>
    public sealed class RenderContext : IRenderContext
    {
        private readonly GraphicsDevice device;
        private readonly SpriteBatch spriteBatch;

        private Effect? appliedEffect;
        private bool began;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderContext"/> class.
        /// </summary>
        /// <param name="device">An instance of the graphics device used in game.</param>
        public RenderContext(GraphicsDevice device)
        {
            this.device = device;
            spriteBatch = new(device);
            device.DeviceReset += (s, e) => ViewportResize?.Invoke(s, e);
            WhiteTexture = CreateTexture(2, 2, [Color.White, Color.White, Color.White, Color.White]);
            Options = new RenderOptions(this);
        }

        /// <inheritdoc/>
        public event EventHandler? ViewportResize;

        /// <inheritdoc/>
        public IRenderOptions Options { get; }

        /// <inheritdoc/>
        public ITexture WhiteTexture { get; }

        /// <inheritdoc/>
        public Transform2D Transform { get; set; }

        /// <inheritdoc/>
        public System.Drawing.Size ViewportSize => new(device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);

        /// <inheritdoc/>
        public void ApplyEffect(IEffect effect)
        {
            if (!Options.EnableEffects)
                return;

            if (effect is not Effect platformEffect)
            {
                string errorMessage = $"Effect type is not compatible with the MonoGame rendering system. " +
                    $"Expected type: {typeof(Effect)}, " +
                    $"actual type: {effect?.GetType().Name ?? "<null>"}";
                ThrowHelper.ThrowArgumentException(nameof(effect), errorMessage);
                return; // Unreachable
            }

            appliedEffect = platformEffect;

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

            spriteBatch.Begin(effect: appliedEffect);
            began = true;
        }

        /// <inheritdoc/>
        public void ClearEffects()
        {
            if (!Options.EnableEffects)
                return;
            appliedEffect = null;
            Flush();
        }

        /// <inheritdoc/>
        public ITexture CreateTexture<TColor>(int width, int height, TColor[] data)
            where TColor : unmanaged
        {
            var texture = new Texture2D(device, width, height);
            texture.SetData(data);
            return texture.Wrap();
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
            Texture2D tex = texture.Unwrap();
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
                Transform.Apply(pos.AsSystemVector()),
                options.Source?.AsEngineRectangle(),
                options.Color.AsEngineColor() * Options.Opacity,
                options.Rotation + Transform.Rotation,
                options.Origin,
                scale * Transform.Scale,
                SpriteEffects.None,
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
            if (began) End();
            Begin();
        }

        /// <inheritdoc/>
        public IEffect GetBuiltInEffect(EffectCode code)
        {
            throw new NotImplementedException();
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    WhiteTexture.Unwrap().Dispose();
                    spriteBatch.Dispose();
                }

                disposedValue = true;
            }
        }

        private class RenderOptions(RenderContext context) : IRenderOptions
        {
            public float Opacity { get; set; } = 1f;

            public System.Drawing.Rectangle Scissor { get => context.device.ScissorRectangle.AsSystemRectangle(); set => context.device.ScissorRectangle = value.AsEngineRectangle(); }

            public bool EnableEffects { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }
    }
}