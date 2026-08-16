// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.UI;
using Stride.Core;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using IcyRenderContext = Icy.Stride.Rendering.RenderContext;

namespace Icy.Stride.Configuration
{
    /// <summary>
    /// A <see cref="SceneRendererBase"/> that draws every <see cref="Canvas"/> in <see cref="Canvases"/> as a
    /// screen-space overlay, wired into Stride's <see cref="GraphicsCompositor"/> pipeline.
    /// </summary>
    /// <remarks>
    /// Unlike MonoGame (a simple <c>Game.Draw()</c> override is enough there), Stride's <c>SpriteBatch.Begin</c>
    /// needs a frame-scoped <see cref="Stride.Graphics.GraphicsContext"/>, which is only available from within the
    /// compositor's own draw callback - so drawing lives here as a dedicated render stage, added into the
    /// compositor's renderer graph by <see cref="StrideBuildingExtensions.UseIcyUI(Stride.Engine.Game)"/>, rather
    /// than piggybacking on a <see cref="Stride.Games.GameSystemBase"/> the way <see cref="IcyUIGameSystem"/> (input
    /// pumping) does.
    /// </remarks>
    public class IcyUISceneRenderer : SceneRendererBase
    {
        /// <summary>
        /// Gets the canvases drawn by this render stage, in order, every frame.
        /// </summary>
        /// <remarks>
        /// Not part of Stride's scene-asset serialization system (there is no Stride serializer for
        /// <see cref="Icy.UI.Canvas"/>, and there shouldn't need to be one) - populated by app code at runtime.
        /// </remarks>
        [DataMemberIgnore]
        public IList<Canvas> Canvases { get; } = [];

        /// <summary>
        /// Gets or sets the configuration whose <see cref="IcyConfiguration.RenderContext"/> this render stage
        /// refreshes with the current frame's <see cref="Stride.Graphics.GraphicsContext"/> before drawing.
        /// </summary>
        [DataMemberIgnore]
        public IcyConfiguration? Configuration { get; set; }

        /// <inheritdoc/>
        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        {
            if (Configuration is null)
                return;

            if (Configuration.RenderContext is IcyRenderContext renderContext)
                renderContext.GraphicsContext = drawContext.GraphicsContext;

            foreach (Canvas canvas in Canvases)
                canvas.Render();
        }
    }
}
