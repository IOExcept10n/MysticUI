// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Data;
using Icy.MonoGame.Configuration;
using Icy.MonoGameSample.Samples;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace Icy.MonoGameSample
{
    public class SampleGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly SamplesRunner samplesRunner;

        private SpriteBatch diagSb;
        private SpriteFont diagFont;
        private IcyConfiguration uiConfiguration;

        public SampleGame()
        {
            _graphics = new(this);
            samplesRunner = new(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            this.UseIcyUI();

            uiConfiguration = this.GetIcyConfiguration();

            samplesRunner.Prepare([
                new InputLoggingSample(this, uiConfiguration),
                new FontsSample(this, uiConfiguration)
                // TODO: add samples here.
                ]);

            var upCommand = new RelayCommand(_ => samplesRunner.Selection++);
            var downCommand = new RelayCommand(_ => samplesRunner.Selection--);
            uiConfiguration.Input.Events.RegisterCommand(upCommand, new(Input.Devices.Keys.PageUp));
            uiConfiguration.Input.Events.RegisterCommand(downCommand, new(Input.Devices.Keys.PageDown));

            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.ApplyChanges();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            diagSb = new(GraphicsDevice);
            diagFont = Content.Load<SpriteFont>("Consolas");
            // TODO: use this.Content to load your game content here
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.LightSlateGray);
            diagSb.Begin();
            diagSb.DrawString(diagFont, $"Current test: {samplesRunner.CurrentSample.Name}. Use PgUp/PgDown to switch tests.", Vector2.One, Color.DarkGreen);
            diagSb.End();
            
            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            // Save anything.
            base.OnExiting(sender, args);
        }
    }
}
