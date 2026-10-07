// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.Input;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.SharedSamples;
using Icy.Stride.Configuration;
using Icy.UI;
using Stride.Engine;
using Stride.Rendering.Compositing;
using Color4 = Stride.Core.Mathematics.Color4;

namespace Icy.StrideSample
{
    /// <summary>
    /// A minimal Stride game hosting IcyUI, wired up in code (no Game Studio project or asset pipeline). It shows
    /// <see cref="SampleShell"/>, the same shared demos and sidebar as <c>MonoGame Sample</c>.
    /// </summary>
    internal sealed class SampleGame : Game
    {
        private Canvas? canvas;

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
            configuration.UseDefaultTheme();
            canvas = new Canvas(configuration) { IsInputEnabled = true };
            overlay.Canvases.Add(canvas);

            configuration.Fonts.ImportFont(configuration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            // The shell lists every shared demo and registers PageUp/PageDown to switch between them.
            canvas.Add(SampleShell.Build(configuration, "Airfool"));

            // Icy.Diagnostics (Phase 9 M3.5) - F1 toggles the box-model overlay, F2 the diagnostics HUD (frame
            // time/memory/focused element), both engine-agnostic.
            var toggleBoundsOverlay = new RelayCommand(() => ToggleDebugTool("Bounds"));
            var toggleDiagnosticsHud = new RelayCommand(() =>
            {
                ToggleDebugTool("Focus");
                ToggleDebugTool("DiagnosticsHud");
            });
            configuration.Input.Events.RegisterCommand(toggleBoundsOverlay, new KeyGesture(Keys.F1));
            configuration.Input.Events.RegisterCommand(toggleDiagnosticsHud, new KeyGesture(Keys.F2));
        }

        private void ToggleDebugTool(string name)
        {
            if (!canvas!.ActiveDebugTools.Remove(name))
                canvas.ActiveDebugTools.Add(name);
        }
    }
}
