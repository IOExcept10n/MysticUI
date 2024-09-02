// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Drawing;
using System.Numerics;

namespace AquaUI.MonoGame.Rendering
{
    /// <summary>
    /// Represents a <see langword="MonoGame"/> implementation of the <see cref="ITextureRenderer{TTexture, TGraphics}"/> type.
    /// </summary>
    /// <param name="device">An instance of the graphics device for the renderer.</param>
    /// <param name="context">Rendering context that controls this renderer instance.</param>
    internal class MonoGameRenderer(GraphicsDevice device, IRenderContext context) : ITextureRenderer<TextureWrapper, GraphicsDevice>, IDisposable
    {
        public GraphicsDevice GraphicsDevice { get; } = device;

        public IRenderContext Context { get; } = context;

        /// <summary>
        /// All the draw calls are redirected to the internal sprite batch instance.
        /// </summary>
        internal SpriteBatch SpriteBatch { get; } = new(device);

        public void Dispose()
        {
            SpriteBatch.Dispose();
        }

        public void Draw(TextureWrapper texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, float depth = 0)
        {
            SpriteBatch.Draw(texture.Texture, position.AsEngineVector(), sourceRectangle?.AsEngineRectangle(), color.AsEngineColor(), rotation, origin.AsEngineVector(), scale.AsEngineVector(), SpriteEffects.None, depth);
        }
    }
}