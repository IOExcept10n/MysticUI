using System.Drawing;
using System.Text;
using Icy.Animations;
using Icy.Assets;
using Icy.Assets.Importers;
using Icy.Configuration;
using Icy.Markup;
using Icy.Rendering.Brushes;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
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

            UIElement fromMarkup = loader.Load(
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
        public void ThemedButtonScreen_MatchesTheHandBuiltStyleAndResources()
        {
            // The milestone's whole-feature regression guard (M4 design spec, §6): a single small screen exercising
            // an implicit (keyless) Style (Task 7), a hover VisualState with a Duration/Easing transition (Tasks 9 +
            // 11), and a {StaticResource}-referenced Timeline sitting unused as a resource (Tasks 5 + 12) - all at
            // once, in markup and via the exact hand-built pattern StylesDemo.cs itself uses (Style<T>/VisualState<T>
            // fluent builders).
            var loader = new MarkupLoader(CreateConfiguration());

            UIElement fromMarkup = loader.Load(
                """
                <StackPanel Orientation="Vertical">
                  <StackPanel.Resources>
                    <Style TargetType="Button" Background="#FF3C64C8">
                      <Style.StateGroups>
                        <VisualStateGroup Name="CommonStates">
                          <VisualState Name="Hovered" State="Hovered" Duration="0:0:0.2" Easing="EaseOutCubic" Background="#FF5A87E6"/>
                        </VisualStateGroup>
                      </Style.StateGroups>
                    </Style>
                    <Timeline x:Key="PulseTimeline" TargetProperty="Opacity" Duration="0:0:1">
                      <AnimationKeyframe Offset="0" Value="0.5"/>
                      <AnimationKeyframe Offset="1" Value="1.0"/>
                    </Timeline>
                  </StackPanel.Resources>
                  <Button Padding="12,0" CommandParameter="{StaticResource PulseTimeline}">
                    <TextBlock>Themed Button</TextBlock>
                  </Button>
                </StackPanel>
                """);

            UIElement fromCode = BuildThemedButtonScreenByHand();

            // Neither root has a directly-assigned Style - the implicit style only resolves once each root attaches
            // to a Canvas (see UIElement.OnAttached/ResolveImplicitStyle). Assigning .Style explicitly on the
            // hand-built side would defeat the point of this test: proving the *implicit* resolution path produces
            // the same result as an explicit one would.
            new Canvas(CreateConfiguration()).Add(fromMarkup);
            new Canvas(CreateConfiguration()).Add(fromCode);

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

            UIElement root = loader.Load(SharedSamples.MarkupDemo.Markup, nameof(SharedSamples.MarkupDemo));

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

        /// <summary>
        /// Builds the themed-button screen from <see cref="ThemedButtonScreen_MatchesTheHandBuiltStyleAndResources"/>
        /// the same way <c>StylesDemo.BuildStateDrivenSection</c> builds its own state-driven section: a fluent
        /// <see cref="Style{TTarget}"/> carrying a <see cref="VisualStateGroup"/> with a <see cref="VisualState{TTarget}"/>
        /// transition, registered as an implicit style (not assigned to the button directly) plus an unused
        /// <see cref="Timeline"/> resource referenced from the button.
        /// </summary>
        private static UIElement BuildThemedButtonScreenByHand()
        {
            var root = new StackPanel { Orientation = Orientation.Vertical };

            var hoveredState = new VisualState<Button>("Hovered", ControlState.Hovered)
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = Easing.EaseOutCubic,
            }.Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 90, 135, 230)));

            var commonStates = new VisualStateGroup("CommonStates");
            commonStates.States.Add(hoveredState);

            var buttonStyle = new Style<Button>()
                .Set(x => x.Background, new SolidColorBrush(Color.FromArgb(255, 60, 100, 200)))
                .WithStateGroup(commonStates);

            // No x:Key equivalent here - registered under the reserved implicit-style key directly, exactly as
            // ImplicitStyleTests does, so OnAttached resolves it the same way the markup-loaded StackPanel.Resources
            // entry does.
            root.Resources[ResourceDictionary.GetImplicitStyleKey(typeof(Button))] = buttonStyle;

            var pulseTimeline = new Timeline("Opacity", TimeSpan.FromSeconds(1));
            pulseTimeline.AddKeyframe(0f, "0.5");
            pulseTimeline.AddKeyframe(1f, "1.0");
            root.Resources["PulseTimeline"] = pulseTimeline;

            var button = new Button
            {
                Content = new TextBlock { Text = "Themed Button" },
                Padding = new Thickness(12, 0),
                CommandParameter = pulseTimeline,
            };
            root.Children.Add(button);

            return root;
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

            // Style is a property on every UIElement (not type-specific), so it's checked alongside the common
            // properties above rather than inside the type switch below.
            AssertSameStyle(expected.Style, actual.Style, path);

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

                case Button expectedButton:
                    var actualButton = (Button)actual;
                    AssertSameBrush(expectedButton.Background, actualButton.Background, path);
                    AssertSameTimeline(expectedButton.CommandParameter as Timeline, actualButton.CommandParameter as Timeline, path);
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
            ContentControl content => content.Content == null ? [] : [content.Content],
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
        /// Asserts that two <see cref="Style"/>s (including a whole <see cref="Style.BasedOn"/> chain, and every
        /// <see cref="Style.StateGroups"/> entry) set the same properties to the same values.
        /// </summary>
        private static void AssertSameStyle(Style? expected, Style? actual, string path)
        {
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.NotNull(actual);
            Assert.Equal(expected.TargetType, actual!.TargetType);
            AssertSameSetters(expected.Setters, actual.Setters, $"{path}.Style");

            Assert.Equal(expected.StateGroups.Count, actual.StateGroups.Count);
            for (int i = 0; i < expected.StateGroups.Count; i++)
            {
                AssertSameStateGroup(expected.StateGroups[i], actual.StateGroups[i], $"{path}.Style.StateGroups[{i}]");
            }

            AssertSameStyle(expected.BasedOn, actual.BasedOn, $"{path}.Style.BasedOn");
        }

        /// <summary>
        /// Asserts that two <see cref="VisualStateGroup"/>s have the same name and the same <see cref="VisualState"/>s,
        /// in the same order.
        /// </summary>
        private static void AssertSameStateGroup(VisualStateGroup expected, VisualStateGroup actual, string path)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.States.Count, actual.States.Count);
            for (int i = 0; i < expected.States.Count; i++)
            {
                AssertSameVisualState(expected.States[i], actual.States[i], $"{path}.States[{i}]");
            }
        }

        /// <summary>
        /// Asserts that two <see cref="VisualState"/>s declare the same name, <see cref="VisualState.State"/> flags,
        /// transition (<see cref="VisualState.Duration"/>/<see cref="VisualState.Easing"/>), and setters. Does not
        /// trigger the state or drive its animation forward - that behavior is covered by the animation system's own
        /// tests; this only pins the declared data markup and hand-built C# agree on.
        /// </summary>
        private static void AssertSameVisualState(VisualState expected, VisualState actual, string path)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.State, actual.State);
            Assert.Equal(expected.Duration, actual.Duration);
            Assert.Equal(expected.Easing, actual.Easing);
            AssertSameSetters(expected.Setters, actual.Setters, path);
        }

        /// <summary>
        /// Asserts that two setter dictionaries (<see cref="Style.Setters"/> or <see cref="VisualState.Setters"/>)
        /// set the same property names to equivalent values, comparing brush values by color rather than reference.
        /// </summary>
        private static void AssertSameSetters(Dictionary<string, object?> expected, Dictionary<string, object?> actual, string path)
        {
            Assert.Equal(expected.Keys.OrderBy(x => x, StringComparer.Ordinal), actual.Keys.OrderBy(x => x, StringComparer.Ordinal));

            foreach (string key in expected.Keys)
            {
                object? expectedValue = expected[key];
                object? actualValue = actual[key];
                if (expectedValue is IBrush || actualValue is IBrush)
                {
                    AssertSameBrush(expectedValue as IBrush, actualValue as IBrush, $"{path}.Setters[{key}]");
                }
                else
                {
                    Assert.Equal(expectedValue, actualValue);
                }
            }
        }

        /// <summary>
        /// Asserts that two <see cref="Timeline"/>s describe the same animation: same target property, duration,
        /// easing, repeat behavior, and keyframes in order.
        /// </summary>
        private static void AssertSameTimeline(Timeline? expected, Timeline? actual, string path)
        {
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.NotNull(actual);
            Assert.Equal(expected.TargetProperty, actual!.TargetProperty);
            Assert.Equal(expected.Duration, actual.Duration);
            Assert.Equal(expected.Easing, actual.Easing);
            Assert.Equal(expected.RepeatCount, actual.RepeatCount);
            Assert.Equal(expected.AutoReverse, actual.AutoReverse);

            Assert.Equal(expected.Keyframes.Count, actual.Keyframes.Count);
            for (int i = 0; i < expected.Keyframes.Count; i++)
            {
                Assert.Equal(expected.Keyframes[i].Offset, actual.Keyframes[i].Offset);
                Assert.Equal(expected.Keyframes[i].Value, actual.Keyframes[i].Value);
            }
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
