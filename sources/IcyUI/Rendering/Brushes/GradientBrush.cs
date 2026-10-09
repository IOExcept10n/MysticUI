// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Identifies which gradient shape a <see cref="GradientBrush"/> paints.
    /// </summary>
    public enum GradientKind
    {
        /// <summary>
        /// A straight-line gradient at <see cref="GradientBrush.Angle"/>.
        /// </summary>
        Linear,

        /// <summary>
        /// A gradient radiating outward from <see cref="GradientBrush.Center"/>.
        /// </summary>
        Radial,
    }

    /// <summary>
    /// One color stop within a <see cref="GradientBrush"/>.
    /// </summary>
    /// <param name="Offset">Position along the gradient, from <c>0</c> to <c>1</c>.</param>
    /// <param name="Color">The color at this stop.</param>
    public readonly record struct GradientStop(float Offset, Color Color);

    /// <summary>
    /// Paints a multi-stop linear or radial color gradient.
    /// </summary>
    /// <remarks>
    /// Renders via a CPU-built pixel buffer uploaded through <see cref="IRenderContext.CreateTexture{TColor}(int, int, TColor[])"/>
    /// - the same texture-upload mechanism used elsewhere in this library (see e.g. font atlas rendering) - rebuilt
    /// only when the draw rect's size, <see cref="Kind"/>, <see cref="Angle"/>/<see cref="Center"/>, or
    /// <see cref="GradientStops"/> actually change.
    /// Also opportunistically probes <see cref="IRenderContext.GetBuiltInEffect(EffectCode)"/> for
    /// <see cref="EffectCode.GradientEffect"/> once per instance (never more than once, even though every current
    /// engine's <c>RenderContext</c> unconditionally throws <see cref="NotImplementedException"/> for it today) -
    /// if a future engine implementation ever returns a working effect instead, this brush picks it up
    /// automatically with no API change on either side. Actually implementing that effect is out of scope here.
    /// </remarks>
    public class GradientBrush : IBrush
    {
        private readonly List<GradientStop> gradientStops = [];

        private Color[]? cachedBuffer;
        private int cachedWidth;
        private int cachedHeight;
        private GradientKind cachedKind;
        private float cachedAngle;
        private Vector2 cachedCenter;
        private int cachedStopsHash;
        private ITexture? cachedTexture;

        private bool effectProbed;
        private IEffect? gradientEffect;

        /// <summary>
        /// Gets or sets which gradient shape this brush paints.
        /// </summary>
        public GradientKind Kind { get; set; } = GradientKind.Linear;

        /// <summary>
        /// Gets the color stops this gradient interpolates between, in any order - sorted internally by
        /// <see cref="GradientStop.Offset"/> when the buffer is built.
        /// </summary>
        public IList<GradientStop> GradientStops => gradientStops;

        /// <summary>
        /// Gets or sets the direction, in degrees, a <see cref="GradientKind.Linear"/> gradient runs across the
        /// draw rect. Ignored for <see cref="GradientKind.Radial"/>.
        /// </summary>
        public float Angle { get; set; }

        /// <summary>
        /// Gets or sets the center, normalized to <c>[0,1]</c> within the draw rect, a <see cref="GradientKind.Radial"/>
        /// gradient radiates from. Ignored for <see cref="GradientKind.Linear"/>.
        /// </summary>
        public Vector2 Center { get; set; } = new(0.5f, 0.5f);

        /// <inheritdoc/>
        public void Draw(IRenderContext context, in TextureRenderingOptions options)
        {
            int width = Math.Max(1, options.Destination.Width);
            int height = Math.Max(1, options.Destination.Height);

            IEffect? effect = TryGetGradientEffect(context);
            if (effect != null)
            {
                // A future real GradientEffect implementation would set its own parameters here (stops/angle/
                // center) via IEffect.SetParameter and draw through it instead of the texture path below - not
                // implemented in this plan, since no engine provides a working effect to drive yet.
            }

            EnsureTexture(context, width, height);
            context.Draw(cachedTexture!, options);
        }

        /// <summary>
        /// Maps a pixel index to <c>[0,1]</c> across the axis, anchored to pixel centers at the edges - <c>0</c> and
        /// <c>1</c> are reached exactly at the first/last pixel, rather than the (unreachable) outer edge of the
        /// texture - so the outermost pixels reproduce their nearest gradient stop's color exactly.
        /// </summary>
        private static float NormalizedPosition(int index, int size) => size > 1 ? index / (float)(size - 1) : 0.5f;

        private static Color Interpolate(List<GradientStop> sortedStops, float t)
        {
            if (t <= sortedStops[0].Offset)
                return sortedStops[0].Color;
            if (t >= sortedStops[^1].Offset)
                return sortedStops[^1].Color;

            for (int i = 0; i < sortedStops.Count - 1; i++)
            {
                GradientStop left = sortedStops[i];
                GradientStop right = sortedStops[i + 1];
                if (t < left.Offset || t > right.Offset)
                    continue;

                float span = right.Offset - left.Offset;
                float localT = span > 0 ? (t - left.Offset) / span : 0f;
                return Color.FromArgb(
                    (int)float.Lerp(left.Color.A, right.Color.A, localT),
                    (int)float.Lerp(left.Color.R, right.Color.R, localT),
                    (int)float.Lerp(left.Color.G, right.Color.G, localT),
                    (int)float.Lerp(left.Color.B, right.Color.B, localT));
            }

            return sortedStops[^1].Color;
        }

        private IEffect? TryGetGradientEffect(IRenderContext context)
        {
            if (!effectProbed)
            {
                effectProbed = true;
                try
                {
                    gradientEffect = context.GetBuiltInEffect(EffectCode.GradientEffect);
                }
                catch (NotImplementedException)
                {
                    gradientEffect = null;
                }
            }

            return gradientEffect;
        }

        private void EnsureTexture(IRenderContext context, int width, int height)
        {
            int stopsHash = ComputeStopsHash();
            bool unchanged = cachedTexture != null
                && cachedWidth == width
                && cachedHeight == height
                && cachedKind == Kind
                && cachedAngle == Angle
                && cachedCenter == Center
                && cachedStopsHash == stopsHash;
            if (unchanged)
                return;

            if (cachedBuffer == null || cachedBuffer.Length != width * height)
                cachedBuffer = new Color[width * height];

            var sortedStops = gradientStops.OrderBy(s => s.Offset).ToList();
            BuildBuffer(cachedBuffer, width, height, sortedStops);

            var pixels = new Rgba32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (Rgba32)cachedBuffer[i];

            cachedTexture = context.CreateTexture(width, height, pixels);
            cachedWidth = width;
            cachedHeight = height;
            cachedKind = Kind;
            cachedAngle = Angle;
            cachedCenter = Center;
            cachedStopsHash = stopsHash;
        }

        private void BuildBuffer(Color[] buffer, int width, int height, List<GradientStop> sortedStops)
        {
            if (sortedStops.Count == 0)
            {
                Array.Fill(buffer, Color.Transparent);
                return;
            }

            if (sortedStops.Count == 1)
            {
                Array.Fill(buffer, sortedStops[0].Color);
                return;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t = Kind == GradientKind.Linear
                        ? ComputeLinearT(x, y, width, height)
                        : ComputeRadialT(x, y, width, height);
                    buffer[(y * width) + x] = Interpolate(sortedStops, t);
                }
            }
        }

        private float ComputeLinearT(int x, int y, int width, int height)
        {
            float radians = Angle * MathF.PI / 180f;
            var direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
            float px = NormalizedPosition(x, width) - 0.5f;
            float py = NormalizedPosition(y, height) - 0.5f;
            float t = ((px * direction.X) + (py * direction.Y)) + 0.5f;
            return float.Clamp(t, 0f, 1f);
        }

        private float ComputeRadialT(int x, int y, int width, int height)
        {
            float dx = NormalizedPosition(x, width) - Center.X;
            float dy = NormalizedPosition(y, height) - Center.Y;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            const float MaxNormalizedDistance = 0.70710678f; // sqrt(0.5^2 + 0.5^2), the unit rect's half-diagonal
            return float.Clamp(distance / MaxNormalizedDistance, 0f, 1f);
        }

        private int ComputeStopsHash()
        {
            HashCode hash = default;
            foreach (GradientStop stop in gradientStops)
                hash.Add(stop);
            return hash.ToHashCode();
        }
    }
}
