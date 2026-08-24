// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data.Markup.Attributes;
using Icy.Markup;
using Icy.Rendering;
using Icy.Rendering.Fonts;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Displays a single run of text using a font resolved from <see cref="UIElement.Configuration"/>'s
    /// <see cref="Icy.Configuration.IcyConfiguration.Fonts"/> service.
    /// </summary>
    /// <remarks>
    /// A thin wrapper over <see cref="Icy.Rendering.Fonts"/> - no wrapping/multi-run styling/text alignment; those
    /// are left for a future control if actually needed. Renders nothing (and measures as empty) until
    /// <see cref="Text"/> is set and a font resolves; <see cref="FontFamily"/> is optional, since resolution falls
    /// back to <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> and then
    /// <see cref="Icy.Rendering.Fonts.FontSystem.FallbackFont"/>.
    /// </remarks>
    [ContentProperty(nameof(Text))]
    public class TextBlock : UIElement
    {
        private string fontFamily = string.Empty;
        private float fontSize = 16;
        private FontStyle fontStyle = FontStyle.Regular;
        private string text = string.Empty;

        /// <summary>
        /// Gets or sets the font family to render <see cref="Text"/> with.
        /// </summary>
        /// <remarks>
        /// Resolved via <see cref="Icy.Rendering.Fonts.FontSystem.GetOrLoad(FontInfo)"/> - the family must already
        /// be importable/loaded by the configured <see cref="Icy.Rendering.Fonts.FontSystem"/> (e.g. via
        /// <see cref="Icy.Rendering.Fonts.FontSystem.ImportFont{TContext}"/> or
        /// <see cref="Icy.Rendering.Fonts.FontSystem.EnableSystemFonts"/>) for text to actually render.
        /// </remarks>
        [Category("Appearance")]
        [DefaultValue("")]
        [RegisterReference]
        [AffectsMeasure]
        public string FontFamily
        {
            get => fontFamily;
            set
            {
                if (SetProperty(ref fontFamily, value))
                {
                    InvalidateMeasure();
                }
            }
        }

        /// <summary>
        /// Gets or sets the font size, in pixels.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(16f)]
        [RegisterReference]
        [AffectsMeasure]
        public float FontSize
        {
            get => fontSize;
            set
            {
                if (SetProperty(ref fontSize, value))
                {
                    InvalidateMeasure();
                }
            }
        }

        /// <summary>
        /// Gets or sets the font style (bold/italic/etc.) to render <see cref="Text"/> with.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(FontStyle.Regular)]
        [RegisterReference]
        [AffectsMeasure]
        public FontStyle FontStyle
        {
            get => fontStyle;
            set
            {
                if (SetProperty(ref fontStyle, value))
                {
                    InvalidateMeasure();
                }
            }
        }

        /// <summary>
        /// Gets or sets the text to display.
        /// </summary>
        [Category("Content")]
        [DefaultValue("")]
        [RegisterReference]
        [AffectsMeasure]
        public string Text
        {
            get => text;
            set
            {
                if (SetProperty(ref text, value ?? string.Empty))
                {
                    InvalidateMeasure();
                }
            }
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            // Leaf element - no children to arrange.
        }

        /// <inheritdoc/>
        protected override Size MeasureContent()
        {
            IFont? font = ResolveFont();
            if (font == null || Text.Length == 0)
                return Size.Empty;

            Vector2 measured = font.MeasureString(Text, DefaultRenderingOptions());
            return new Size((int)MathF.Ceiling(measured.X), (int)MathF.Ceiling(measured.Y));
        }

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            IFont? font = ResolveFont();
            if (font == null || Text.Length == 0)
                return;

            font.DrawString(context, Text, DefaultRenderingOptions());
        }

        private FontRenderingOptions DefaultRenderingOptions() => new(
            Position: new Vector2(Padding.Left, Padding.Top),
            Scale: null,
            Rotation: 0,
            Origin: Vector2.Zero,
            CharacterSpacing: 0,
            LineSpacing: 0,
            Color: Foreground,
            Depth: ZIndex,
            Effect: null);

        /// <summary>
        /// Resolves the font to measure and draw <see cref="Text"/> with.
        /// </summary>
        /// <returns>The resolved font, or <see langword="null"/> when nothing at all could be resolved.</returns>
        /// <remarks>
        /// Falls back in three steps: this element's own <see cref="FontFamily"/>, then
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/>, then
        /// <see cref="Icy.Rendering.Fonts.FontSystem.FallbackFont"/>. An element that never had a family assigned
        /// used to short-circuit to <see langword="null"/> here and render nothing even when the configuration had
        /// a perfectly good fallback - which markup's bare-text sugar (<c>&lt;Button&gt;Click Me&lt;/Button&gt;</c>)
        /// would have hit on every element it creates.
        /// </remarks>
        private IFont? ResolveFont()
        {
            if (Configuration?.Fonts is not { } fonts)
                return null;

            string family = FontFamily.Length > 0 ? FontFamily : fonts.DefaultFontFamily;
            return family.Length == 0 ? fonts.FallbackFont : fonts.GetOrLoad(new FontInfo(family, FontSize, FontStyle));
        }
    }
}
