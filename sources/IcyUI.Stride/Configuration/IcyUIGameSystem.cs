// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Input;
using Stride.Core;
using Stride.Engine;
using Stride.Games;

namespace Icy.Stride.Configuration
{
    /// <summary>
    /// Holds the <see cref="IcyConfiguration"/> for a game and pumps input each frame.
    /// </summary>
    /// <remarks>
    /// This is the input-pumping counterpart to <see cref="IcyUISceneRenderer"/>, which does the actual drawing
    /// through the <see cref="Stride.Rendering.Compositing.GraphicsCompositor"/> - see the remarks there for why
    /// the two are split. Mirrors <c>Icy.MonoGame.Configuration.MonoGameIcyRenderer</c>'s role as configuration
    /// holder plus per-frame <see cref="IInputSystem"/> pump, minus the drawing (MonoGame's component doesn't draw
    /// anything either - see that class).
    /// </remarks>
    /// <param name="registry">The game's service registry.</param>
    /// <param name="configuration">The configuration this system holds and pumps input for.</param>
    internal class IcyUIGameSystem(IServiceRegistry registry, IcyConfiguration configuration) : GameSystemBase(registry)
    {
        /// <summary>
        /// Gets the configuration this system holds.
        /// </summary>
        [DataMemberIgnore]
        public IcyConfiguration Configuration { get; } = configuration;

        /// <inheritdoc/>
        public override void Initialize()
        {
            Configuration.Input.Initialize();
            base.Initialize();
            Enabled = true;
        }

        /// <inheritdoc/>
        public override void Update(GameTime gameTime)
        {
            if (Configuration.Input is IUpdateableInput updateable)
            {
                updateable.Update(gameTime.Elapsed);
            }

            base.Update(gameTime);
        }
    }
}
