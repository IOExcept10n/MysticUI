// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data.Markup.Attributes;
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
    /// are left for a future control if actually needed. Renders nothing (and measures as empty) until both
    /// <see cref="Text"/> and <see cref="FontFamily"/> are set and the font resolves successfully.
    /// </remarks>
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

        private IFont? ResolveFont() =>
            FontFamily.Length == 0 ? null : Configuration?.Fonts.GetOrLoad(new FontInfo(FontFamily, FontSize, FontStyle));
    }
}
