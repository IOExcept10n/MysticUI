using System.Drawing;
using System.Text;
using Icy.Assets;
using Icy.Assets.Importers;
using Icy.Configuration;
using Icy.Markup;
using Icy.Rendering.Brushes;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Markup
{
    /// <summary>
    /// Checks that a markup document builds the same tree the equivalent hand-written C# does - the strongest
    /// regression guard available for the loader, since it pins structure, types, and property values all at once
    /// against an independently-written reference.
    /// </summary>
    public class MarkupEquivalenceTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void MarkupTree_MatchesTheHandBuiltTree()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            UIElement fromMarkup = (UIElement)loader.Load(
                """
                <Border Padding="14" Background="#FF26262C" BorderThickness="1">
                  <StackPanel Orientation="Horizontal">
                    <Border Background="LightCoral" Width="100" Height="60" Margin="0,0,8,0">
                      <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center">Stack 1</TextBlock>
                    </Border>
                    <Border Background="LightSkyBlue" Width="100" Height="60" Margin="0,0,8,0">
                      <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center">Stack 2</TextBlock>
                    </Border>
                  </StackPanel>
                </Border>
                """);

            UIElement fromCode = BuildByHand();

            AssertEquivalent(fromCode, fromMarkup);
        }

        [Fact]
        public void MarkupGrid_MatchesTheHandBuiltGrid()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var fromMarkup = (Grid)loader.Load(
                """
                <Grid>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                  </Grid.ColumnDefinitions>
                  <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                  </Grid.RowDefinitions>
                  <TextBlock Grid.Column="1">cell</TextBlock>
                </Grid>
                """);

            var fromCode = new Grid();
            fromCode.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            fromCode.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fromCode.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var cell = new TextBlock { Text = "cell" };
            Grid.SetColumn(cell, 1);
            fromCode.Children.Add(cell);

            Assert.Equal(
                fromCode.ColumnDefinitions.Select(x => x.Width),
                fromMarkup.ColumnDefinitions.Select(x => x.Width));
            Assert.Equal(
                fromCode.RowDefinitions.Select(x => x.Height),
                fromMarkup.RowDefinitions.Select(x => x.Height));
            AssertEquivalent(fromCode, fromMarkup);
        }

        [Fact]
        public void MarkupImporter_BuildsATreeFromAStream()
        {
            IcyConfiguration configuration = CreateConfiguration();
            var importer = new MarkupImporter(configuration);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<StackPanel><TextBlock>from a stream</TextBlock></StackPanel>"));

            Assert.True(importer.CanRead(MarkupImporter.MarkupMimeType));
            Assert.True(importer.CanRead(MarkupImporter.TextMarkupMimeType));
            Assert.False(importer.CanRead("image/png"));

            var panel = (StackPanel)importer.Import(stream, new FakeImportContext(configuration));

            Assert.Equal("from a stream", ((TextBlock)panel.Children[0]).Text);
        }

        [Fact]
        public void SampleDemoMarkup_LoadsAndProducesTheExpectedShape()
        {
            // Guards the document the MonoGame and Stride samples actually render - a typo there would otherwise
            // only surface when someone launches a sample.
            var loader = new MarkupLoader(CreateConfiguration());

            UIElement root = (UIElement)loader.Load(SharedSamples.MarkupDemo.Markup, nameof(SharedSamples.MarkupDemo));

            var button = root.FindRequiredControl<Button>("ok");
            Assert.Equal("Click Me", Assert.IsType<TextBlock>(button.Content).Text);

            var grid = root.FindRequiredControl<Grid>("cells");
            Assert.Equal(2, grid.ColumnDefinitions.Count);
            Assert.Equal(2, grid.RowDefinitions.Count);
            Assert.Equal(4, grid.Children.Count);
            Assert.Equal(1, Grid.GetRow(grid.Children[3]));
            Assert.Equal(1, Grid.GetColumn(grid.Children[3]));
        }

        private static UIElement BuildByHand()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(CreateSwatch(Color.LightCoral, "Stack 1"));
            row.Children.Add(CreateSwatch(Color.LightSkyBlue, "Stack 2"));

            return new Border
            {
                Child = row,
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromArgb(255, 38, 38, 44)),
                BorderThickness = new Thickness(1),
            };
        }

        private static Border CreateSwatch(Color color, string label) => new()
        {
            Background = new SolidColorBrush(color),
            Width = 100,
            Height = 60,
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 14,
                Foreground = Color.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        };

        /// <summary>
        /// Asserts that two trees have the same shape and the same values for every property markup could have set.
        /// </summary>
        private static void AssertEquivalent(UIElement expected, UIElement actual, string path = "root")
        {
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Margin, actual.Margin);
            Assert.Equal(expected.Padding, actual.Padding);
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            Assert.Equal(expected.HorizontalAlignment, actual.HorizontalAlignment);
            Assert.Equal(expected.VerticalAlignment, actual.VerticalAlignment);
            Assert.Equal(expected.Foreground, actual.Foreground);

            switch (expected)
            {
                case TextBlock expectedText:
                    var actualText = (TextBlock)actual;
                    Assert.Equal(expectedText.Text, actualText.Text);
                    Assert.Equal(expectedText.FontSize, actualText.FontSize);
                    break;

                case Border expectedBorder:
                    var actualBorder = (Border)actual;
                    AssertSameBrush(expectedBorder.Background, actualBorder.Background, path);
                    Assert.Equal(expectedBorder.BorderThickness, actualBorder.BorderThickness);
                    break;

                case StackPanel expectedStack:
                    Assert.Equal(expectedStack.Orientation, ((StackPanel)actual).Orientation);
                    break;
            }

            List<UIElement> expectedChildren = [.. Children(expected)];
            List<UIElement> actualChildren = [.. Children(actual)];
            Assert.Equal(expectedChildren.Count, actualChildren.Count);

            for (int i = 0; i < expectedChildren.Count; i++)
            {
                AssertEquivalent(expectedChildren[i], actualChildren[i], $"{path}[{i}]");
            }
        }

        private static IEnumerable<UIElement> Children(UIElement element) => element switch
        {
            Panel panel => panel.Children,
            Border border => border.Child == null ? [] : [border.Child],
            _ => [],
        };

        private static void AssertSameBrush(IBrush? expected, IBrush? actual, string path)
        {
            if (expected is SolidColorBrush expectedSolid)
            {
                var actualSolid = Assert.IsType<SolidColorBrush>(actual);
                Assert.True(expectedSolid.Color.ToArgb() == actualSolid.Color.ToArgb(), $"{path}: expected {expectedSolid.Color}, got {actualSolid.Color}");
                return;
            }

            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// The minimum <see cref="IImportContext"/> a <see cref="MarkupImporter"/> actually reads.
        /// </summary>
        private sealed class FakeImportContext(IcyConfiguration configuration) : IImportContext
        {
            public IAssetResolver AssetResolver => configuration.Assets.AssetResolver;

            public string? DataFormat => MarkupImporter.MarkupMimeType;

            public IAssetContext ImportSource => configuration.Assets.DefaultAssetContext;

            public string? ResourceName => "UI/Test.xml";
        }
    }
}
