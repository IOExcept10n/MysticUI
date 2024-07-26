// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Represents an interface for the global renderer options.
    /// </summary>
    public interface IRenderOptions
    {
        /// <summary>
        /// Gets or sets the opacity of current drawing pass.
        /// </summary>
        float Opacity { get; set; }

        /// <summary>
        /// Gets or sets the area of scissor rectangle.
        /// </summary>
        Rectangle Scissors { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current render pass supports visual effects such as shaders.
        /// </summary>
        bool EnableEffects { get; set; }
    }
}