// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Drawing;
using Icy.Assets;
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
    /// Covers <c>{TemplateBinding}</c> (see <see cref="Icy.Markup.Extensions.TemplateBindingExtension"/>): pulling
    /// a value from - and staying in sync with - the control a <see cref="ControlTemplate"/>'s content is built
    /// for.
    /// </summary>
    public class TemplateBindingExtensionTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void PicksUpTheControlsPropertyAtLoadTime()
        {
            var loader = new MarkupLoader(CreateConfiguration());
            var template = (ControlTemplate)loader.LoadObject(
                """
                <ControlTemplate TargetType="Control">
                  <Border Background="{TemplateBinding Background}"/>
                </ControlTemplate>
                """);
            var control = new Control { Background = new SolidColorBrush(Color.Red) };

            var built = (Border)template.LoadContent(control);

            Assert.Same(control.Background, built.Background);
        }

        [Fact]
        public void UpdatesReactivelyAfterTheControlsPropertyChangesPostLoad()
        {
            // TextBlock.Text, not Control.Background, is the property changed here: at this point in the codebase,
            // Control's own decoration properties forward straight into an internal Chrome element without calling
            // SetProperty on the Control itself, so they don't yet raise PropertyChanged on the templated control
            // (Control.Template's own dual-path wiring, covered separately in ControlTests, is what adds that).
            // This test only needs to prove {TemplateBinding}'s own reactive-refresh mechanism, against any
            // property that already raises PropertyChanged correctly today.
            var loader = new MarkupLoader(CreateConfiguration());
            var template = (ControlTemplate)loader.LoadObject(
                """
                <ControlTemplate TargetType="TextBlock">
                  <TextBlock Text="{TemplateBinding Path=Text}"/>
                </ControlTemplate>
                """);
            var source = new TextBlock { Text = "before" };
            var built = (TextBlock)template.LoadContent(source);

            source.Text = "after";

            Assert.Equal("after", built.Text);
        }

        [Fact]
        public void UsedOutsideTemplateContent_Throws()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.LoadObject("""<Border Background="{TemplateBinding Background}"/>"""));
            Assert.Contains("ControlTemplate", ex.Description);
        }

        [Fact]
        public void TargetingANonBindableProperty_ThrowsWhenTheTemplateIsInstantiated()
        {
            // UIElement.Name is [NonBindable] - see Data/Bindings/Attributes/NonBindableAttribute.cs - the same
            // property BindingExtensionTests uses to cover the equivalent {Binding} case.
            var loader = new MarkupLoader(CreateConfiguration());
            var template = (ControlTemplate)loader.LoadObject(
                """
                <ControlTemplate TargetType="Control">
                  <TextBlock Name="{TemplateBinding Path=Background}"/>
                </ControlTemplate>
                """);
            var control = new Control();

            var ex = Assert.Throws<MarkupException>(() => template.LoadContent(control));
            Assert.Contains("bind", ex.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void WithNoPath_Throws()
        {
            var loader = new MarkupLoader(CreateConfiguration());
            var template = (ControlTemplate)loader.LoadObject(
                """
                <ControlTemplate TargetType="Control">
                  <Border Background="{TemplateBinding}"/>
                </ControlTemplate>
                """);
            var control = new Control();

            Assert.Throws<MarkupException>(() => template.LoadContent(control));
        }
    }
}
