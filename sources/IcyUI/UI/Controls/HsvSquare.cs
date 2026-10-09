// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Input.Gestures;
using Icy.Rendering;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A draggable 2D saturation/value picker for the current <see cref="Hue"/> - horizontally white to the
    /// hue's fully-saturated color, vertically that to black, with a small marker at the current
    /// <see cref="Saturation"/>/<see cref="Value"/> position.
    /// </summary>
    /// <remarks>
    /// Internal - a private implementation detail of <see cref="ColorPicker"/>, not a public reusable control
    /// (per the design spec's explicit decision). Renders via the same CPU-built-texture technique
    /// <see cref="Rendering.Brushes.GradientBrush"/> uses, but is not itself a <see cref="Rendering.Brushes.GradientBrush"/>
    /// consumer - a 2D HSV blend isn't expressible as linear/radial color-stop interpolation. Dragging uses the
    /// same single-target <see cref="OnDragStarted(Point)"/>/<see cref="OnDragPerforming(Point)"/> mechanism
    /// <see cref="Slider"/>'s thumb uses.
    /// </remarks>
    internal class HsvSquare : UIElement
    {
        private float hue;
        private float saturation;
        private float value = 1f;
        private ITexture? cachedTexture;
        private float cachedHue = float.NaN;
        private int cachedWidth;
        private int cachedHeight;

        /// <summary>
        /// Occurs when <see cref="Saturation"/>/<see cref="Value"/> changes, including from a drag.
        /// </summary>
        public event EventHandler? SaturationValueChanged;

        /// <summary>
        /// Gets or sets the hue, in degrees (<c>0</c>-<c>360</c>), this square's background gradient reflects.
        /// </summary>
        public float Hue
        {
            get => hue;
            set => SetProperty(ref hue, value);
        }

        /// <summary>
        /// Gets or sets the current saturation, clamped to <c>[0,1]</c>.
        /// </summary>
        public float Saturation
        {
            get => saturation;
            set
            {
                if (SetProperty(ref saturation, float.Clamp(value, 0f, 1f)))
                    SaturationValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the current value (brightness), clamped to <c>[0,1]</c>.
        /// </summary>
        public float Value
        {
            get => value;
            set
            {
                if (SetProperty(ref this.value, float.Clamp(value, 0f, 1f)))
                    SaturationValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        protected internal override DragAxes GetDragAxes(in DragClaimContext context) =>
            context.Kind is PointerKind.Touch or PointerKind.MouseLeft ? DragAxes.Both : DragAxes.None;

        /// <inheritdoc/>
        protected internal override void OnDragStarted(Point screenPoint)
        {
            base.OnDragStarted(screenPoint);
            UpdateFromPoint(screenPoint);
        }

        /// <inheritdoc/>
        protected internal override void OnDragPerforming(Point screenPoint)
        {
            base.OnDragPerforming(screenPoint);
            UpdateFromPoint(screenPoint);
        }

        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            // Leaf element - no children to arrange.
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => new(150, 150);

        /// <inheritdoc/>
        /// <remarks>
        /// Draws in this element's own local space (destination/marker positions relative to its own top-left,
        /// not <see cref="UIElement.ActualBounds"/>'s absolute/cumulative position) - <see cref="UIElement.Draw"/>
        /// already applies this element's own screen-position transform before calling here (see
        /// <see cref="UIElement.GetDefaultRenderOptions"/>'s own documentation), so drawing at <see cref="UIElement.ActualBounds"/>'s
        /// own (non-zero) coordinates would double-apply this element's position - shifting the rendered square and
        /// marker away from the actual, correctly hit-tested area (invisible, but still draggable, since dragging
        /// goes through <see cref="UIElement.PointToLocal(Point)"/> instead, unaffected by this).
        /// </remarks>
        protected override void OnRender(IRenderContext context)
        {
            Rectangle bounds = ActualBounds;
            EnsureTexture(context, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
            context.Draw(cachedTexture!, GetDefaultRenderOptions());

            const int MarkerSize = 6;
            var markerCenter = new Vector2(
                Saturation * bounds.Width,
                (1f - Value) * bounds.Height);
            context.DrawCircle(markerCenter, MarkerSize / 2f, 12, Color.White, 1.5f);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Releases the cached <see cref="ITexture"/> when this element leaves the tree, rather than leaving it to
        /// be replaced (and disposed) the next time <see cref="EnsureTexture(IRenderContext, int, int)"/> happens to
        /// run - a detached <see cref="HsvSquare"/> is never rendered again, so nothing would otherwise trigger that
        /// replacement. <see cref="cachedWidth"/>/<see cref="cachedHeight"/> are reset to <c>0</c> alongside it (not
        /// just the texture reference) so a stale cache-hit can't read <c>true</c> off a <see langword="null"/>
        /// texture if this element is re-attached and re-rendered before its <see cref="Hue"/>/size change again.
        /// </remarks>
        protected override void OnDetached()
        {
            cachedTexture?.Dispose();
            cachedTexture = null;
            cachedWidth = 0;
            cachedHeight = 0;
            base.OnDetached();
        }

        private static Color BlendHsv(Color hueColor, float saturation, float value)
        {
            // White -> hueColor across saturation, then that -> black across value - the standard SV-square blend.
            float r = ((1f - saturation) * 255f) + (saturation * hueColor.R);
            float g = ((1f - saturation) * 255f) + (saturation * hueColor.G);
            float b = ((1f - saturation) * 255f) + (saturation * hueColor.B);
            return Color.FromArgb((int)(r * value), (int)(g * value), (int)(b * value));
        }

        private static Color HueToColor(float hueDegrees)
        {
            float h = hueDegrees / 60f;
            int sector = (int)h % 6;
            float fractional = h - (int)h;
            byte p = 0;
            byte q = (byte)(255 * (1 - fractional));
            byte t = (byte)(255 * fractional);
            return sector switch
            {
                0 => Color.FromArgb(255, 255, t, p),
                1 => Color.FromArgb(255, q, 255, p),
                2 => Color.FromArgb(255, p, 255, t),
                3 => Color.FromArgb(255, p, q, 255),
                4 => Color.FromArgb(255, t, p, 255),
                _ => Color.FromArgb(255, 255, p, q),
            };
        }

        private void EnsureTexture(IRenderContext context, int width, int height)
        {
            if (cachedTexture != null && cachedHue == Hue && cachedWidth == width && cachedHeight == height)
                return;

            var pixels = new Rgba32[width * height];
            Color hueColor = HueToColor(Hue);
            for (int y = 0; y < height; y++)
            {
                float rowValue = 1f - ((float)y / (height - 1 <= 0 ? 1 : height - 1));
                for (int x = 0; x < width; x++)
                {
                    float columnSaturation = (float)x / (width - 1 <= 0 ? 1 : width - 1);
                    pixels[(y * width) + x] = (Rgba32)BlendHsv(hueColor, columnSaturation, rowValue);
                }
            }

            cachedTexture?.Dispose();
            cachedTexture = context.CreateTexture(width, height, pixels);
            cachedHue = Hue;
            cachedWidth = width;
            cachedHeight = height;
        }

        private void UpdateFromPoint(Point screenPoint)
        {
            Vector2 local = PointToLocal(screenPoint);
            Rectangle bounds = ActualBounds;
            float newSaturation = bounds.Width > 0 ? float.Clamp(local.X / bounds.Width, 0f, 1f) : 0f;
            float newValue = bounds.Height > 0 ? 1f - float.Clamp(local.Y / bounds.Height, 0f, 1f) : 1f;

            SetProperty(ref saturation, newSaturation, nameof(Saturation));
            SetProperty(ref value, newValue, nameof(Value));
            SaturationValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
