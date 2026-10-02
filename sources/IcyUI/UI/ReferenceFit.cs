// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Specifies how <see cref="UIScaleMode.ReferenceResolution"/> maps a reference resolution onto a viewport
    /// whose aspect ratio may differ from it.
    /// </summary>
    public enum ReferenceFit
    {
        /// <summary>
        /// Uses the smaller of the width and height ratios, so the whole reference area is always visible.
        /// </summary>
        Fit,

        /// <summary>
        /// Uses the larger of the width and height ratios, so the reference area always covers the viewport.
        /// </summary>
        Fill,

        /// <summary>
        /// Uses the width ratio only.
        /// </summary>
        MatchWidth,

        /// <summary>
        /// Uses the height ratio only.
        /// </summary>
        MatchHeight,
    }
}
