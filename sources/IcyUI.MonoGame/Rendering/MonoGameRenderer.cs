// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.Rendering.Fonts;
using CommunityToolkit.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using System.Drawing;
using System.Numerics;

namespace Icy.MonoGame.Rendering
{
    /// <summary>
    /// Represents a <see langword="MonoGame"/> implementation of the <see cref="ITextureRenderer{TTexture, TGraphics}"/> type.
    /// </summary>
    /// <param name="device">An instance of the graphics device for the renderer.</param>
    /// <param name="context">Rendering context that controls this renderer instance.</param>
    internal class MonoGameRenderer(GraphicsDevice device, IRenderContext context) : ITextureRenderer<TextureAdapter, GraphicsDevice>
    {
        private bool disposedValue;
        private Effect? appliedEffect;
        private bool began;

        public GraphicsDevice GraphicsDevice { get; } = device;

        public IRenderContext Context { get; } = context;

        public IRenderOptions Options => throw new NotImplementedException();

        /// <summary>
        /// All the draw calls are redirected to the internal sprite batch instance.
        /// </summary>
        internal SpriteBatch SpriteBatch { get; } = new(device);

        public void ApplyEffect(IEffect effect)
        {
            if (!Options.EnableEffects)
                return;

            if (effect is not Effect mgEffect)
            {
                string errorMessage = $"Effect type is not compatible with the MonoGame rendering system. " +
                    $"Expected type: {typeof(Effect)}, " +
                    $"actual type: {effect?.GetType() ?? Type.Missing}";
                ThrowHelper.ThrowArgumentException(nameof(effect), errorMessage);
                return; // Unreachable
            }

            appliedEffect = mgEffect;

            Flush();
        }

        public void Begin()
        {
            if (began)
            {
                ThrowHelper.ThrowInvalidOperationException($"Cannot begin rendering when it has been already started. " +
                    $"Please call {nameof(Flush)} instead of {nameof(Begin)}.");
            }
            SpriteBatch.Begin(effect: appliedEffect);
            began = true;
        }

        public void ClearEffects()
        {
            if (!Options.EnableEffects)
                return;
            appliedEffect = null;
            Flush();
        }

        public void Dispose()
        {
            SpriteBatch.Dispose();
        }

        public void Draw(TextureAdapter texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, float depth = 0)
        {
            SpriteBatch.Draw(texture.Texture, position.AsEngineVector(), sourceRectangle?.AsEngineRectangle(), color.AsEngineColor(), rotation, origin.AsEngineVector(), scale.AsEngineVector(), SpriteEffects.None, depth);
        }

        public void Draw(IBrush brush, Rectangle destination, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float depth = 0)
        {
            if (!began)
                ThrowHelper.ThrowInvalidOperationException($"Cannot draw until rendering has been started. Please call {nameof(Begin)} to begin drawing.");
            // Yes, yet another redirection.
            brush.Draw(this, destination, sourceRectangle, color, rotation, origin, depth);
        }

        public void DrawString(IFont font, string text, Vector2 position, float layerDepth, Color color, Vector2 scale, float rotation, Vector2 origin)
        {
            if (font is not FontAdapter mgFont)
            {
                ThrowHelper.ThrowArgumentException(nameof(font), "Unsupported font type.");
                return;
            }
            SpriteBatch.DrawString(mgFont.Font, text, position.AsEngineVector(), color.AsEngineColor(), rotation, origin, scale, SpriteEffects.None, layerDepth);
        }

        public void End()
        {
            if (!began)
            {
                ThrowHelper.ThrowInvalidOperationException($"Cannot end rendering when it hasn't been started yet.");
            }
            SpriteBatch.End();
            began = false;
        }

        public void Flush()
        {
            if (began) End();
            Begin();
        }

        public IEffect GetBuiltInEffect(EffectCode code)
        {
            throw new NotImplementedException();
        }
    }
}
