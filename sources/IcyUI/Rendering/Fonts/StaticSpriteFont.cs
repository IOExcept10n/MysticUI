// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Frozen;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a static sprite font.
    /// </summary>
    public class StaticSpriteFont : SpriteFont
    {
        private const int CharLimit = 0x10000;

        private readonly ITexture[] atlas;
        private readonly FrozenDictionary<CodepointsPair, int> kernings;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticSpriteFont"/> class.
        /// </summary>
        /// <param name="info">Info about created font.</param>
        /// <param name="atlas">Array of images for the glyph atlases.</param>
        /// <param name="kernings">Map of kernings for all the defined pairs.</param>
        /// <param name="glyphs">List of all the glyphs used in the font.</param>
        /// <param name="lineGap">Recommended spacing between two lines of text.</param>
        /// <param name="resolver">An instance of the service that helps with getting glyphs that are not presented in this font.</param>
        public StaticSpriteFont(
            FontInfo info,
            ITexture[] atlas,
            IReadOnlyDictionary<CodepointsPair, int> kernings,
            IEnumerable<FontGlyph> glyphs,
            IFallbackFontResolver? resolver = null,
            float lineGap = 0)
            : base(
                info,
                new StaticFontAtlas(atlas, glyphs.ToFrozenDictionary(x => new StyledGlyphDefinition(x.Codepoint, info.Size, info.Style))),
                resolver)
        {
            this.kernings = kernings.ToFrozenDictionary();
            this.atlas = atlas;
            Metrics = CalculateMetrics(lineGap);
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use kernings in measuring.
        /// </summary>
        public bool EnableKernings { get; set; }

        /// <inheritdoc/>
        public override bool SupportsCharacter(int codepoint) => Atlas.Glyphs.ContainsKey(GetStyledGlyph(codepoint));

        /// <inheritdoc/>
        public override FontGlyph GetGlyph(int codepoint)
        {
            if (!Glyphs.TryGetValue(codepoint, out var glyph) && !Glyphs.TryGetValue(DefaultCodepoint, out glyph))
            {
                return FontGlyph.None;
            }

            return glyph;
        }

        /// <inheritdoc/>
        protected override float GetKerning(FontGlyph current, FontGlyph previous)
        {
            if (!EnableKernings || !kernings.TryGetValue(new(previous.Codepoint, current.Codepoint), out var value))
                return 0;
            return value;
        }

        private FontMetrics CalculateMetrics(float lineGap)
        {
            float maxAscent = float.MinValue;
            float minDescent = float.MaxValue;
            float lowercaseHeight = 0;
            float capitalHeight = 0;

            foreach (var pair in Glyphs)
            {
                var glyph = pair.Value;

                // Ascent is the maximum distance from baseline to the top of any glyph
                maxAscent = MathF.Max(maxAscent, glyph.Bearing.Y);

                // Descent is the minimum distance from baseline to the bottom of any glyph
                minDescent = MathF.Min(minDescent, glyph.Bearing.Y - glyph.Size.Height);

                if (glyph.Codepoint >= CharLimit)
                    continue;

                char c = (char)glyph.Codepoint;
                if (char.IsLower(c))
                {
                    lowercaseHeight = MathF.Max(lowercaseHeight, glyph.Size.Height);
                }
                else if (char.IsUpper(c))
                {
                    capitalHeight = MathF.Max(capitalHeight, glyph.Size.Height);
                }
            }

            return new(maxAscent, minDescent, lineGap, lowercaseHeight, capitalHeight);
        }

        /// <summary>
        /// Represents a pair of glyph codepoints, used for the kerning calculation.
        /// </summary>
        /// <param name="PreviousCodepoint">Gets the previous codepoint of the pair.</param>
        /// <param name="NextCodepoint">Gets the next codepoint of the pair.</param>
        public readonly record struct CodepointsPair(int PreviousCodepoint, int NextCodepoint);
    }
}