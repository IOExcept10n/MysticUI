using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icy.Configuration;
using Icy.Rendering.Brushes;
using Icy.MonoGame;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    internal class UISample : SampleBase
    {
        private TestElement test;

        public UISample(Game game, IcyConfiguration configuration, Canvas canvas) : base(game, configuration, canvas, "Basic UI")
        {
        }

        protected override void LoadContent()
        {
            Canvas.Add(new TestElement()
            {
                Background = new SolidColorBrush(Color.Aqua.AsSystemColor()),
                Border = new SolidColorBrush(Color.Black.AsSystemColor()),
                BorderThickness = new(2),
                Width = 80,
                Height = 40,
                Margin = new(5),
                RenderScale = new(2, 0.6f),
                HorizontalAlignment = HorizontalAlignment.Left,
            });

            test = new TestElement()
            {
                Background = new SolidColorBrush(Color.Lime.AsSystemColor()),
                Border = new SolidColorBrush(Color.Black.AsSystemColor()),
                BorderThickness = new(2),
                Width = 80,
                Height = 40,
                //RenderRotation = 300,
                //RenderScale = new(2, 3),
                //RenderOffset = new(-45, 32),
                Margin = new(0, 5)
            };

            Canvas.Add(test);

            Canvas.Add(new TestElement()
            {
                Background = new SolidColorBrush(Color.PeachPuff.AsSystemColor()),
                Border = new SolidColorBrush(Color.Black.AsSystemColor()),
                BorderThickness = new(2),
                Width = 80,
                Height = 40,
                Margin = new(5),
                RenderRotation = 45,
                HorizontalAlignment = HorizontalAlignment.Right,
            });

            RegisterCommand("Ctrl+R", () => test.RenderRotation++);
            RegisterCommand("Ctrl+Shift+R", () => test.RenderRotation--);
            RegisterCommand("Ctrl++", () => test.RenderScale += Vector2.One.AsSystemVector() * 0.1f);
            RegisterCommand("Ctrl+-", () => test.RenderScale -= Vector2.One.AsSystemVector() * 0.1f);
            RegisterCommand("Right", () => test.RenderOffset += Vector2.UnitX.AsSystemVector());
            RegisterCommand("Left", () => test.RenderOffset -= Vector2.UnitX.AsSystemVector());
            RegisterCommand("Up", () => test.RenderOffset -= Vector2.UnitY.AsSystemVector());
            RegisterCommand("Down", () => test.RenderOffset += Vector2.UnitY.AsSystemVector());
            RegisterCommand("Ctrl+Right", () => test.RenderTransformOrigin += Vector2.UnitX.AsSystemVector() * 0.1f);
            RegisterCommand("Ctrl+Left", () => test.RenderTransformOrigin -= Vector2.UnitX.AsSystemVector() * 0.1f);
            RegisterCommand("Ctrl+Up", () => test.RenderTransformOrigin -= Vector2.UnitY.AsSystemVector() * 0.1f);
            RegisterCommand("Ctrl+Down", () => test.RenderTransformOrigin += Vector2.UnitY.AsSystemVector() * 0.1f);
            base.LoadContent();
        }

        private class TestElement : UIElement
        {
        }
    }
}
