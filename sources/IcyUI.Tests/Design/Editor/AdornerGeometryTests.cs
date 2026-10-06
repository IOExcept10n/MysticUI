// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor;
using Icy.Design.Editor.Placement;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class AdornerGeometryTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="10,20,0,0">
              <Border x:Name="box" Width="100" Height="40"/>
            </StackPanel>
            """;

        [Fact]
        public void SurfaceBounds_MatchTheLayoutWithoutTransforms()
        {
            using var host = new EditorTestHost(Page);

            Assert.Equal(new RectangleF(10, 20, 100, 40), AdornerGeometry.SurfaceBounds(host.Named<Border>("box")));
        }

        [Fact]
        public void SurfaceBounds_FollowTheCanvasTransform()
        {
            using var host = new EditorTestHost(Page);
            host.Canvas.Offset = new Vector2(30, 5);
            host.Canvas.Scale = new Vector2(2, 2);
            host.Render();

            RectangleF bounds = AdornerGeometry.SurfaceBounds(host.Named<Border>("box"));

            Assert.Equal(200, bounds.Width);
            Assert.Equal(80, bounds.Height);
        }

        [Fact]
        public void ToSurface_MapsAContainersLocalRectangle()
        {
            using var host = new EditorTestHost(Page);

            RectangleF mapped = AdornerGeometry.ToSurface(host.Named<StackPanel>("root"), new RectangleF(0, 40, 100, 0));

            Assert.Equal(new RectangleF(10, 60, 100, 0), mapped);
        }

        [Fact]
        public void ScreenToSurface_DividesByTheScale()
        {
            Assert.Equal(new RectangleF(10, 20, 50, 25), AdornerGeometry.ScreenToSurface(new Rectangle(20, 40, 100, 50), 2f));
        }

        [Fact]
        public void Handles_AreEightSquaresOnTheOutline()
        {
            IReadOnlyList<(ResizeHandle Handle, RectangleF Area)> handles = AdornerGeometry.Handles(new RectangleF(0, 0, 100, 40), 7);

            Assert.Equal(8, handles.Count);
            Assert.Contains(handles, x => x.Handle == ResizeHandle.BottomRight && x.Area == new RectangleF(96.5f, 36.5f, 7, 7));
            Assert.Contains(handles, x => x.Handle == ResizeHandle.Top && x.Area == new RectangleF(46.5f, -3.5f, 7, 7));
        }

        [Theory]
        [InlineData(100, 40, ResizeHandle.BottomRight)]
        [InlineData(0, 20, ResizeHandle.Left)]
        [InlineData(50, 20, ResizeHandle.None)]
        public void HandleAt_UsesSurfaceUnits(float x, float y, ResizeHandle expected)
        {
            Assert.Equal(expected, AdornerGeometry.HandleAt(new RectangleF(0, 0, 100, 40), 7, new Vector2(x, y)));
        }
    }
}
