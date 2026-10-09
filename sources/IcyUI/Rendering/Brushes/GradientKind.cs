// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
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
}
