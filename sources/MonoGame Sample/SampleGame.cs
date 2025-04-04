// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Data;
using Icy.MonoGame.Configuration;
using Icy.MonoGameSample.Samples;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpDX.Direct3D9;
using System;

namespace Icy.MonoGameSample
{
    public class SampleGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private readonly SamplesRunner samplesRunner;

        private SpriteBatch diagSb;
        private SpriteFont diagFont;
        private IcyConfiguration uiConfiguration;

        public SampleGame()
        {
            graphics = new(this);
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
            var switchFullScreen = new RelayCommand(_ => 
            {
                if (!graphics.IsFullScreen) ToFullScreen();
                else ToWindow();
                graphics.ApplyChanges();
            });
            uiConfiguration.Input.Events.RegisterCommand(upCommand, new(Input.Devices.Keys.PageUp));
            uiConfiguration.Input.Events.RegisterCommand(downCommand, new(Input.Devices.Keys.PageDown));
            uiConfiguration.Input.Events.RegisterCommand(switchFullScreen, new(Input.Devices.Keys.Enter, Input.Devices.ModifierKeys.Alt));
            ToWindow();
            graphics.ApplyChanges();
            base.Initialize();
        }

        private void ToWindow()
        {
            graphics.PreferredBackBufferHeight = 720;
            graphics.PreferredBackBufferWidth = 1280;
            graphics.IsFullScreen = false;
        }

        private void ToFullScreen()
        {
            graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            graphics.IsFullScreen = true;
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
            GraphicsDevice.Clear(Color.White);
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
