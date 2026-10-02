// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Specifies how a <see cref="Canvas"/> chooses the base factor of its <see cref="Canvas.EffectiveScale"/>.
    /// </summary>
    /// <remarks>
    /// The base factor is always multiplied by <see cref="Configuration.ScalingConfiguration.UserScale"/>.
    /// <see cref="Dpi"/> and <see cref="ReferenceResolution"/> are alternatives and are never combined.
    /// </remarks>
    public enum UIScaleMode
    {
        /// <summary>
        /// No base scaling: one UI unit is one physical pixel.
        /// </summary>
        None,

        /// <summary>
        /// Follows the operating system display scale reported by <see cref="Rendering.IRenderContext.DisplayScale"/>
        /// (e.g. <c>2.0</c> on a 200 % display). A host that isn't DPI-aware reports <c>1.0</c>.
        /// </summary>
        Dpi,

        /// <summary>
        /// Scales the UI so that a fixed reference resolution fits the viewport according to a <see cref="ReferenceFit"/> policy.
        /// </summary>
        ReferenceResolution,
    }
}
