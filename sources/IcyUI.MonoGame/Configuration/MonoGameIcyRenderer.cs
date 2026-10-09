// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Input;
using Icy.MonoGame.Rendering;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Configuration
{
    /// <summary>
    /// The MonoGame game component that drives IcyUI each frame: it initializes input, feeds it the frame time, and polls
    /// the display scale.
    /// </summary>
    /// <param name="game">The game the component belongs to.</param>
    /// <param name="configuration">The IcyUI configuration it drives.</param>
    internal class MonoGameIcyRenderer(Game game, IcyConfiguration configuration) : DrawableGameComponent(game)
    {
        /// <summary>
        /// Gets the IcyUI configuration this component drives.
        /// </summary>
        public IcyConfiguration Configuration { get; } = configuration;

        /// <inheritdoc/>
        public override void Initialize()
        {
            Configuration.Input.Initialize();
            base.Initialize();
        }

        /// <inheritdoc/>
        public override void Update(GameTime gameTime)
        {
            if (Configuration.Input is IUpdateableInput updateable)
            {
                updateable.Update(gameTime.ElapsedGameTime);
            }

            (Configuration.RenderContext as RenderContext)?.PollDisplayScale();
            base.Update(gameTime);
        }
    }
}
