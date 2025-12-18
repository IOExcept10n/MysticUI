// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using Icy.Configuration;
using Icy.Data;
using Icy.Input;
using Icy.Input.Diagnostics;
using Icy.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Icy.MonoGameSample.Samples
{
    internal class InputLoggingSample : SampleBase
    {
        private const int MaxLength = 50;

        private readonly IInputSystem input;
        private readonly StringBuilder deviceEventLogs = new();
        private readonly StringBuilder inputEventLogs = new();

        private SpriteFont displayFont;
        private SpriteBatch spriteBatch;

        public InputLoggingSample(Game game, IcyConfiguration configuration, Canvas canvas) : base(game, configuration, canvas, "Input logging")
        {
            input = configuration.Input;
            var deviceListener = new DeviceEventsAggregator(input);
            var eventListener = new InputEventsAggregator(input.Events);

            deviceListener.OnEvent += OnDeviceEvent;
            eventListener.OnEvent += OnInputEvent;
        }

        public override void Initialize()
        {
            var clearDeviceLogs = new RelayCommand(() => deviceEventLogs.Clear());
            var clearInputLogs = new RelayCommand(() => inputEventLogs.Clear());
            var enableText = new RelayCommand(input.Events.Text.EnableTextInput);
            var disableText = new RelayCommand(input.Events.Text.DisableTextInput);

            input.Events.RegisterCommand(clearDeviceLogs, new(Input.Devices.Keys.D, Input.Devices.ModifierKeys.Ctrl));
            input.Events.RegisterCommand(clearInputLogs, new(Input.Devices.Keys.E, Input.Devices.ModifierKeys.Ctrl));
            input.Events.RegisterCommand(enableText, new(Input.Devices.Keys.T, Input.Devices.ModifierKeys.Ctrl));
            input.Events.RegisterCommand(disableText, new(Input.Devices.Keys.H, Input.Devices.ModifierKeys.Ctrl));

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            displayFont = Game.Content.Load<SpriteFont>("Segoe UI");
            base.LoadContent();
        }

        public override void Draw(GameTime gameTime)
        {
            spriteBatch.Begin();
            spriteBatch.DrawString(displayFont, deviceEventLogs, new Vector2(20, 20), Color.Red);
            spriteBatch.DrawString(displayFont, inputEventLogs, new Vector2(575, 20), Color.Blue);
            spriteBatch.End();
            base.Draw(gameTime);
        }

        private void OnInputEvent(object sender, InputEventsAggregator.EventInfo e)
        {
            string line = $"[{DateTime.UtcNow:hh:mm:ss:ffffff}]: ({e.InputEventListener.GetType().Name}) {{{e.EventType}}}: {(e.Args as IDataEventArgs)?.Data}";
            if (line.Length > MaxLength)
            {
                line = line[..MaxLength] + "...";
            }

            inputEventLogs.AppendLine(line);

            // Show at most last MaxLength * 10 characters of logs.
            if (inputEventLogs.Length > MaxLength * 10)
            {
                inputEventLogs.Remove(0, MaxLength);
            }
        }

        private void OnDeviceEvent(object sender, DeviceEventsAggregator.EventInfo e)
        {
            string line = $"[{DateTime.UtcNow:hh:mm:ss:ffffff}]: ({e.InputDevice.GetType().Name}) {{{e.EventType}}}: {(e.Args as IDataEventArgs)?.Data}";
            if (line.Length > MaxLength)
            {
                line = line[..MaxLength] + "...";
            }

            deviceEventLogs.AppendLine(line);

            // Show at most last 500 characters of logs.
            if (deviceEventLogs.Length > MaxLength * 10)
            {
                deviceEventLogs.Remove(0, MaxLength);
            }
        }
    }
}