// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering
{
    /// <summary>
    /// Represents an interface for storing the global renderer options.
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
        Rectangle Scissor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current render pass supports visual effects such as shaders.
        /// </summary>
        /// <remarks>
        /// Note that enabling effect may affect the performance because effects applying can interrupt texture batching.
        /// </remarks>
        bool EnableEffects { get; set; }
    }
}
