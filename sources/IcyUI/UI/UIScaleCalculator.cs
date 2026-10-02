// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.UI
{
    /// <summary>
    /// Computes a <see cref="Canvas"/>'s effective UI scale from its scaling inputs.
    /// </summary>
    internal static class UIScaleCalculator
    {
        /// <summary>
        /// Computes <c>base(mode) × userScale</c>.
        /// </summary>
        /// <param name="mode">The scale mode that picks the base factor.</param>
        /// <param name="displayScale">The OS display scale, used by <see cref="UIScaleMode.Dpi"/>.</param>
        /// <param name="viewport">The physical viewport size, used by <see cref="UIScaleMode.ReferenceResolution"/>.</param>
        /// <param name="reference">The reference resolution, used by <see cref="UIScaleMode.ReferenceResolution"/>.</param>
        /// <param name="fit">The fit policy, used by <see cref="UIScaleMode.ReferenceResolution"/>.</param>
        /// <param name="userScale">The user preference multiplied on top of the base factor.</param>
        /// <returns>The effective scale, or <c>1</c> when the inputs don't produce a finite positive value.</returns>
        public static float Compute(UIScaleMode mode, float displayScale, Size viewport, Size reference, ReferenceFit fit, float userScale)
        {
            float baseScale = mode switch
            {
                UIScaleMode.Dpi => displayScale,
                UIScaleMode.ReferenceResolution => ComputeFit(viewport, reference, fit),
                _ => 1f,
            };

            float result = baseScale * userScale;
            return float.IsFinite(result) && result > 0 ? result : 1f;
        }

        private static float ComputeFit(Size viewport, Size reference, ReferenceFit fit)
        {
            if (viewport.Width <= 0 || viewport.Height <= 0 || reference.Width <= 0 || reference.Height <= 0)
                return 1f;

            float widthRatio = (float)viewport.Width / reference.Width;
            float heightRatio = (float)viewport.Height / reference.Height;
            return fit switch
            {
                ReferenceFit.Fill => MathF.Max(widthRatio, heightRatio),
                ReferenceFit.MatchWidth => widthRatio,
                ReferenceFit.MatchHeight => heightRatio,
                _ => MathF.Min(widthRatio, heightRatio),
            };
        }
    }
}
