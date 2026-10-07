// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Identifies which small vector glyph an <see cref="Icon"/> draws.
    /// </summary>
    public enum IconKind
    {
        /// <summary>
        /// A checkmark ("✓"), e.g. for a checked <see cref="CheckBox"/>.
        /// </summary>
        Checkmark,

        /// <summary>
        /// An upward-pointing chevron ("^").
        /// </summary>
        ChevronUp,

        /// <summary>
        /// A downward-pointing chevron ("v").
        /// </summary>
        ChevronDown,

        /// <summary>
        /// A left-pointing chevron ("&lt;").
        /// </summary>
        ChevronLeft,

        /// <summary>
        /// A right-pointing chevron ("&gt;").
        /// </summary>
        ChevronRight,
    }

    /// <summary>
    /// Draws one small, stroke-only vector glyph (see <see cref="Kind"/>) scaled to fill this element's own
    /// bounds - a minimal alternative to a full path/shape markup language, covering exactly the iconography
    /// simple themed controls need (a checkbox's check, a dropdown's chevron) without a bitmap asset.
    /// </summary>
    /// <remarks>
    /// Renders via the existing <see cref="ShapesExtensions.DrawPolygon(IRenderContext, Vector2, Vector2[], Color, float)"/>
    /// primitive (the same one the debug-overlay system already uses), not a new rendering primitive.
    /// </remarks>
    public class Icon : UIElement
    {
        private const int DefaultSize = 16;

        private IconKind kind;
        private IBrush stroke = new SolidColorBrush(Color.White);
        private float strokeThickness = 2f;

        /// <summary>
        /// Gets or sets which glyph this icon draws.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(IconKind.Checkmark)]
        [RegisterReference]
        public IconKind Kind
        {
            get => kind;
            set => SetProperty(ref kind, value);
        }

        /// <summary>
        /// Gets or sets the brush the glyph is drawn with. Only a <see cref="SolidColorBrush"/> is actually
        /// honored today - anything else falls back to <see cref="Color.White"/>, since the underlying
        /// <see cref="ShapesExtensions"/> line-drawing primitives only accept a flat <see cref="Color"/>.
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush Stroke
        {
            get => stroke;
            set => SetProperty(ref stroke, value);
        }

        /// <summary>
        /// Gets or sets the line thickness the glyph is drawn with, in pixels.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(2f)]
        [RegisterReference]
        public float StrokeThickness
        {
            get => strokeThickness;
            set => SetProperty(ref strokeThickness, value);
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            // Leaf element - no children to arrange.
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => new(DefaultSize, DefaultSize);

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            Color color = Stroke is SolidColorBrush solid ? solid.Color : Color.White;
            Size size = ActualBounds.Size;
            Vector2[] points = GetGlyphPoints(kind, size.Width, size.Height);

            // Local space: Draw() has already moved the context to this element's position.
            context.DrawPolyline(Vector2.Zero, points, color, strokeThickness);
        }

        private static Vector2[] GetGlyphPoints(IconKind glyphKind, float width, float height) => glyphKind switch
        {
            IconKind.Checkmark =>
            [
                new(width * 0.20f, height * 0.50f),
                new(width * 0.42f, height * 0.72f),
                new(width * 0.85f, height * 0.25f),
            ],
            IconKind.ChevronUp =>
            [
                new(width * 0.20f, height * 0.65f),
                new(width * 0.50f, height * 0.35f),
                new(width * 0.80f, height * 0.65f),
            ],
            IconKind.ChevronDown =>
            [
                new(width * 0.20f, height * 0.35f),
                new(width * 0.50f, height * 0.65f),
                new(width * 0.80f, height * 0.35f),
            ],
            IconKind.ChevronLeft =>
            [
                new(width * 0.65f, height * 0.20f),
                new(width * 0.35f, height * 0.50f),
                new(width * 0.65f, height * 0.80f),
            ],
            IconKind.ChevronRight =>
            [
                new(width * 0.35f, height * 0.20f),
                new(width * 0.65f, height * 0.50f),
                new(width * 0.35f, height * 0.80f),
            ],
            _ => [],
        };
    }
}
