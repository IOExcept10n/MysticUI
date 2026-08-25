// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="NavigationDemo"/> - a <see cref="Icy.UI.Controls.Frame"/> switching between two
    /// <see cref="Icy.UI.Controls.Page"/>s via a button on each - as its own selectable sample (PgUp/PgDown to
    /// switch to it, like the other samples).
    /// </summary>
    internal class NavigationSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public NavigationSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "Navigation Demo")
        {
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

            demoRoot = NavigationDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
