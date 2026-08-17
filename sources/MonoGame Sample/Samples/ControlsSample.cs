// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="ControlsDemo"/> - a single scrollable page exercising every v1 Tier-1 control - as its own
    /// selectable sample (PgUp/PgDown to switch to it, like the other samples).
    /// </summary>
    internal class ControlsSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public ControlsSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "Tier-1 Controls Demo")
        {
            // The Canvas is shared across every sample - only show/hit-test this demo's tree while this sample is
            // the one selected in the runner (VisibleChanged mirrors the runner's Enabled/Visible toggling).
            VisibleChanged += (_, _) =>
            {
                if (demoRoot != null)
                    demoRoot.IsVisible = Visible;
            };
        }

        protected override void LoadContent()
        {
            UIConfiguration.Fonts.ImportFont(UIConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            Canvas.IsInputEnabled = true;

            demoRoot = ControlsDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
