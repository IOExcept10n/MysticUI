using System.Drawing;
using System.Numerics;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Rendering.Brushes
{
    public class NinePatchImageBrushTests
    {
        private static TextureRenderingOptions At(Rectangle destination) =>
            new(destination, null, Color.White, 0f, Vector2.Zero);

        [Fact]
        public void Draw_CutsTheSourceByThePadding_AndStretchesEachSliceIntoItsPatch()
        {
            var context = new FakeRenderContext();
            ITexture texture = context.CreateTexture(40, 40, new uint[40 * 40]);
            var brush = new NinePatchImageBrush(texture, new Rectangle(5, 5, 30, 30), new Thickness(10));

            brush.Draw(context, At(new Rectangle(100, 100, 60, 60)));

            Assert.Equal(
                [
                    (new Rectangle(100, 100, 10, 10), new Rectangle(5, 5, 10, 10)),
                    (new Rectangle(110, 100, 40, 10), new Rectangle(15, 5, 10, 10)),
                    (new Rectangle(150, 100, 10, 10), new Rectangle(25, 5, 10, 10)),
                    (new Rectangle(100, 110, 10, 40), new Rectangle(5, 15, 10, 10)),
                    (new Rectangle(110, 110, 40, 40), new Rectangle(15, 15, 10, 10)),
                    (new Rectangle(150, 110, 10, 40), new Rectangle(25, 15, 10, 10)),
                    (new Rectangle(100, 150, 10, 10), new Rectangle(5, 25, 10, 10)),
                    (new Rectangle(110, 150, 40, 10), new Rectangle(15, 25, 10, 10)),
                    (new Rectangle(150, 150, 10, 10), new Rectangle(25, 25, 10, 10)),
                ],
                context.DrawCalls.Select(x => (x.Options.Destination, x.Options.Source!.Value)));
        }

        [Fact]
        public void Draw_FollowsTheDestination_WhenTheElementMovesOrResizes()
        {
            var context = new FakeRenderContext();
            ITexture texture = context.CreateTexture(30, 30, new uint[30 * 30]);
            var brush = new NinePatchImageBrush(texture, new Thickness(10));

            brush.Draw(context, At(new Rectangle(100, 100, 60, 60)));
            context.DrawCalls.Clear();
            brush.Draw(context, At(new Rectangle(0, 0, 40, 30)));

            Assert.Equal(new Rectangle(0, 0, 10, 10), context.DrawCalls[0].Options.Destination);
            Assert.Equal(new Rectangle(10, 10, 20, 10), context.DrawCalls[4].Options.Destination);
            Assert.Equal(new Rectangle(30, 20, 10, 10), context.DrawCalls[8].Options.Destination);
        }
    }
}
