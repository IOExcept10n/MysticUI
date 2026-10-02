// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Display
{
    /// <summary>
    /// An <see cref="IDisplayScaleProvider"/> that always reports the same scale.
    /// </summary>
    /// <remarks>
    /// Used as the fallback on platforms without a dedicated provider (see <see cref="DisplayScales.GetProvider"/>), and
    /// useful for tests or for forcing a scale.
    /// </remarks>
    public sealed class FixedDisplayScaleProvider : IDisplayScaleProvider
    {
        private readonly float scale;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedDisplayScaleProvider"/> class.
        /// </summary>
        /// <param name="scale">The scale to report. Defaults to <c>1.0</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="scale"/> is not a finite number greater than zero.</exception>
        public FixedDisplayScaleProvider(float scale = 1f)
        {
            if (!float.IsFinite(scale) || scale <= 0)
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "The display scale must be a finite number greater than zero.");
            this.scale = scale;
        }

        /// <inheritdoc/>
        public float GetScale(in NativeWindowInfo window) => scale;
    }
}
