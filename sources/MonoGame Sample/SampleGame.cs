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
using System.Text;

namespace Icy.MonoGameSample
{
    public class SampleGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private readonly SamplesRunner samplesRunner;

        private readonly StringBuilder diagnosticsInfo = new();

        private SpriteBatch diagSb;
        private SpriteFont diagFont;
        private IcyConfiguration uiConfiguration;
        private Canvas canvas;

        private Type unsafeMemoryStats = typeof(IcyConfiguration).Assembly.GetType("Hebron.Runtime.MemoryStats");

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
                new MarkupSample(this, uiConfiguration, canvas)
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
            canvas.Render();
            diagSb.Begin();
            diagnosticsInfo.Clear();
            diagnosticsInfo.Append("Current test:").Append(samplesRunner.CurrentSample.Name).AppendLine(". Use PgUp/PgDown to switch tests.")
                           .Append("FPS: ").Append(1 / gameTime.ElapsedGameTime.TotalSeconds).AppendLine()
                           .AppendLine("Memory stats:")
                           .Append("Heap size:").Append(GC.GetGCMemoryInfo().HeapSizeBytes).Append('B').AppendLine()
                           .Append("Memory excluding fragmentation: ").Append(GC.GetTotalMemory(false)).Append('B').AppendLine()
                           .Append("Unsafe allocations: ").Append(unsafeMemoryStats.GetProperty("Allocations").GetValue(null)).Append('.').AppendLine();
            diagSb.DrawString(diagFont, diagnosticsInfo.ToString(), new(600, 1), Color.DarkGreen);
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
