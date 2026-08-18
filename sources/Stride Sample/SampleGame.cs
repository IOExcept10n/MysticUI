// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using CommunityToolkit.Mvvm.Input;
using Icy.Input.Devices;
using Icy.Rendering.Brushes;
using Icy.SharedSamples;
using Icy.Stride.Configuration;
using Icy.UI;
using Stride.Engine;
using Stride.Rendering.Compositing;
using Color4 = Stride.Core.Mathematics.Color4;

namespace Icy.StrideSample
{
    /// <summary>
    /// A minimal Stride game demonstrating IcyUI wired up purely in code (no Game Studio project/asset pipeline) -
    /// the Stride counterpart to <c>MonoGame Sample</c>'s <c>SampleGame</c>. Hosts <see cref="ControlsDemo"/> and
    /// <see cref="StylesDemo"/>, the same demos <c>MonoGame Sample</c>'s <c>ControlsSample</c>/<c>StylesSample</c>
    /// run - PageUp/PageDown switches between them (Stride has no multi-sample runner like MonoGame's
    /// <c>SamplesRunner</c>, so this just toggles which of the two demo roots is visible).
    /// </summary>
    /// <remarks>
    /// This is the newest, least-verified part of the IcyUI.Stride port: it was written and compiled against the
    /// real Stride 4.2.0.2122 packages, but never run against a live GPU/editor (no Stride tooling is available in
    /// the environment this was written in). The <see cref="GraphicsCompositor"/> wiring in particular - built
    /// entirely by hand here rather than via a Game Studio-generated asset - is the piece most likely to need
    /// adjustment; please treat this file as a starting point to validate and iterate on, not a finished sample.
    /// </remarks>
    internal sealed class SampleGame : Game
    {
        private Canvas? canvas;
        private UIElement? controlsRoot;
        private UIElement? stylesRoot;
        private int selectedDemo;

        protected override void BeginRun()
        {
            base.BeginRun();

            // Minimal in-code GraphicsCompositor: clear the back buffer, then hand the "Game" render slot to
            // UseIcyUI(), which wraps it so the UI overlay draws on top of whatever was there before (nothing,
            // here - this sample has no 3D content).
            var compositor = new GraphicsCompositor
            {
                Game = new SceneRendererCollection
                {
                    new ClearRenderer { Color = new Color4(0.1f, 0.1f, 0.12f, 1f) },
                },
            };
            SceneSystem.GraphicsCompositor = compositor;
            SceneSystem.SceneInstance = new SceneInstance(Services, new Scene());

            IcyUISceneRenderer overlay = this.UseIcyUI();
            var configuration = this.GetIcyConfiguration();
            canvas = new Canvas(configuration)
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 25, 25, 30)),
                IsInputEnabled = true,
            };
            overlay.Canvases.Add(canvas);

            configuration.Fonts.ImportFont(configuration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            controlsRoot = ControlsDemo.Build(configuration, "Airfool");
            stylesRoot = StylesDemo.Build(configuration, "Airfool");
            canvas.Add(controlsRoot);
            canvas.Add(stylesRoot);
            UpdateSelectedDemo();

            var switchDemo = new RelayCommand(() =>
            {
                selectedDemo = (selectedDemo + 1) % 2;
                UpdateSelectedDemo();
            });
            configuration.Input.Events.RegisterCommand(switchDemo, new KeyGesture(Keys.PageDown));
            configuration.Input.Events.RegisterCommand(switchDemo, new KeyGesture(Keys.PageUp));
        }

        private void UpdateSelectedDemo()
        {
            controlsRoot!.IsVisible = selectedDemo == 0;
            stylesRoot!.IsVisible = selectedDemo == 1;
        }
    }
}
