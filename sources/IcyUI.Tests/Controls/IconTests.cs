using System.Drawing;
using System.Numerics;
using Icy.Rendering.Brushes;
using Icy.Tests.Rendering;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class IconTests
    {
        [Fact]
        public void MeasureContent_ReturnsFixedDefaultSize()
        {
            var icon = new Icon();

            Size measured = icon.Measure();

            Assert.Equal(new Size(16, 16), measured);
        }

        [Fact]
        public void OnRender_DrawsWithoutThrowing()
        {
            var icon = new Icon { Kind = IconKind.Checkmark, Stroke = new SolidColorBrush(Color.White) };
            icon.Arrange(new Rectangle(0, 0, 16, 16));
            var context = new FakeRenderContext();

            icon.Draw(context);

            Assert.NotEmpty(context.DrawCalls);
        }

        [Theory]
        [InlineData(IconKind.Checkmark)]
        [InlineData(IconKind.ChevronUp)]
        [InlineData(IconKind.ChevronDown)]
        [InlineData(IconKind.ChevronLeft)]
        [InlineData(IconKind.ChevronRight)]
        public void OnRender_EveryKind_DrawsWithoutThrowing(IconKind kind)
        {
            var icon = new Icon { Kind = kind };
            icon.Arrange(new Rectangle(0, 0, 16, 16));
            var context = new FakeRenderContext();

            icon.Draw(context);

            Assert.NotEmpty(context.DrawCalls);
        }

        [Fact]
        public void OnRender_AwayFromTheOrigin_DrawsInsideItsOwnBounds()
        {
            // Draw() already moves the context to the icon's position, so the glyph must be drawn in local space.
            var icon = new Icon { Kind = IconKind.ChevronRight };
            icon.Arrange(new Rectangle(100, 50, 16, 16));
            var context = new FakeRenderContext { Transform = Icy.Rendering.Transform2D.Identity };

            icon.Draw(context);

            Assert.NotEmpty(context.DrawCalls);
            foreach (var call in context.DrawCalls)
            {
                Rectangle screen = call.TransformAtDrawTime.Apply(call.Options.Destination);
                Assert.InRange(screen.X, 100, 116);
                Assert.InRange(screen.Y, 50, 66);
            }
        }
    
        [Theory]
        [InlineData(IconKind.Checkmark)]
        [InlineData(IconKind.ChevronRight)]
        public void OnRender_DrawsAnOpenStroke_NotAClosedTriangle(IconKind kind)
        {
            var icon = new Icon { Kind = kind };
            icon.Arrange(new Rectangle(0, 0, 16, 16));
            var context = new FakeRenderContext { Transform = Icy.Rendering.Transform2D.Identity };

            icon.Draw(context);

            // Three points, two segments: closing the shape would add a third.
            Assert.Equal(2, context.DrawCalls.Count);
        }

        [Fact]
        public void DrawPolyline_CentersEachSegmentAndCapsBothEnds()
        {
            var context = new FakeRenderContext { Transform = Icy.Rendering.Transform2D.Identity };

            Icy.Rendering.ShapesExtensions.DrawPolyline(context, new System.Numerics.Vector2(5, 0), [new(0, 10), new(10, 10)], Color.White, 2);

            var call = Assert.Single(context.DrawCalls);
            Assert.Equal(new Rectangle(4, 9, 12, 2), call.Options.Destination);
            Assert.Equal(0f, call.Options.Rotation);
        }

        [Theory]
        [InlineData(5.6f, 3.2f, 10.4f, 8f, 5.6f, 12.8f)]   // ChevronRight at 16x16
        [InlineData(3.2f, 10.4f, 8f, 5.6f, 12.8f, 10.4f)]  // ChevronUp at 16x16
        [InlineData(3.2f, 8f, 6.72f, 11.52f, 13.6f, 4f)]   // Checkmark at 16x16
        [InlineData(7.3f, 2.1f, 12.45f, 9.9f, 4.05f, 14.7f)]
        public void DrawPolyline_EverySegmentPassesThroughItsPointsWithFullCaps(float x1, float y1, float x2, float y2, float x3, float y3)
        {
            // Integer destinations used to round each segment's corner on its own, so two diagonal segments missed their
            // shared joint by a fraction of a pixel and left a visible seam there.
            Vector2[] points = [new(x1, y1), new(x2, y2), new(x3, y3)];
            var context = new FakeRenderContext { Transform = Icy.Rendering.Transform2D.Identity };

            Icy.Rendering.ShapesExtensions.DrawPolyline(context, Vector2.Zero, points, Color.White, 2);

            Assert.Equal(2, context.DrawCalls.Count);
            for (int i = 0; i < 2; i++)
            {
                var (start, end) = Centerline(context.DrawCalls[i].Texture, context.DrawCalls[i].Options);
                Vector2 direction = Vector2.Normalize(end - start);
                float length = Vector2.Distance(start, end);
                foreach (Vector2 point in (Vector2[])[points[i], points[i + 1]])
                {
                    Assert.True(DistanceToLine(point, start, end) < 0.01f, $"Segment {i} misses {point} by {DistanceToLine(point, start, end)}.");

                    // A square cap: the stroke reaches at least half the thickness past each point.
                    float along = Vector2.Dot(point - start, direction);
                    Assert.InRange(along, 1f - 0.01f, length - 1f + 0.01f);
                }
            }
        }

        // The centerline of a drawn quad, in SpriteBatch semantics: the origin is in texels and the quad rotates around it.
        private static (Vector2 Start, Vector2 End) Centerline(Icy.Rendering.ITexture texture, Icy.Rendering.TextureRenderingOptions options)
        {
            Rectangle destination = options.Destination;
            Vector2 scale = new((float)destination.Width / texture.Size.Width, (float)destination.Height / texture.Size.Height);
            Matrix3x2 rotation = Matrix3x2.CreateRotation(options.Rotation);
            Vector2 position = new(destination.X, destination.Y);
            Vector2 ToWorld(Vector2 local) => position + Vector2.Transform(local - (options.Origin * scale), rotation);
            float middle = destination.Height / 2f;
            return (ToWorld(new(0, middle)), ToWorld(new(destination.Width, middle)));
        }

        private static float DistanceToLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 direction = Vector2.Normalize(lineEnd - lineStart);
            Vector2 offset = point - lineStart;
            return MathF.Abs((offset.X * direction.Y) - (offset.Y * direction.X));
        }
    }
}
