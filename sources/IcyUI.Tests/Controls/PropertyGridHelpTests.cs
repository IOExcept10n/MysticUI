using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class PropertyGridHelpTests
    {
        private sealed class Target
        {
            [Description("Seconds before the bomb goes off.")]
            [Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]
            public float Fuse { get; set; } = 3;

            public float Speed { get; set; } = 1;
        }

        private static (Canvas Canvas, PropertyGrid Grid) Create()
        {
            var canvas = new Canvas(new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()))
            {
                IsInputEnabled = true,
                IsVisible = true,
            };
            var grid = new PropertyGrid { Target = new Target(), Width = 400, Height = 300 };
            canvas.Add(grid);
            canvas.Render();
            return (canvas, grid);
        }

        [Fact]
        public void FocusingARow_MakesItActive_AndShowsItsDescription()
        {
            (Canvas canvas, PropertyGrid grid) = Create();
            int raised = 0;
            grid.ActiveMessageChanged += (_, _) => raised++;

            canvas.Focus(FindBox(grid, "Fuse"));

            Assert.Equal("Fuse", grid.ActiveEntry?.Name);
            Assert.Equal("Seconds before the bomb goes off.", grid.ActiveMessage);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void InvalidInput_ReplacesTheDescription_WithTheReason_UntilItIsFixed()
        {
            (Canvas canvas, PropertyGrid grid) = Create();
            TextBox box = FindBox(grid, "Fuse");
            canvas.Focus(box);

            box.Text = "0";
            Assert.Equal("Must be greater than 0.", grid.ActiveMessage);

            box.Text = "5";
            Assert.Equal("Seconds before the bomb goes off.", grid.ActiveMessage);
        }

        [Fact]
        public void MovingFocusToAnotherRow_FollowsIt_AndLeavingTheGridClearsIt()
        {
            (Canvas canvas, PropertyGrid grid) = Create();
            canvas.Focus(FindBox(grid, "Fuse"));

            canvas.Focus(FindBox(grid, "Speed"));
            Assert.Equal("Speed", grid.ActiveEntry?.Name);
            Assert.Null(grid.ActiveMessage);

            canvas.Focus(null);
            Assert.Null(grid.ActiveEntry);
        }

        private static TextBox FindBox(PropertyGrid grid, string name)
        {
            var containers = (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetProperty("RealizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(grid)!;
            foreach (ItemContainer container in containers.Values)
            {
                if (container.Content is Grid row && row.Children[0] is TextBlock label && label.Text == name)
                    return row.Children[1].EnumerateVisualSubtree().OfType<TextBox>().First();
            }

            throw new InvalidOperationException($"No row '{name}'.");
        }
    }
}
