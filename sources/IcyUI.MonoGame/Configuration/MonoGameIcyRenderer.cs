using Icy.Configuration;
using Icy.Input;
using Icy.MonoGame.Rendering;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Configuration
{
    internal class MonoGameIcyRenderer(Game game, IcyConfiguration configuration) : DrawableGameComponent(game)
    {
        public IcyConfiguration Configuration { get; } = configuration;

        public override void Initialize()
        {
            Configuration.Input.Initialize();
            base.Initialize();
        }

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
