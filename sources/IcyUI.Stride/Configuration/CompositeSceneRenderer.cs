// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Stride.Core;
using Stride.Rendering;
using Stride.Rendering.Compositing;

namespace Icy.Stride.Configuration
{
    /// <summary>
    /// Draws an existing <see cref="ISceneRenderer"/> followed by <see cref="IcyUISceneRenderer"/>, so the UI
    /// overlay layers on top of whatever the game was already rendering.
    /// </summary>
    /// <remarks>
    /// <see cref="GraphicsCompositor.Game"/> is a single <see cref="ISceneRenderer"/> slot - inserting
    /// <see cref="IcyUISceneRenderer"/> as an additional render stage means replacing that slot with this small
    /// composite that runs the original renderer first, then the UI overlay on top, rather than discarding it.
    /// </remarks>
    /// <param name="inner">The renderer that was previously in <see cref="GraphicsCompositor.Game"/>, or <see langword="null"/> if there wasn't one.</param>
    /// <param name="overlay">The IcyUI render stage to draw after <paramref name="inner"/>.</param>
    internal sealed class CompositeSceneRenderer(ISceneRenderer? inner, IcyUISceneRenderer overlay) : SceneRendererBase
    {
        /// <summary>
        /// Gets the IcyUI render stage this composite draws on top of the inner renderer.
        /// </summary>
        [DataMemberIgnore]
        public IcyUISceneRenderer Overlay { get; } = overlay;

        /// <inheritdoc/>
        protected override void CollectCore(RenderContext context)
        {
            inner?.Collect(context);
            base.CollectCore(context);
        }

        /// <inheritdoc/>
        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        {
            inner?.Draw(drawContext);
            Overlay.Draw(drawContext);
        }
    }
}
