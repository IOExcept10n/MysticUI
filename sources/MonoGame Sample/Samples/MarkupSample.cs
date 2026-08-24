// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="MarkupDemo"/> - the same layout section <see cref="ControlsDemo"/> builds by hand, declared
    /// in markup instead - as its own selectable sample (PgUp/PgDown to switch to it, like the other samples).
    /// </summary>
    /// <remarks>
    /// Switch between this and the Tier-1 controls demo to compare the two trees: they should look identical.
    /// </remarks>
    internal class MarkupSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public MarkupSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "Markup Demo")
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

            demoRoot = MarkupDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
