// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="MarkupStylesDemo"/> - the M4 markup styling/animation screen (implicit Style, hover
    /// VisualState transition, a Timeline resource played on click, and a merged ResourceDictionary loaded from a
    /// real bundled file) - as its own selectable sample (PgUp/PgDown to switch to it, like the other samples).
    /// </summary>
    internal class MarkupStylesSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public MarkupStylesSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "Markup Styles Demo")
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

            demoRoot = MarkupStylesDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
