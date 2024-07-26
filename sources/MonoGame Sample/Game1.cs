// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;
using AquaUI.Input;
using AquaUI.Input.Diagnostics;
using AquaUI.MonoGame.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoGame_Sample
{
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private readonly MonoGameInputSystem input;
        private SpriteFont font;
        private readonly StringBuilder deviceEventLogs = new();
        private readonly StringBuilder inputEventLogs = new();
        private readonly JsonSerializerOptions jsonOptions;

        private float commandDelay;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            input = new MonoGameInputSystem(this);
            var devListener = new DeviceEventsAggregator(input);
            devListener.OnEvent += DevListener_OnEvent;
            var eventListener = new InputEventsAggregator(input.Events);
            eventListener.OnEvent += EventListener_OnEvent;
            jsonOptions = new()
            {
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                },
                IncludeFields = true,
            };
        }

        private void EventListener_OnEvent(object sender, InputEventsAggregator.LoggerEventInfo e)
        {
            Log(inputEventLogs)
                .Append('(')
                .Append(e.InputEventListener.GetType().Name)
                .Append(')')
                .Append(' ')
                .Append('{')
                .Append(e.EventType)
                .Append('}')
                .Append(':')
                .Append(' ')
                .AppendLine(JsonSerializer.Serialize((e.Args as IDataEventArgs)?.Data, options: jsonOptions));
        }

        private void DevListener_OnEvent(object sender, DeviceEventsAggregator.LoggerEventInfo e)
        {
            Log(deviceEventLogs)
                .Append('(')
                .Append(e.InputDevice.GetType().Name)
                .Append(')')
                .Append(' ')
                .Append('{')
                .Append(e.EventType)
                .Append('}')
                .Append(':')
                .Append(' ')
                .AppendLine(JsonSerializer.Serialize((e.Args as IDataEventArgs)?.Data, options: jsonOptions));
        }

        private static StringBuilder Log(StringBuilder logger)
        {
            return logger.Append(DateTime.UtcNow.ToString("[yyyy.MM.dd hh:mm:ss:ffffff]: "));
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.ApplyChanges();
            input.Initialize();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            font = Content.Load<SpriteFont>("Segoe UI");
            // TODO: use this.Content to load your game content here
        }

        protected override void Update(GameTime gameTime)
        {
            //if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            //    Exit();

            var state = Keyboard.GetState();
            if (state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl) && commandDelay < 0)
            {
                if (state.IsKeyDown(Keys.D))
                    deviceEventLogs.Clear();
                if (state.IsKeyDown(Keys.E))
                    inputEventLogs.Clear();
                if (state.IsKeyDown(Keys.T))
                    input.Events.Text.EnableTextInput();
                if (state.IsKeyDown(Keys.H))
                    input.Events.Text.DisableTextInput();
                commandDelay = 0.5f;
            }

            // TODO: Add your update logic here
            foreach (var device in input.OfType<IUpdateableInput>())
                device.Update(gameTime.ElapsedGameTime);
            input.Events.Update(gameTime.ElapsedGameTime);
            commandDelay -= gameTime.ElapsedGameTime.Seconds;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here
            _spriteBatch.Begin();
            _spriteBatch.DrawString(font, deviceEventLogs, new Vector2(20, 20), Color.Red);
            _spriteBatch.DrawString(font, inputEventLogs, new Vector2(575, 20), Color.Blue);
            _spriteBatch.End();
            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            File.AppendAllText("devices.log", deviceEventLogs.ToString());
            File.AppendAllText("events.log", inputEventLogs.ToString());
            base.OnExiting(sender, args);
        }
    }
}