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

            // The loop above only grows `max` for glyphs that have actual ink (Size > 0) - an inkless glyph like
            // a space contributes Size=(0,0) and is silently excluded from this tight ink bounding box, even
            // though it legitimately advanced the cursor (ProcessText's own bounds.Location.X += glyph.Advance,
            // which still ran for it). That's invisible for a space in the middle of the text, since a later
            // glyph's own ink pushes max.X past it anyway - but a *trailing* space has nothing after it to do
            // that, so it silently disappeared from the measured width entirely. Extending max.X to the cursor's
            // final resting position (bounds.Location.X, after ProcessText's ref mutation) fixes both that and
            // the equivalent case in TextBox's own caret-position measurement (MeasureString of a substring that
            // happens to end in a space).
            max.X = Math.Max(max.X, bounds.Location.X);

            // Same ink-vs-metrics gap on the vertical axis, but narrower: text that has ANY real ink anywhere
            // (min.Y != max.Y - different glyphs have different Bearing.Y/Size.Height, so real content almost
            // always produces a real spread) keeps its existing tight ink bounding box untouched - that's
            // established, tested behavior elsewhere in this class (e.g. lowercase-only text correctly measures
            // shorter than text with capitals or descenders). The gap only matters for text with NO ink
            // whatsoever - every glyph empty, so min.Y and max.Y never moved past their identical starting point
            // (e.g. a lone space, which TextBox measures in place of a truly empty Text - see its own
            // MeasureContent). Falling back to the font's own metrics-derived line height only in that genuinely
            // degenerate case is what TextBox.OnRender's own caret-height calculation already does unconditionally
            // via Metrics.Ascent - Metrics.Descent, just scoped here to when there's nothing else to measure by.
            if (min.Y == max.Y)
            {
                min.Y = localOptions.Position.Y;
                max.Y = localOptions.Position.Y + (Metrics.Ascent - Metrics.Descent);
            }

            return transform.Apply(Rectangle.FromLTRB((int)min.X, (int)min.Y, (int)max.X, (int)max.Y));
        }

        /// <inheritdoc/>
        public Rectangle CalculateBounds(string text, in FontRenderingOptions options) =>
            CalculateBounds(text.AsSpan(), options);

        /// <inheritdoc/>
        public Vector2 MeasureAdvance(ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            if (text.IsEmpty)
                return Vector2.Zero;

            var localOptions = options;
            Transform2D transform = CreateTransform(localOptions);
            Prepare(text, localOptions, out int baseline, out int lineHeight);

            BoundsInfo bounds = new(new Vector2(0, baseline), localOptions.Position.X);
            ProcessText(text, localOptions, lineHeight, ref bounds, static (_, _, _) => { });

            // Deliberately NOT the ink-based CalculateBounds: this is the raw cursor position after every
            // character (including inkless ones like a trailing space), never clamped up by a preceding glyph's
            // own ink overhanging past its advance width - see the interface remarks for why that distinction
            // matters for caret placement specifically.
            Rectangle advanceRect = Rectangle.FromLTRB(0, 0, (int)bounds.Location.X, (int)(Metrics.Ascent - Metrics.Descent));
            Rectangle transformed = transform.Apply(advanceRect);
            return new Vector2(transformed.Width, transformed.Height);
        }

        /// <inheritdoc/>
        public Vector2 MeasureAdvance(string text, in FontRenderingOptions options) =>
            MeasureAdvance(text.AsSpan(), options);

        /// <inheritdoc/>
        public void DrawString(IRenderContext context, ReadOnlySpan<char> text, in FontRenderingOptions options)
        {
            if (text.IsEmpty)
                return;

            var localOptions = options;
            Transform2D transform = CreateTransform(localOptions);
            Prepare(text, localOptions, out int baseline, out int lineHeight);
            BoundsInfo renderBounds = new(new Vector2(0, baseline), localOptions.Position.X);

            // Under a scaling render transform (e.g. a Canvas at 200 % DPI), draw glyphs rasterized at the device size
            // in device units, under a transform compensated by 1/deviceScale. Pen positions still come from this
            // (logical) font, so drawn text keeps exactly its measured width.
            float deviceScale = GetDeviceScale(context.Transform);
            bool useDevice = deviceScale != 1f && IsTranslationOnly(localOptions);
            Transform2D outerTransform = context.Transform;
            if (useDevice)
                context.Transform = Transform2D.Create(Matrix3x2.CreateScale(1f / deviceScale) * outerTransform.Matrix);

            try
            {
                ProcessText(text, localOptions, lineHeight, ref renderBounds, (glyph, font, glyphPos) =>
                {
                    if (glyph.IsEmpty)
                        return;

                    if (useDevice && font == null && TryGetDeviceGlyph(glyph.Codepoint, deviceScale, out FontGlyph deviceGlyph) && !deviceGlyph.IsEmpty)
                    {
                        Vector2 pen = transform.Apply(glyphPos - glyph.Bearing) * deviceScale;
                        Rectangle deviceBounds = new(
                            (int)MathF.Round(pen.X + deviceGlyph.Bearing.X),
                            (int)MathF.Round(pen.Y + deviceGlyph.Bearing.Y),
                            deviceGlyph.Size.Width,
                            deviceGlyph.Size.Height);
                        context.Draw(GetGlyphTexture(deviceGlyph), new TextureRenderingOptions(deviceBounds, deviceGlyph.TextureRegion, localOptions.Color, 0f, Vector2.Zero, localOptions.Depth));
                        return;
                    }

                    Rectangle glyphBounds = new((int)glyphPos.X, (int)glyphPos.Y, glyph.Size.Width, glyph.Size.Height);
                    Rectangle renderGlyphBounds = transform.Apply(glyphBounds);
                    if (useDevice)
                        renderGlyphBounds = ScaleRectangle(renderGlyphBounds, deviceScale);

                    var glyphTexture = ((font as SpriteFont) ?? this)?.GetGlyphTexture(glyph);

                    // Unfortunately, we can't support fonts that can't provide a texture for the specified glyph.
                    if (glyphTexture == null)
                        return;

                    TextureRenderingOptions renderOptions = new(
                        Destination: renderGlyphBounds,
                        Source: glyph.TextureRegion,
                        Color: localOptions.Color,
                        Rotation: localOptions.Rotation,
                        Origin: localOptions.Origin,
                        Depth: localOptions.Depth);

                    context.Draw(glyphTexture, renderOptions);
                });
            }
            finally
            {
                context.Transform = outerTransform;
            }
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
        /// Gets the uniform device scale to rasterize glyphs at for the specified render transform.
        /// </summary>
        /// <param name="transform">The render context's current transform.</param>
        /// <returns>
        /// <c>1</c> when glyphs should be drawn at their logical size (scale within 1 % of 1, non-uniform, rotated or
        /// degenerate); otherwise the scale rounded to the nearest 0.25, which keeps animated scales from filling the
        /// atlas with many slightly different sizes.
        /// </returns>
        internal static float GetDeviceScale(in Transform2D transform)
        {
            Vector2 scale = transform.Scale;
            if (MathF.Abs(transform.Rotation) > 0.0001f || scale.X <= 0 || MathF.Abs(scale.X - scale.Y) > 0.01f)
                return 1f;
            if (MathF.Abs(scale.X - 1f) < 0.01f)
                return 1f;

            float quantized = MathF.Round(scale.X * 4f, MidpointRounding.AwayFromZero) / 4f;
            return quantized < 0.25f ? 1f : quantized;
        }

        /// <summary>
        /// Gets an image for the specified glyph inside this font.
        /// </summary>
        /// <param name="glyph">Glyph to get image for.</param>
        /// <returns>An instance of the texture atlas for the specified glyph.</returns>
        protected ITexture GetGlyphTexture(FontGlyph glyph) => Atlas.GetGlyphPage(glyph);

        /// <summary>
        /// Tries to get a glyph rasterized for the specified device scale, so text drawn under a scaling transform
        /// stays crisp.
        /// </summary>
        /// <param name="codepoint">The codepoint to get the glyph for.</param>
        /// <param name="deviceScale">The device scale, as returned by <see cref="GetDeviceScale(in Transform2D)"/>.</param>
        /// <param name="glyph">The device-resolution glyph, with its size, bearing and texture region in device pixels.</param>
        /// <returns>
        /// <see langword="true"/> if the font can rasterize at arbitrary sizes and produced a glyph; otherwise <see langword="false"/>.
        /// The base implementation returns <see langword="false"/> (bitmap fonts are simply scaled).
        /// </returns>
        protected virtual bool TryGetDeviceGlyph(int codepoint, float deviceScale, out FontGlyph glyph)
        {
            glyph = FontGlyph.None;
            return false;
        }

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

        private static Rectangle ScaleRectangle(Rectangle rectangle, float scale) => Rectangle.FromLTRB(
            (int)MathF.Round(rectangle.Left * scale),
            (int)MathF.Round(rectangle.Top * scale),
            (int)MathF.Round(rectangle.Right * scale),
            (int)MathF.Round(rectangle.Bottom * scale));

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

        private bool IsTranslationOnly(in FontRenderingOptions options) =>
            options.Rotation == 0 && options.Origin == Vector2.Zero && (options.Scale ?? Vector2.One) == Vector2.One && RenderSizeMultiplier == 1f;

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