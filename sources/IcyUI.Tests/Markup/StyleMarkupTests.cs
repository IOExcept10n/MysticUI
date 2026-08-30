// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Drawing;
using Icy.Animations;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Rendering.Brushes;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    public class StyleMarkupTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void FullStyleWithStateGroups_ParsesToTheEquivalentHandBuiltStyle()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var style = (Style)loader.LoadObject(
                """
                <Style TargetType="Button" Background="Blue">
                  <Style.StateGroups>
                    <VisualStateGroup Name="CommonStates">
                      <VisualState Name="MouseOver" State="Hovered" Duration="0:0:0.15" Easing="EaseOutCubic" Background="LightBlue"/>
                    </VisualStateGroup>
                  </Style.StateGroups>
                </Style>
                """);

            Assert.Equal(typeof(Button), style.TargetType);
            var background = Assert.IsType<SolidColorBrush>(style.Setters["Background"]);
            Assert.Equal(Color.Blue.ToArgb(), background.Color.ToArgb());

            VisualStateGroup group = Assert.Single(style.StateGroups);
            Assert.Equal("CommonStates", group.Name);

            VisualState state = Assert.Single(group.States);
            Assert.Equal(ControlState.Hovered, state.State);
            Assert.Equal(TimeSpan.FromMilliseconds(150), state.Duration);
            Assert.NotNull(state.Easing);
            Assert.Equal(Easing.EaseOutCubic, state.Easing);

            var stateBackground = Assert.IsType<SolidColorBrush>(state.Setters["Background"]);
            Assert.Equal(Color.LightBlue.ToArgb(), stateBackground.Color.ToArgb());
        }
    }
}
