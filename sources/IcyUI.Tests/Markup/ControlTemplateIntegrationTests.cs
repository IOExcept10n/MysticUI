// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
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
    /// End-to-end <see cref="ControlTemplate"/> coverage through real markup documents and a real
    /// <see cref="Canvas"/> - loader, <c>{TemplateBinding}</c>, <see cref="ContentPresenter"/>, hit-testing, and
    /// (crucially) <see cref="Style"/>/<see cref="VisualState"/> all working together with no changes of their
    /// own, unlike <see cref="ControlTemplateMarkupTests"/> (loader-only) or
    /// <see cref="TemplateBindingExtensionTests"/> (the extension in isolation).
    /// </summary>
    public class ControlTemplateIntegrationTests
    {
        private static (Canvas Canvas, FakeInputSystem Input, IcyConfiguration Configuration) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input, config);
        }

        private static UIElement GetChrome(Control control) =>
            (UIElement)typeof(Control).GetProperty("Chrome", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;

        [Fact]
        public void TemplatedButton_MeasuresArrangesDrawsAndStillFiresClickOnTap()
        {
            var (canvas, input, configuration) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var loader = new MarkupLoader(configuration);

            UIElement root = loader.Load(
                """
                <StackPanel>
                  <StackPanel.Resources>
                    <ControlTemplate x:Key="ButtonSkin" TargetType="Button">
                      <Border Background="{TemplateBinding Background}" BorderThickness="{TemplateBinding BorderThickness}">
                        <ContentPresenter Content="{TemplateBinding Content}"/>
                      </Border>
                    </ControlTemplate>
                  </StackPanel.Resources>
                  <Button Template="{StaticResource ButtonSkin}" Padding="12,6" Background="Blue"
                          Width="100" Height="40" HorizontalAlignment="Left" VerticalAlignment="Top">Click Me</Button>
                </StackPanel>
                """);
            canvas.Add(root);
            canvas.Render();

            var button = root.EnumerateSubtree().OfType<Button>().Single();
            var chrome = Assert.IsType<Border>(GetChrome(button));
            Assert.False(button.ActualBounds.IsEmpty);
            Assert.False(chrome.ActualBounds.IsEmpty);

            bool clicked = false;
            button.Click += (_, _) => clicked = true;

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(50, 20), 1));

            Assert.True(clicked);
        }

        [Fact]
        public void StyleAndVisualState_OnATemplatedButton_UpdateTheTemplateThroughTemplateBindingAlone()
        {
            var (canvas, _, configuration) = CreateCanvas();
            canvas.IsVisible = true;
            var loader = new MarkupLoader(configuration);

            UIElement root = loader.Load(
                """
                <StackPanel>
                  <StackPanel.Resources>
                    <ControlTemplate x:Key="ButtonSkin" TargetType="Button">
                      <Border Background="{TemplateBinding Background}">
                        <ContentPresenter Content="{TemplateBinding Content}"/>
                      </Border>
                    </ControlTemplate>
                    <Style x:Key="HoverStyle" TargetType="Button" Background="Blue">
                      <Style.StateGroups>
                        <VisualStateGroup Name="CommonStates">
                          <VisualState Name="MouseOver" State="Hovered" Background="LightBlue"/>
                        </VisualStateGroup>
                      </Style.StateGroups>
                    </Style>
                  </StackPanel.Resources>
                  <Button Template="{StaticResource ButtonSkin}" Style="{StaticResource HoverStyle}">Click Me</Button>
                </StackPanel>
                """);
            canvas.Add(root);
            canvas.Render();

            var button = root.EnumerateSubtree().OfType<Button>().Single();
            var chrome = Assert.IsType<Border>(GetChrome(button));

            Assert.Equal(Color.Blue.ToArgb(), ((SolidColorBrush)chrome.Background).Color.ToArgb());

            button.ControlState = ControlState.Hovered;

            // Style/VisualState never touched Chrome/the template at all - they set Button.Background exactly as
            // they would on an untemplated button. The templated Border only followed along via its own
            // {TemplateBinding Background}, which is the whole point of this test.
            Assert.Equal(Color.LightBlue.ToArgb(), ((SolidColorBrush)chrome.Background).Color.ToArgb());
        }

        [Fact]
        public void TemplatedToggleButton_AppearanceChangesWithIsChecked()
        {
            var (canvas, _, configuration) = CreateCanvas();
            canvas.IsVisible = true;
            var loader = new MarkupLoader(configuration);

            UIElement root = loader.Load(
                """
                <StackPanel>
                  <StackPanel.Resources>
                    <ControlTemplate x:Key="ToggleSkin" TargetType="ToggleButton">
                      <Border Background="{TemplateBinding Background}">
                        <ContentPresenter Content="{TemplateBinding Content}"/>
                      </Border>
                    </ControlTemplate>
                    <Style x:Key="CheckedStyle" TargetType="ToggleButton" Background="Gray">
                      <Style.StateGroups>
                        <VisualStateGroup Name="CheckStates">
                          <VisualState Name="Checked" State="Checked" Background="Green"/>
                        </VisualStateGroup>
                      </Style.StateGroups>
                    </Style>
                  </StackPanel.Resources>
                  <ToggleButton Template="{StaticResource ToggleSkin}" Style="{StaticResource CheckedStyle}">Toggle</ToggleButton>
                </StackPanel>
                """);
            canvas.Add(root);
            canvas.Render();

            var toggle = root.EnumerateSubtree().OfType<ToggleButton>().Single();
            var chrome = Assert.IsType<Border>(GetChrome(toggle));

            Assert.Equal(Color.Gray.ToArgb(), ((SolidColorBrush)chrome.Background).Color.ToArgb());

            toggle.IsChecked = true;

            Assert.Equal(Color.Green.ToArgb(), ((SolidColorBrush)chrome.Background).Color.ToArgb());
        }
    }
}
