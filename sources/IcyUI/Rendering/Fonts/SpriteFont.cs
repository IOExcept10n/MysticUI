// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Frozen;
using System.Drawing;
using System.Numerics;
using Icy.Data;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a base class for the sprite fonts. It supports sprite rendering and calculating text render bounds.
    /// </summary>
    /// <remarks>
    /// The <see cref="SpriteFont"/> class provides core functionality for rendering text using sprite-based fonts.
    /// It handles:
    /// <list type="bullet">
    /// <item><description>Glyph positioning and metrics</description></item>
    /// <item><description>Baseline alignment</description></item>
    /// <item><description>Kerning and character spacing</description></item>
    /// <item><description>Multi-line text with proper line spacing</description></item>
    /// </list>
    /// </remarks>
    public abstract class SpriteFont : ISpanDrawableFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpriteFont"/> class.
        /// </summary>
        /// <param name="info">Info about the created font.</param>
        /// <param name="atlas">The texture atlas containing the font's glyphs.</param>
        /// <param name="fontResolver">An instance of the service that helps with finding fallback fonts for cases when glyph is not found.</param>
        protected SpriteFont(FontInfo info, IFontAtlas atlas, IFallbackFontResolver? fontResolver)
        {
            Info = info;
            Atlas = atlas;
            FontResolver = fontResolver;
        }

        /// <summary>
        /// Gets an instance of the <see cref="IFontAtlas"/> that stores all the font glyphs.
        /// </summary>
        public IFontAtlas Atlas { get; }

        /// <summary>
        /// Gets or sets a codepoint of the default font character.
        /// </summary>
        /// <remarks>
        /// This character will be returned in case when requested character not found in the font.
        /// </remarks>
        public int DefaultCodepoint { get; set; }

        /// <inheritdoc/>
        public IReadOnlyDictionary<int, FontGlyph> Glyphs => Atlas.Glyphs.ToFrozenDictionary(x => x.Key.Codepoint, y => y.Value);

        /// <inheritdoc/>
        public FontInfo Info { get; protected set; }

        /// <inheritdoc/>
        public FontMetrics Metrics { get; protected set; }

        /// <summary>
        /// Gets or sets an instance of the service to resolve fallback fonts when character is not found.
        /// </summary>
        public IFallbackFontResolver? FontResolver { get; protected set; }

        /// <summary>
        /// Gets or sets the multiplier for the font rendering size.
        /// </summary>
        protected float RenderSizeMultiplier { get; set; } = 1f;

        /// <inheritdoc/>
        public Rectangle CalculateBounds(ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            if (text.IsEmpty)
                return Rectangle.Empty;

            var localOptions = options;
            Transform2D transform = CreateTransform(localOptions);
            Prepare(text, localOptions, out int baseline, out int lineHeight);

            // Initialize bounds with the baseline position
            BoundsInfo bounds = new(new Vector2(0, baseline), localOptions.Position.X);
            Vector2 min = bounds.Location;
            Vector2 max = bounds.Location;

            ProcessText(text, localOptions, lineHeight, ref bounds, (glyph, font, glyphPos) =>
            {
                min = Vector2.Min(min, glyphPos);
                max = Vector2.Max(max, glyphPos + new Vector2(glyph.Size.Width, glyph.Size.Height));
            });

            return transform.Apply(Rectangle.FromLTRB((int)min.X, (int)min.Y, (int)max.X, (int)max.Y));
        }

        /// <inheritdoc/>
        public Rectangle CalculateBounds(string text, in FontRenderingOptions options) =>
            CalculateBounds(text.AsSpan(), options);

        /// <inheritdoc/>
        public void DrawString(IRenderContext context, ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            if (text.IsEmpty)
                return;

            var localOptions = options;
            Transform2D transform = CreateTransform(localOptions);
            Prepare(text, localOptions, out int baseline, out int lineHeight);
            BoundsInfo renderBounds = new(new Vector2(0, baseline), localOptions.Position.X);

            ProcessText(text, localOptions, lineHeight, ref renderBounds, (glyph, font, glyphPos) =>
            {
                if (!glyph.IsEmpty)
                {
                    Rectangle glyphBounds = new((int)glyphPos.X, (int)glyphPos.Y, glyph.Size.Width, glyph.Size.Height);

                    var renderGlyphBounds = transform.Apply(glyphBounds);
                    var glyphTexture = ((font as SpriteFont) ?? this)?.GetGlyphTexture(glyph);

                    // Unfortunately, we can't support fonts that can't provide a texture for the specified glyph.
                    if (glyphTexture == null)
                        return;

                    var textureGlyphBounds = glyph.TextureRegion;

                    TextureRenderingOptions renderOptions = new(
                        Destination: renderGlyphBounds,
                        Source: textureGlyphBounds,
                        Color: localOptions.Color,
                        Rotation: localOptions.Rotation,
                        Origin: localOptions.Origin,
                        Depth: localOptions.Depth);

                    context.Draw(glyphTexture, renderOptions);
                }
            });
        }

        /// <inheritdoc/>
        public void DrawString(IRenderContext context, string text, in FontRenderingOptions options) =>
            DrawString(context, text.AsSpan(), options);

        /// <inheritdoc/>
        public List<RenderGlyph> GetRenderGlyphs(ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            List<RenderGlyph> result = [];
            if (text.IsEmpty)
                return result;

            var localOptions = options;
            Transform2D transform = CreateTransform(localOptions);
            Prepare(text, localOptions, out int baseline, out int lineHeight);
            BoundsInfo renderBounds = new(new Vector2(0, baseline), localOptions.Position.X);
            int i = 0;

            ProcessText(text, localOptions, lineHeight, ref renderBounds, (glyph, font, glyphPos) =>
            {
                Rectangle glyphBounds = new((int)glyphPos.X, (int)glyphPos.Y, glyph.Size.Width, glyph.Size.Height);

                var transformed = transform.Apply(glyphBounds);
                result.Add(new RenderGlyph(i++, glyph.Codepoint, transformed, (int)glyph.Advance));
            });

            return result;
        }

        /// <inheritdoc/>
        public List<RenderGlyph> GetRenderGlyphs(string text, in FontRenderingOptions options) =>
            GetRenderGlyphs(text.AsSpan(), options);

        /// <inheritdoc/>
        public Vector2 MeasureString(ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            var bounds = CalculateBounds(text, options);
            return new(bounds.Width, bounds.Height);
        }

        /// <inheritdoc/>
        public Vector2 MeasureString(string text, in FontRenderingOptions options) =>
            MeasureString(text.AsSpan(), options);

        /// <inheritdoc/>
        public abstract bool SupportsCharacter(int codepoint);

        /// <summary>
        /// Gets the glyph for the specified character codepoint.
        /// </summary>
        /// <param name="codepoint">Codepoint to get glyph for.</param>
        /// <returns>Glyph info to use with this font.</returns>
        /// <remarks>
        /// If the requested codepoint is not found in the font, this method will attempt to return
        /// the glyph for <see cref="DefaultCodepoint"/>. If that also fails, it returns <see cref="FontGlyph.None"/>.
        /// </remarks>
        public virtual FontGlyph GetGlyph(int codepoint)
        {
            if (Atlas.GetGlyph(GetStyledGlyph(codepoint)) is FontGlyph glyph)
                return glyph;
            if (DefaultCodepoint != 0 && Atlas.GetGlyph(GetStyledGlyph(DefaultCodepoint)) is FontGlyph defaultGlyph)
                return defaultGlyph;
            return FontGlyph.None;
        }

        /// <summary>
        /// Gets an image for the specified glyph inside this font.
        /// </summary>
        /// <param name="glyph">Glyph to get image for.</param>
        /// <returns>An instance of the texture atlas for the specified glyph.</returns>
        protected ITexture GetGlyphTexture(FontGlyph glyph) => Atlas.GetGlyphPage(glyph);

        /// <summary>
        /// Gets the kerning between two glyphs.
        /// </summary>
        /// <param name="current">Current glyph info.</param>
        /// <param name="previous">Previous glyph info.</param>
        /// <returns>Kerning value in pixels.</returns>
        /// <remarks>
        /// Kerning adjusts the spacing between specific pairs of glyphs to improve visual appearance.
        /// For example, in "VA", the 'A' might be moved slightly left to tuck under the 'V'.
        /// </remarks>
        protected abstract float GetKerning(FontGlyph current, FontGlyph previous);

        /// <summary>
        /// Gets an instance of the <see cref="StyledGlyphDefinition"/> for the specified codepoint for this font instance.
        /// </summary>
        /// <param name="codepoint">Character codepoint to get glyph info for.</param>
        /// <returns>An instance of the <see cref="StyledGlyphDefinition"/> with info from this font and specified <paramref name="codepoint"/>.</returns>
        protected StyledGlyphDefinition GetStyledGlyph(int codepoint) => new(codepoint, Info.Size, Info.Style);

        /// <summary>
        /// Prepares for the rendering and calculates font parameters such as <paramref name="baseline"/> and <paramref name="lineHeight"/>.
        /// </summary>
        /// <param name="text">Text to prepare.</param>
        /// <param name="options">Options for the rendering to prepare.</param>
        /// <param name="baseline">The baseline position for text rendering.</param>
        /// <param name="lineHeight">The total height of a line of text.</param>
        /// <remarks>
        /// The baseline is the imaginary line upon which most characters sit. Some characters may descend below it (like 'g', 'j', 'p').
        /// The line height determines the vertical space between lines of text, including ascenders and descenders.
        /// </remarks>
        protected virtual void Prepare(ReadOnlySpan<char> text, in FontRenderingOptions options, out int baseline, out int lineHeight)
        {
            baseline = (int)(Metrics.Ascent + options.Position.Y);
            lineHeight = (int)(Metrics.Ascent - Metrics.Descent + Metrics.LineGap);
        }

        private static bool HandleControlCode(int codepoint, in FontRenderingOptions options, int lineHeight, ref BoundsInfo bounds)
        {
            switch (codepoint)
            {
                case '\n':
                    bounds.Location.Y += lineHeight + options.LineSpacing;
                    bounds.Location.X = 0;
                    bounds.Previous = FontGlyph.None;
                    return true;

                case '\r':
                    bounds.Location.X = 0;
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

        /// <summary>
        /// Processes text by iterating over codepoints and handling glyphs.
        /// </summary>
        /// <param name="text">Text to process.</param>
        /// <param name="options">Rendering options.</param>
        /// <param name="lineHeight">Height of a line of text.</param>
        /// <param name="bounds">Current bounds information.</param>
        /// <param name="glyphCallback">
        /// Callback to handle each glyph.
        /// It provides the following info:
        /// <list type="bullet">
        /// <item>
        /// Info about the selected glyph.
        /// </item>
        /// <item>
        /// Info about the fallback font instance that retrieved this glyph (<see langword="null"/> if the glyph is from this original font).
        /// </item>
        /// <item>
        /// Position to render glyph at.
        /// </item>
        /// </list>
        /// </param>
        private void ProcessText(
            ReadOnlySpan<char> text,
            in FontRenderingOptions options,
            int lineHeight,
            ref BoundsInfo bounds,
            Action<FontGlyph, IFont?, Vector2> glyphCallback)
        {
            foreach (int codepoint in text.EnumerateCodepoints())
            {
                if (HandleControlCode(codepoint, options, lineHeight, ref bounds))
                    continue;

                FontGlyph glyph;
                IFont? fallbackFont = null;
                if (SupportsCharacter(codepoint))
                {
                    glyph = GetGlyph(codepoint);
                }
                else
                {
                    fallbackFont = FontResolver?.GetFallbackFont(GetStyledGlyph(codepoint));
                    if (fallbackFont == null)
                        continue;
                    glyph = fallbackFont.GetGlyph(codepoint);
                }

                if (glyph == FontGlyph.None)
                    continue;

                // Add spacing and kerning
                AddSpacing(glyph, options.CharacterSpacing, ref bounds);

                // Calculate glyph position relative to baseline
                Vector2 glyphPos = bounds.Location + glyph.Bearing;
                glyphCallback(glyph, fallbackFont, glyphPos);

                // Move to next glyph position
                bounds.Location.X += glyph.Advance;
                bounds.Previous = glyph;
            }
        }

        private struct BoundsInfo
        {
            public Vector2 LastLocation;
            public float LineStartX;
            public Vector2 Location;
            public FontGlyph Previous;

            public BoundsInfo(Vector2 position, float lineStartX)
            {
                LastLocation = Location = position;
                Previous = FontGlyph.None;
                LineStartX = lineStartX;
            }
        }
    }
}