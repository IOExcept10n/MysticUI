// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering;
using Icy.UI;

namespace Icy.Diagnostics
{
    /// <summary>
    /// A debug visualizer drawn at a single <see cref="UIElement"/>'s own screen transform - a bounding-box
    /// outline, a focus highlight, and the like.
    /// </summary>
    /// <remarks>
    /// Rendered from inside <see cref="UIElement.Draw(IRenderContext)"/>, immediately after the element's own
    /// <c>OnRender</c>, which is the one point <see cref="IRenderContext.Transform"/> is already exactly that
    /// element's own screen transform - draw in the element's local space (against
    /// <c>(0,0)-(ActualBounds.Width,ActualBounds.Height)</c>), the same convention <c>Border.OnRender</c> and
    /// <see cref="UIElement.ClipToBounds"/>'s scissor computation both use, rather than re-deriving the
    /// element's absolute screen position.
    /// </remarks>
    public interface IDebugOverlay
    {
        /// <summary>
        /// Gets the name this overlay is registered and activated under - matched against
        /// <see cref="UI.Canvas.ActiveDebugTools"/> and the <c>Debug.Visualization</c> attached property
        /// (<see cref="Debug.GetVisualization"/>/<see cref="Debug.SetVisualization"/>).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Draws this overlay over <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element being visualized.</param>
        /// <param name="context">
        /// The render context, with <see cref="IRenderContext.Transform"/> already set to
        /// <paramref name="element"/>'s own screen transform.
        /// </param>
        void Render(UIElement element, IRenderContext context);
    }
}
