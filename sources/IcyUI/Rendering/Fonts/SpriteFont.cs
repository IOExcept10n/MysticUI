// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using Icy.Data;
using Icy.Rendering.Brushes;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a base class for the sprite fonts. It supports sprite rendering and calculating text render bounds.
    /// </summary>
    public abstract class SpriteFont : ISpanDrawableFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpriteFont"/> class.
        /// </summary>
        /// <param name="info">Info about the created font.</param>
        protected SpriteFont(FontInfo info)
        {
            Info = info;
        }

        /// <inheritdoc/>
        public abstract IReadOnlyDictionary<int, FontGlyph> Glyphs { get; }

        /// <inheritdoc/>
        public FontInfo Info { get; protected set; }

        /// <inheritdoc/>
        public FontMetrics Metrics { get; protected set; }

        /// <summary>
        /// Gets or sets a codepoint of the default font character.
        /// </summary>
        /// <remarks>
        /// This character will be returned in case when requested character not found in the font.
        /// </remarks>
        public int DefaultCodepoint { get; set; }

        /// <summary>
        /// Gets or sets the multiplier for the font rendering size.
        /// </summary>
        protected float RenderSizeMultiplier { get; set; } = 1f;

        /// <inheritdoc/>
        public Rectangle CalculateBounds(ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            if (text.IsEmpty)
                return Rectangle.Empty;
            Transform2D transform = CreateTransform(options);
            Prepare(text, options, out int ascent, out int lineHeight);
            BoundsInfo bounds = new(new(0, ascent));
            foreach (int c in text.EnumerateCodepoints())
            {
                if (HandleControlCode(c, options, lineHeight, ref bounds))
                    continue;
                UpdateBounds(c, options, ref bounds);
            }

            return transform.Apply(Rectangle.FromLTRB((int)bounds.Min.X, (int)bounds.Min.Y, (int)bounds.Max.X, (int)bounds.Max.Y));
        }

        /// <inheritdoc/>
        public Rectangle CalculateBounds(string text, in FontRenderingOptions options) => CalculateBounds(text.AsSpan(), options);

        /// <inheritdoc/>
        public List<RenderGlyph> GetRenderGlyphs(ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            List<RenderGlyph> result = [];
            if (text.IsEmpty)
                return result;
            Transform2D transform = CreateTransform(options);
            Prepare(text, options, out int ascent, out int lineHeight);
            BoundsInfo renderBounds = new(new(0, ascent));
            int i = 0;
            foreach (int codepoint in text.EnumerateCodepoints())
            {
                Rectangle glyphBounds = new((int)renderBounds.Location.X, (int)renderBounds.Location.Y - lineHeight, 0, lineHeight);
                float horizontalAdvance = 0;
                if (!HandleControlCode(codepoint, options, lineHeight, ref renderBounds))
                {
                    GetGlyphBounds(codepoint, options, ref renderBounds, ref glyphBounds, ref horizontalAdvance);
                }

                var transformed = transform.Apply(glyphBounds);
                result.Add(new RenderGlyph(i++, codepoint, transformed, (int)horizontalAdvance));
            }

            return result;
        }

        /// <inheritdoc/>
        public List<RenderGlyph> GetRenderGlyphs(string text, in FontRenderingOptions options) => GetRenderGlyphs(text.AsSpan(), options);

        /// <inheritdoc/>
        public Vector2 MeasureString(ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            var bounds = CalculateBounds(text, options);
            return new(bounds.Width, bounds.Height);
        }

        /// <inheritdoc/>
        public Vector2 MeasureString(string text, in FontRenderingOptions options) => MeasureString(text.AsSpan(), options);

        /// <inheritdoc/>
        public void DrawString(IRenderContext context, ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            if (text.IsEmpty)
                return;
            Transform2D transform = CreateTransform(options);
            Prepare(text, options, out int ascent, out int lineHeight);
            BoundsInfo renderBounds = new(new(0, ascent));
            foreach (int codepoint in text.EnumerateCodepoints())
            {
                if (HandleControlCode(codepoint, options, lineHeight, ref renderBounds))
                    continue;

                var glyph = GetGlyph(codepoint, options);
                if (glyph == FontGlyph.None)
                    continue;

                AddSpacing(glyph, options.CharacterSpacing, ref renderBounds);
                if (!glyph.IsEmpty)
                {
                    var glyphBounds = glyph.GetRenderRectangle(renderBounds.Location);
                    var renderGlyphBounds = transform.Apply(glyphBounds);
                    var glyphTexture = GetGlyphTexture(glyph);
                    var textureGlyphBounds = glyph.GetTextureRectangle(glyphTexture.Size);
                    TextureRenderingOptions renderOptions = new(
                        Destination: renderGlyphBounds,
                        Source: textureGlyphBounds,
                        Color: options.Color,
                        Rotation: options.Rotation,
                        Origin: options.Origin,
                        Depth: options.Depth);

                    context.Draw(glyphTexture, renderOptions);
                }
            }
        }

        /// <inheritdoc/>
        public void DrawString(IRenderContext context, string text, in FontRenderingOptions options) =>
            ((ISpanDrawableFont)this).DrawString(context, text.AsSpan(), options);

        /// <summary>
        /// Gets the glyph for the specified character codepoint.
        /// </summary>
        /// <param name="codepoint">Codepoint to get glyph for.</param>
        /// <param name="options">Rendering options to get glyph more precise.</param>
        /// <returns>Glyph info to use with this font.</returns>
        protected abstract FontGlyph GetGlyph(int codepoint, in FontRenderingOptions options);

        /// <summary>
        /// Gets an image for the specified glyph inside this font.
        /// </summary>
        /// <param name="glyph">Glyph to get image for.</param>
        /// <returns>An instance of the texture atlas for the specified glyph.</returns>
        protected abstract ITexture GetGlyphTexture(FontGlyph glyph);

        /// <summary>
        /// Gets the kerning between two glyphs.
        /// </summary>
        /// <param name="current">Current glyph info.</param>
        /// <param name="previous">Previous glyph info.</param>
        /// <returns>Kerning value in pixels.</returns>
        protected abstract float GetKerning(FontGlyph current, FontGlyph previous);

        /// <summary>
        /// Prepares for the rendering and calculates font parameters such as <paramref name="ascent"/> and <paramref name="lineHeight"/>.
        /// </summary>
        /// <param name="text">Text to prepare.</param>
        /// <param name="options">Options for the rendering to prepare.</param>
        /// <param name="ascent">Ascent value for the specified text.</param>
        /// <param name="lineHeight">Line height for the specified text.</param>
        protected abstract void Prepare(ReadOnlySpan<char> text, in FontRenderingOptions options, out int ascent, out int lineHeight);

        private static bool HandleControlCode(int codepoint, in FontRenderingOptions options, int lineHeight, ref BoundsInfo bounds)
        {
            switch (codepoint)
            {
                case '\n':
                    bounds.Location.Y += lineHeight + options.LineSpacing;
                    bounds.Location.X = options.Position.X;
                    bounds.Previous = FontGlyph.None;
                    return true;

                case '\r':
                    bounds.Location.X = options.Position.X;
                    bounds.Previous = FontGlyph.None;
                    return true;

                case '\b':
                    bounds.Location = bounds.LastLocation;
                    return true;
            }

            return false;
        }

        private void AddSpacing(FontGlyph glyph, float spacing, ref BoundsInfo bounds)
        {
            if (bounds.Previous != FontGlyph.None)
                bounds.Location.X += spacing + GetKerning(glyph, bounds.Previous);
        }

        private Transform2D CreateTransform(in FontRenderingOptions options) =>
            Transform2D.Create(options.Position, options.Rotation, options.Origin, (options.Scale ?? Vector2.One) * RenderSizeMultiplier);

        private void GetGlyphBounds(int codepoint, FontRenderingOptions options, ref BoundsInfo renderBounds, ref Rectangle glyphBounds, ref float horizontalAdvance)
        {
            var glyph = GetGlyph(codepoint, options);
            if (glyph != FontGlyph.None)
            {
                AddSpacing(glyph, options.CharacterSpacing, ref renderBounds);
                glyphBounds = glyph.GetRenderRectangle(renderBounds.Location);
                horizontalAdvance = glyph.Advance;
                renderBounds.Location.X += horizontalAdvance;
                renderBounds.Previous = glyph;
            }
        }

        private void UpdateBounds(int codepoint, in FontRenderingOptions options, ref BoundsInfo bounds)
        {
            var glyph = GetGlyph(codepoint, options);
            if (glyph == FontGlyph.None)
                return;

            AddSpacing(glyph, options.CharacterSpacing, ref bounds);

            // Update bounds
            bounds.Min = Vector2.Min(bounds.Location + glyph.Bearing, bounds.Min);
            bounds.Max = Vector2.Max(bounds.Location + new Vector2(glyph.Advance, glyph.Size.Height), bounds.Max);

            bounds.Previous = glyph;
            bounds.LastLocation = bounds.Location;
        }

        private struct BoundsInfo
        {
            public Vector2 LastLocation;
            public Vector2 Location;
            public Vector2 Max;
            public Vector2 Min;
            public FontGlyph Previous;

            public BoundsInfo(Vector2 position)
            {
                Min = Max = Location = position;
                Previous = FontGlyph.None;
            }
        }
    }
}
