// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.Input;
using Icy.Configuration;
using Icy.MonoGame.Configuration;
using Icy.MonoGameSample.Samples;
using Icy.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace Icy.MonoGameSample
{
    public class SampleGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private readonly SamplesRunner samplesRunner;

        private IcyConfiguration uiConfiguration;
        private Canvas canvas;

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
            canvas = new(uiConfiguration);

            samplesRunner.Prepare([
                new InputLoggingSample(this, uiConfiguration, canvas),
                new FontsSample(this, uiConfiguration, canvas),
                new UISample(this, uiConfiguration, canvas),
                new ControlsSample(this, uiConfiguration, canvas),
                new StylesSample(this, uiConfiguration, canvas),
                new MarkupSample(this, uiConfiguration, canvas),
                new NavigationSample(this, uiConfiguration, canvas)
                ]);

            var upCommand = new RelayCommand(() => samplesRunner.Selection++);
            var downCommand = new RelayCommand(() => samplesRunner.Selection--);
            var switchFullScreen = new RelayCommand(() => 
            {
                if (!graphics.IsFullScreen) ToFullScreen();
                else ToWindow();
                graphics.ApplyChanges();
            });
            uiConfiguration.Input.Events.RegisterCommand(upCommand, new(Input.Devices.Keys.Tab, Input.Devices.ModifierKeys.Shift));
            uiConfiguration.Input.Events.RegisterCommand(upCommand, new(Input.Devices.Keys.PageUp));
            uiConfiguration.Input.Events.RegisterCommand(downCommand, new(Input.Devices.Keys.PageDown));
            uiConfiguration.Input.Events.RegisterCommand(switchFullScreen, new(Input.Devices.Keys.Enter, Input.Devices.ModifierKeys.Alt));

            // Icy.Diagnostics (Phase 9 M3.5) - F1 toggles the box-model overlay, F2 the diagnostics HUD (frame
            // time/memory/focused element), both engine-agnostic.
            var toggleBoundsOverlay = new RelayCommand(() => ToggleDebugTool("Bounds"));
            var toggleDiagnosticsHud = new RelayCommand(() =>
            {
                ToggleDebugTool("Focus");
                ToggleDebugTool("DiagnosticsHud");
            });
            uiConfiguration.Input.Events.RegisterCommand(toggleBoundsOverlay, new(Input.Devices.Keys.F1));
            uiConfiguration.Input.Events.RegisterCommand(toggleDiagnosticsHud, new(Input.Devices.Keys.F2));

            ToWindow();
            graphics.ApplyChanges();
            base.Initialize();
        }

        private void ToggleDebugTool(string name)
        {
            if (!canvas.ActiveDebugTools.Remove(name))
                canvas.ActiveDebugTools.Add(name);
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
            canvas.Render();
            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            // Save anything.
            base.OnExiting(sender, args);
        }
    }
}
