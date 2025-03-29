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
            WhiteTexture = CreateTexture(2, 2, [Color.White, Color.White, Color.White, Color.White]);
        }

        /// <inheritdoc/>
        public IRenderOptions Options => throw new NotImplementedException();

        /// <inheritdoc/>
        public ITexture WhiteTexture { get; }

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
        public void Draw(ITexture texture, in TextureRenderingOptions options) => spriteBatch.Draw(texture.Unwrap(), options);

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
    }
}