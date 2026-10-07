// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.Input;
using Icy.Configuration;
using Icy.MonoGame.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Icy.MonoGameSample
{
    public class SampleGame : Game
    {
        private const string FontFamily = "Airfool";

        private readonly GraphicsDeviceManager graphics;

        private IcyConfiguration uiConfiguration;
        private Canvas canvas;

        public SampleGame()
        {
            graphics = new(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            this.UseIcyUI();

            uiConfiguration = this.GetIcyConfiguration();
            uiConfiguration.UseDefaultTheme();
            canvas = new(uiConfiguration) { IsInputEnabled = true };

            var switchFullScreen = new RelayCommand(() =>
            {
                if (!graphics.IsFullScreen) ToFullScreen();
                else ToWindow();
                graphics.ApplyChanges();
            });
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

            // The canvas re-lays out when the back buffer size changes; MonoGame doesn't resize the back buffer with
            // the window by itself.
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += (_, _) => FollowWindowSize();

            ToWindow();
            graphics.ApplyChanges();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            uiConfiguration.Fonts.ImportFont(uiConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            // The shell lists every shared demo and registers PageUp/PageDown to switch between them.
            canvas.Add(SampleShell.Build(uiConfiguration, FontFamily));
        }

        protected override void Update(GameTime gameTime)
        {
            // Not Escape: dialogs and popups close on it.
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.White);
            canvas.Render();
            base.Draw(gameTime);
        }

        private void ToggleDebugTool(string name)
        {
            if (!canvas.ActiveDebugTools.Remove(name))
                canvas.ActiveDebugTools.Add(name);
        }

        private void FollowWindowSize()
        {
            Rectangle client = Window.ClientBounds;
            if (graphics.IsFullScreen || client.Width <= 0 || client.Height <= 0)
                return;

            // ApplyChanges can raise ClientSizeChanged again; once the sizes match there's nothing left to apply.
            if (graphics.PreferredBackBufferWidth == client.Width && graphics.PreferredBackBufferHeight == client.Height)
                return;

            graphics.PreferredBackBufferWidth = client.Width;
            graphics.PreferredBackBufferHeight = client.Height;
            graphics.ApplyChanges();
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
    }
}
