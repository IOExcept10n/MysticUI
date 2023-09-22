// This code is based on MyraUI project: https://github.com/rds1983/Myra
using FontStashSharp;
using FontStashSharp.RichText;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace MysticUI
{
    /// <summary>
    /// represents an object that handles rendering actions for the 2D textures.
    /// </summary>
    public partial class RenderContext : IDisposable
    {
        /// <summary>
        /// Transformations which are applied to all drawing process.
        /// </summary>
        public Transform Transform;

        private static readonly RasterizerStateDescription uiRasterizerState = RasterizerStateDescription.Default with { ScissorTestEnable = true };

        private readonly SpriteBatch renderer;

        private TextureFiltering filtering;
        private bool isRenderingStarted;
        private Rectangle scissor;
        private bool supportsEffects;

        /// <summary>
        /// Creates new instance of the <see cref="RenderContext"/> class.
        /// </summary>
        public RenderContext()
        {
            renderer = new SpriteBatch(EnvironmentSettingsProvider.EnvironmentSettings.GraphicsDevice);
            scissor = EnvironmentSettingsProvider.EnvironmentSettings.Game.GraphicsContext.CommandList.Scissor;
        }

        private enum TextureFiltering
        {
            Nearest,
            Linear
        }

        /// <summary>
        /// Opacity which is applied to all drawing process.
        /// </summary>
        public float Opacity { get; set; }

        /// <summary>
        /// Scissor rectangle.
        /// </summary>
        public Rectangle Scissor
        {
            get => scissor;
            set
            {
                scissor = value;
                var settings = EnvironmentSettingsProvider.EnvironmentSettings;
                if (!settings.DebugOptions.DisableClipping)
                {
                    Flush();
                    settings.Game.GraphicsContext.CommandList.SetScissorRectangle(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets if the render context supports visual effects such as shaders etc.
        /// </summary>
        public bool SupportsEffects
        {
            get => supportsEffects;
            set
            {
                supportsEffects = value;
                Flush();
            }
        }

        /// <summary>
        /// Begins the drawing phase.
        /// </summary>
        public void Begin()
        {
            var game = EnvironmentSettingsProvider.EnvironmentSettings.Game;

            var samplerState = filtering == TextureFiltering.Nearest ?
                game.GraphicsDevice.SamplerStates.PointClamp :
                game.GraphicsDevice.SamplerStates.LinearClamp;
            renderer.Begin(game.GraphicsContext, supportsEffects ? SpriteSortMode.Immediate : SpriteSortMode.Deferred, BlendStates.AlphaBlend, samplerState, null, uiRasterizerState);

            isRenderingStarted = true;
        }

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="position">Position to draw into.</param>
        /// <param name="sourceRectangle">Source area from the texture.</param>
        /// <param name="color">Color to apply to a texture.</param>
        /// <param name="rotation">Rotation to apply to a texture.</param>
        /// <param name="scale">Scale to apply.</param>
        /// <param name="depth">Z-layer depth.</param>
        public void Draw(Texture texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 scale, float depth = 0.0f)
        {
            SetFiltering(TextureFiltering.Nearest);
            color *= Opacity;
            scale *= Transform.Scale2D;
            rotation += Transform.Rotation2D;
            position = Transform.Apply(position);
            renderer.Draw(texture, position, sourceRectangle, color, rotation, Vector2.Zero, scale, SpriteEffects.None, ImageOrientation.AsIs, depth);
        }

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="destinationRectangle">Destination to draw into.</param>
        /// <param name="sourceRectangle">Source rectangle to draw from.</param>
        /// <param name="color">Color to apply.</param>
        /// <param name="rotation">Rotation to apply.</param>
        /// <param name="depth">Z-layer depth.</param>
        public void Draw(Texture texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, float depth = 0.0f)
        {
            Vector2 sz;
            if (sourceRectangle != null)
            {
                sz = new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height);
            }
            else
            {
                sz = new Vector2(texture.Width, texture.Height);
            }

            var pos = new Vector2(destinationRectangle.X, destinationRectangle.Y);
            var scale = new Vector2(destinationRectangle.Width / sz.X, destinationRectangle.Height / sz.Y);
            Draw(texture, pos, sourceRectangle, color, rotation, scale, depth);
        }

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="destinationRectangle">Destination to draw into.</param>
        /// <param name="sourceRectangle">Source to draw from.</param>
        /// <param name="color">Color to apply.</param>
        /// <param name="rotation">Rotation to apply.</param>
        public void Draw(Texture texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation) =>
            Draw(texture, destinationRectangle, sourceRectangle, color, rotation, 0.0f);

        /// <summary>
        /// Draws a texture
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="destinationRectangle">Destination to draw into.</param>
        /// <param name="sourceRectangle">Source to draw from.</param>
        /// <param name="color">Color to apply.</param>
        public void Draw(Texture texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color) =>
            Draw(texture, destinationRectangle, sourceRectangle, color, 0);

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="destinationRectangle">Destination to draw into.</param>
        /// <param name="color">Color to apply.</param>
        public void Draw(Texture texture, Rectangle destinationRectangle, Color color)
            => Draw(texture, destinationRectangle, null, color, 0);

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="position">Position to draw.</param>
        /// <param name="color">Color to apply.</param>
        /// <param name="scale">Scale to apply.</param>
        /// <param name="rotation">Rotation to apply.</param>
        public void Draw(Texture texture, Vector2 position, Color color, Vector2 scale, float rotation = 0.0f) =>
            Draw(texture, position, null, color, rotation, scale);

        /// <summary>
        /// Draws a texture
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="position"></param>
        /// <param name="sourceRectangle"></param>
        /// <param name="color"></param>
        /// <param name="rotation"></param>
        public void Draw(Texture texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation) =>
            Draw(texture, position, sourceRectangle, color, rotation, Vector2.One);

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="position">Position to draw.</param>
        /// <param name="sourceRectangle">Source rectangle to draw from.</param>
        /// <param name="color">Color to apply.</param>
        public void Draw(Texture texture, Vector2 position, Rectangle? sourceRectangle, Color color) =>
            Draw(texture, position, sourceRectangle, color, 0, Vector2.One);

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="position">Position to draw.</param>
        /// <param name="color">Color to apply.</param>
        public void Draw(Texture texture, Vector2 position, Color color) =>
            Draw(texture, position, null, color, 0, Vector2.One);

        /// <summary>
        /// Draws a rich text string.
        /// </summary>
        /// <param name="richText">The text which will be drawn.</param>
        /// <param name="position">The drawing location on screen.</param>
        /// <param name="color">A color mask.</param>
        /// <param name="sourceScale">A scaling of this text.</param>
        /// <param name="rotation">A rotation of this text in radians.</param>
        /// <param name="layerDepth">A depth of the layer of this string.</param>
        /// <param name="horizontalAlignment">Text horizontal alignment.</param>
        public void DrawRichText(RichTextLayout richText, Vector2 position, Color color,
            Vector2? sourceScale = null, float rotation = 0, float layerDepth = 0.0f,
            TextHorizontalAlignment horizontalAlignment = TextHorizontalAlignment.Left)
        {
            SetTextTextureFiltering();
            color *= Opacity;
            position = Transform.Apply(position);

            var scale = sourceScale ?? Vector2.One;

            scale *= Transform.Scale2D;
            rotation += Transform.Rotation2D;

            richText.Draw(renderer, position, color, scale, rotation, Vector2.Zero, layerDepth, horizontalAlignment);
        }

        /// <summary>
        /// Draws a text string.
        /// </summary>
        /// <param name="font">The font to draw text with.</param>
        /// <param name="text">The text which will be drawn.</param>
        /// <param name="position">The drawing location on screen.</param>
        /// <param name="color">A color mask.</param>
        /// <param name="rotation">A rotation of this text in radians.</param>
        /// <param name="scale">A scaling of this text.</param>
        /// <param name="layerDepth">A depth of the layer of this string.</param>
        public void DrawString(SpriteFontBase font, string text, Vector2 position, Color color, Vector2 scale, float rotation, float layerDepth = 0.0f)
        {
            SetTextTextureFiltering();
            color *= Opacity;
            position = Transform.Apply(position);

            scale *= Transform.Scale2D;
            rotation += Transform.Rotation2D;

            font.DrawText(renderer, text, position, color, scale, rotation, Vector2.Zero, layerDepth);
        }

        /// <summary>
        /// Draws a text string.
        /// </summary>
        /// <param name="font">The font to draw text with.</param>
        /// <param name="text">The text which will be drawn.</param>
        /// <param name="position">The drawing location on screen.</param>
        /// <param name="color">A color mask.</param>
        /// <param name="scale">A scaling of this text.</param>
        /// <param name="layerDepth">A depth of the layer of this string.</param>
        public void DrawString(SpriteFontBase font, string text, Vector2 position, Color color, Vector2 scale, float layerDepth = 0.0f) =>
            DrawString(font, text, position, color, scale, 0, layerDepth);

        /// <summary>
        /// Draws a text string.
        /// </summary>
        /// <param name="font">The font to draw text with.</param>
        /// <param name="text">The text which will be drawn.</param>
        /// <param name="position">The drawing location on screen.</param>
        /// <param name="color">A color mask.</param>
        /// <param name="layerDepth">A depth of the layer of this string.</param>
        public void DrawString(SpriteFontBase font, string text, Vector2 position, Color color, float layerDepth = 0.0f) =>
            DrawString(font, text, position, color, Vector2.One, 0, layerDepth);

        /// <summary>
        /// Ends the drawing phase.
        /// </summary>
        public void End()
        {
            renderer.End();
            isRenderingStarted = false;
        }

        /// <summary>
        /// Restarts the drawing phase.
        /// </summary>
        public void Flush()
        {
            if (isRenderingStarted)
            {
                End();
                Begin();
            }
        }

        private void SetFiltering(TextureFiltering value)
        {
            filtering = value;
            Flush();
        }

        private void SetTextTextureFiltering() =>
            SetFiltering(EnvironmentSettingsProvider.EnvironmentSettings.SmoothText ? TextureFiltering.Linear : TextureFiltering.Nearest);

        /// <inheritdoc/>
        public void Dispose()
        {
            renderer?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Destroys the renderer.
        /// </summary>
        ~RenderContext()
        {
            renderer?.Dispose();
        }
    }
}