// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Rendering;
using Icy.Rendering.Brushes;

namespace Icy.Tests.Rendering.Brushes
{
    public class GradientBrushTests
    {
        [Fact]
        public void Linear_Angle0_LeftEdgeIsFirstStop_RightEdgeIsLastStop()
        {
            var brush = new GradientBrush { Kind = GradientKind.Linear, Angle = 0f };
            brush.GradientStops.Add(new GradientStop(0f, Color.Red));
            brush.GradientStops.Add(new GradientStop(1f, Color.Blue));
            var context = new RecordingFakeRenderContext();

            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 10, 1)));

            Rgba32[] pixels = context.LastTextureData!;
            Assert.Equal((byte)255, pixels[0].R); // leftmost pixel closer to Red
            Assert.Equal((byte)255, pixels[^1].B); // rightmost pixel closer to Blue
        }

        [Fact]
        public void Radial_CenterIsFirstStop_EdgeIsLastStop()
        {
            var brush = new GradientBrush { Kind = GradientKind.Radial, Center = new Vector2(0.5f, 0.5f) };
            brush.GradientStops.Add(new GradientStop(0f, Color.White));
            brush.GradientStops.Add(new GradientStop(1f, Color.Black));
            var context = new RecordingFakeRenderContext();

            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 11, 11)));

            Rgba32[] pixels = context.LastTextureData!;
            Assert.True(pixels[(5 * 11) + 5].R > pixels[0].R); // center pixel brighter than a corner pixel
        }

        [Fact]
        public void SameParameters_ReusesTheCachedTexture()
        {
            var brush = new GradientBrush { Kind = GradientKind.Linear };
            brush.GradientStops.Add(new GradientStop(0f, Color.Red));
            brush.GradientStops.Add(new GradientStop(1f, Color.Blue));
            var context = new RecordingFakeRenderContext();

            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 10, 10)));
            ITexture first = context.LastCreatedTexture!;
            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 10, 10)));

            Assert.Same(first, context.LastCreatedTexture);
            Assert.Equal(1, context.CreateTextureCallCount);
        }

        [Fact]
        public void ChangingAStop_RebuildsTheTexture()
        {
            var brush = new GradientBrush { Kind = GradientKind.Linear };
            brush.GradientStops.Add(new GradientStop(0f, Color.Red));
            brush.GradientStops.Add(new GradientStop(1f, Color.Blue));
            var context = new RecordingFakeRenderContext();
            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 10, 10)));

            brush.GradientStops[1] = new GradientStop(1f, Color.Green);
            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 10, 10)));

            Assert.Equal(2, context.CreateTextureCallCount);
        }

        [Fact]
        public void GetBuiltInEffect_ThatThrows_IsProbedOnlyOnce()
        {
            var brush = new GradientBrush { Kind = GradientKind.Linear };
            brush.GradientStops.Add(new GradientStop(0f, Color.Red));
            brush.GradientStops.Add(new GradientStop(1f, Color.Blue));
            var context = new ThrowingGetBuiltInEffectRenderContext();

            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 10, 10)));
            brush.Draw(context, new TextureRenderingOptions(new Rectangle(0, 0, 10, 10)));

            Assert.Equal(1, context.GetBuiltInEffectCallCount);
        }
    }
}
