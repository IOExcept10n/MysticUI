// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Rendering;
using AquaUI.Rendering.Brushes;
using CommunityToolkit.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using System.Drawing;
using System.Numerics;

namespace AquaUI.MonoGame.Rendering
{
    public class RenderContext : IRenderContext
    {
        private bool disposedValue;
        private Effect? appliedEffect;
        private bool began;

        public IRenderOptions Options => throw new NotImplementedException();

        internal MonoGameRenderer Renderer { get; }

        public RenderContext(GraphicsDevice device)
        {
            Renderer = new(device, this);
        }

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

        // TODO
        public void ApplyMask(ITexture texture, Transform2D transform)
        {
            throw new NotImplementedException();
        }

        public void Begin()
        {
            if (began)
            {
                ThrowHelper.ThrowInvalidOperationException($"Cannot begin rendering when it has been already started. " +
                    $"Please call {nameof(Flush)} instead of {nameof(Begin)}.");
            }
            Renderer.SpriteBatch.Begin(effect: appliedEffect);
            began = true;
        }

        public void ClearEffects()
        {
            if (!Options.EnableEffects)
                return;
            appliedEffect = null;
            Flush();
        }

        // TODO
        public void ClearMask()
        {
            throw new NotImplementedException();
        }

        public void Draw(IBrush brush, Rectangle destination, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float depth = 0)
        {
            if (!began)
                ThrowHelper.ThrowInvalidOperationException($"Cannot draw until rendering has been started. Please call {nameof(Begin)} to begin drawing.");
            // Yes, yet another redirection.
            brush.Draw(Renderer, destination, sourceRectangle, color, rotation, origin, depth);
        }

        // HACK
        public void DrawString(IFont font, string text, Vector2 position, float layerDepth, Color color, Vector2 scale, float rotation, Vector2 origin)
        {
            if (font is not FontWrapper mgFont)
            {
                ThrowHelper.ThrowArgumentException(nameof(font), "Unsupported font type.");
                return;
            }
            Renderer.SpriteBatch.DrawString(mgFont.Font, text, position.AsEngineVector(), color.AsEngineColor(), rotation, origin, scale, SpriteEffects.None, layerDepth);
        }

        public void End()
        {
            if (!began)
            {
                ThrowHelper.ThrowInvalidOperationException($"Cannot end rendering when it hasn't been started yet.");
            }
            Renderer.SpriteBatch.End();
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Renderer.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}