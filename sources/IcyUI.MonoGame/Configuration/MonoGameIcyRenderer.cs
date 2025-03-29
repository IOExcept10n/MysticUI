using Icy.Configuration;
using Icy.Input;
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

            base.Update(gameTime);
        }
    }
}
