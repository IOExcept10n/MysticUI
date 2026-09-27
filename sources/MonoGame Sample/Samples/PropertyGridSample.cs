// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="PropertyGridDemo"/> - every v1 <c>PropertyGrid</c> editor type, plus a button that swaps
    /// its target between two differently-shaped objects - as its own selectable sample (PgUp/PgDown to switch
    /// to it, like the other samples).
    /// </summary>
    internal class PropertyGridSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public PropertyGridSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "PropertyGrid Demo")
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

            demoRoot = PropertyGridDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
