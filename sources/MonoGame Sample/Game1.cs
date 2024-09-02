// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;
using AquaUI.Input;
using AquaUI.Input.Diagnostics;
using AquaUI.Rendering;
using AquaUI.MonoGame;
using AquaUI.MonoGame.Input;
using AquaUI.MonoGame.Rendering;
using AquaUI.Rendering.Brushes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MonoGame_Sample.Samples;

namespace MonoGame_Sample
{
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private int selection = 0;
        private SpriteBatch diagSb;
        private SpriteFont diagFont;
        private KeyboardState previousState;

        public int Selection
        {
            get => selection;
            set
            {
                if (value != selection)
                {
                    if (value >= Components.Count)
                    {
                        selection = 0;
                    }
                    else if (value < 0)
                    {
                        selection = Components.Count - 1;
                    }
                    OnSelectionUpdate();
                }
            }
        }

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Components.Add(new InputLoggingTest(this));
            Components.Add(new RenderContextTest(this));
            OnSelectionUpdate();
        }

        public void OnSelectionUpdate()
        {
            for (int i = 0; i < Components.Count; i++)
            {
                var component = Components[i] as DrawableGameComponent;
                component.Enabled = component.Visible = i == selection;
            }
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
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
            var currentState = Keyboard.GetState();
            if (currentState.IsKeyDown(Keys.PageDown) && previousState.IsKeyUp(Keys.PageDown))
            {
                Selection--;
            }
            if (currentState.IsKeyDown(Keys.PageUp) && previousState.IsKeyUp(Keys.PageUp))
            {
                Selection++;
            }
            //if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            //    Exit();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            diagSb.Begin();
            diagSb.DrawString(diagFont, $"Current test: {Components[selection].GetType().Name}. Use PgUp/PgDown to switch tests.", Vector2.One, Color.DarkGreen);
            diagSb.End();
            // TODO: Add your drawing code here
            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            // Save anything.
            base.OnExiting(sender, args);
        }
    }
}