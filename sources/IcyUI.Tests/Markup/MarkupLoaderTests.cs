using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Markup
{
    /// <summary>
    /// Covers <see cref="MarkupLoader"/>: the element/attribute resolution rules, the content and property-element
    /// syntax, directives, and the error model.
    /// </summary>
    public class MarkupLoaderTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        private static MarkupLoader CreateLoader() => new(CreateConfiguration());

        [Fact]
        public void Load_ResolvesBuiltInTagsWithNoNamespaceDeclaration()
        {
            var loader = CreateLoader();

            UIElement root = loader.Load("<StackPanel/>");

            Assert.IsType<StackPanel>(root);
        }

        [Fact]
        public void Load_ResolvesBuiltInTagsUnderTheExplicitDefaultNamespace()
        {
            var loader = CreateLoader();

            UIElement root = loader.Load($"<StackPanel xmlns=\"{MarkupNamespaces.Default}\"/>");

            Assert.IsType<StackPanel>(root);
        }

        [Fact]
        public void Load_SetsRegisteredPropertiesFromAttributes()
        {
            var loader = CreateLoader();

            var panel = (StackPanel)loader.Load("<StackPanel Orientation=\"Horizontal\" Padding=\"12,6\"/>");

            Assert.Equal(Orientation.Horizontal, panel.Orientation);
            Assert.Equal(new Thickness(12, 6), panel.Padding);
        }

        [Fact]
        public void Load_SetsUnregisteredClrPropertiesFromAttributes()
        {
            // ColumnDefinition is a plain data object with no [RegisterReference] anywhere on it, so the property
            // registry knows nothing about it - markup still has to be able to set its properties.
            var loader = CreateLoader();

            var grid = (Grid)loader.Load(
                """
                <Grid>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="24"/>
                  </Grid.ColumnDefinitions>
                </Grid>
                """);

            Assert.Equal(3, grid.ColumnDefinitions.Count);
            Assert.Equal(GridLength.Star, grid.ColumnDefinitions[0].Width);
            Assert.Equal(GridLength.Auto, grid.ColumnDefinitions[1].Width);
            Assert.Equal(new GridLength(24, GridUnitType.Pixel), grid.ColumnDefinitions[2].Width);
        }

        [Fact]
        public void Load_AddsChildElementsToTheContentCollection()
        {
            var loader = CreateLoader();

            var panel = (StackPanel)loader.Load(
                """
                <StackPanel>
                  <TextBlock Text="first"/>
                  <TextBlock Text="second"/>
                </StackPanel>
                """);

            Assert.Equal(2, panel.Children.Count);
            Assert.Equal("first", ((TextBlock)panel.Children[0]).Text);
            Assert.Equal("second", ((TextBlock)panel.Children[1]).Text);
        }

        [Fact]
        public void Load_AssignsASingleChildToASingleValuedContentProperty()
        {
            var loader = CreateLoader();

            var border = (Border)loader.Load("<Border><TextBlock Text=\"inside\"/></Border>");

            Assert.Equal("inside", Assert.IsType<TextBlock>(border.Child).Text);
        }

        [Fact]
        public void Load_RejectsASecondChildOnASingleValuedContentProperty()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load("<Border><TextBlock/><TextBlock/></Border>"));

            Assert.Contains("single piece of content", error.Message);
        }

        [Fact]
        public void Load_AssignsBareTextToAStringContentProperty()
        {
            var loader = CreateLoader();

            var text = (TextBlock)loader.Load("<TextBlock FontSize=\"28\">Hello</TextBlock>");

            Assert.Equal("Hello", text.Text);
            Assert.Equal(28, text.FontSize);
        }

        [Fact]
        public void Load_WrapsBareTextInATextBlockForElementContentProperties()
        {
            var loader = CreateLoader();

            var button = (Button)loader.Load("<Button>Click Me</Button>");

            Assert.Equal("Click Me", Assert.IsType<TextBlock>(button.Content).Text);
        }

        [Fact]
        public void Load_SetsAttachedPropertiesThroughTheirOwnerType()
        {
            var loader = CreateLoader();

            var grid = (Grid)loader.Load(
                """
                <Grid>
                  <TextBlock Grid.Row="2" Grid.Column="1"/>
                </Grid>
                """);

            UIElement cell = grid.Children[0];
            Assert.Equal(2, Grid.GetRow(cell));
            Assert.Equal(1, Grid.GetColumn(cell));
        }

        [Fact]
        public void Load_RegistersNamedElementsAndMakesThemFindable()
        {
            var loader = CreateLoader();

            var panel = (StackPanel)loader.Load(
                """
                <StackPanel>
                  <Button x:Name="ok"/>
                  <TextBlock x:Name="caption"/>
                </StackPanel>
                """);

            Button ok = panel.FindRequiredControl<Button>("ok");
            Assert.Equal("ok", ok.Name);
            Assert.NotNull(panel.FindControl<TextBlock>("caption"));

            // Reachable from anywhere in the tree, not just the root.
            Assert.Same(ok, ok.FindControl<Button>("ok"));
        }

        [Fact]
        public void Load_RejectsDuplicateNames()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load(
                """
                <StackPanel>
                  <Button x:Name="ok"/>
                  <Button x:Name="ok"/>
                </StackPanel>
                """));

            Assert.Contains("Duplicate x:Name 'ok'", error.Message);
        }

        [Fact]
        public void Load_UsesTheBackingClassNamedByXClass()
        {
            var loader = CreateLoader();

            UIElement root = loader.Load($"<StackPanel x:Class=\"{typeof(NamedPanel).FullName}\"/>");

            Assert.IsType<NamedPanel>(root);
        }

        [Fact]
        public void Load_RejectsABackingClassThatDoesNotDeriveFromTheTag()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load($"<Border x:Class=\"{typeof(NamedPanel).FullName}\"/>"));

            Assert.Contains("does not derive from", error.Message);
        }

        [Fact]
        public void Load_ResolvesCustomTypesThroughAClrNamespacePrefix()
        {
            var loader = CreateLoader();
            string assembly = typeof(NamedPanel).Assembly.GetName().Name!;

            UIElement root = loader.Load(
                $"""
                <StackPanel xmlns:test="clr-namespace:{typeof(NamedPanel).Namespace};assembly={assembly}">
                  <test:NamedPanel/>
                </StackPanel>
                """);

            Assert.IsType<NamedPanel>(((StackPanel)root).Children[0]);
        }

        [Fact]
        public void Load_ResolvesCustomTypesThroughARegisteredShortName()
        {
            IcyConfiguration configuration = CreateConfiguration();
            configuration.Types.Markup.RegisterShortName<NamedPanel>("Named");
            var loader = new MarkupLoader(configuration);

            UIElement root = loader.Load("<StackPanel><Named/></StackPanel>");

            Assert.IsType<NamedPanel>(((StackPanel)root).Children[0]);
        }

        [Fact]
        public void RegisterShortName_RejectsShadowingABuiltIn()
        {
            IcyConfiguration configuration = CreateConfiguration();

            var error = Assert.Throws<MarkupException>(() => configuration.Types.Markup.RegisterShortName<NamedPanel>("Button"));

            Assert.Contains("collides with the built-in", error.Message);
        }

        [Fact]
        public void Load_TreatsAnUnknownAttributeAsAnErrorAndSuggestsANearMatch()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load("<Border Bakground=\"#fff\"/>"));

            Assert.Contains("Unknown property 'Bakground'", error.Message);
            Assert.Contains("Did you mean 'Background'?", error.Message);
        }

        [Fact]
        public void Load_TreatsAnUnknownTagAsAnErrorAndSuggestsANearMatch()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load("<StackPnael/>"));

            Assert.Contains("Unknown type 'StackPnael'", error.Message);
            Assert.Contains("Did you mean 'StackPanel'?", error.Message);
        }

        [Fact]
        public void Load_ReportsThePositionOfAnError()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load(
                """
                <StackPanel>
                  <TextBlock Nope="1"/>
                </StackPanel>
                """,
                "UI/Broken.xml"));

            Assert.Equal("UI/Broken.xml", error.SourcePath);
            Assert.Equal(2, error.Line);
            Assert.StartsWith("UI/Broken.xml(2,", error.Message);
        }

        [Fact]
        public void Load_ReportsMalformedXmlAsAMarkupError()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load("<StackPanel><Border></StackPanel>", "UI/Broken.xml"));

            Assert.Equal("UI/Broken.xml", error.SourcePath);
            Assert.NotEqual(0, error.Line);
        }

        [Fact]
        public void Load_RejectsAValueThatLooksLikeAMarkupExtension()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load("<TextBlock Text=\"{Binding Name}\"/>"));

            Assert.Contains("markup extensions aren't supported yet", error.Message);
        }

        [Fact]
        public void Load_UnescapesALiteralValueThatStartsWithABrace()
        {
            var loader = CreateLoader();

            var text = (TextBlock)loader.Load("<TextBlock Text=\"{}{not an extension}\"/>");

            Assert.Equal("{not an extension}", text.Text);
        }

        [Fact]
        public void Load_KeepsAValueThatMerelyContainsBracesLiteral()
        {
            // The previous implementation matched extensions with a pattern that also caught values like this one.
            var loader = CreateLoader();

            var text = (TextBlock)loader.Load("<TextBlock Text=\"total: {0}\"/>");

            Assert.Equal("total: {0}", text.Text);
        }

        [Fact]
        public void Load_RejectsAPropertyElementThatDoesNotBelongToItsParent()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load(
                """
                <Border>
                  <Grid.ColumnDefinitions/>
                </Border>
                """));

            Assert.Contains("its parent is a 'Border'", error.Message);
        }

        [Fact]
        public void Load_RejectsContentOnATypeWithNoContentProperty()
        {
            var loader = CreateLoader();

            var error = Assert.Throws<MarkupException>(() => loader.Load("<Slider>oops</Slider>"));

            Assert.Contains("has no content property", error.Message);
        }

        [Fact]
        public void Load_BuildsElementsAgainstTheConfigurationsPropertyRegistry()
        {
            IcyConfiguration configuration = CreateConfiguration();
            var isolated = new Icy.Data.Markup.PropertyRegistry();
            configuration.Types.PropertyRegistry = isolated;
            var loader = new MarkupLoader(configuration);

            var panel = (StackPanel)loader.Load("<StackPanel><TextBlock/></StackPanel>");

            Assert.Same(isolated, panel.PropertyRegistry);
            Assert.Same(isolated, panel.Children[0].PropertyRegistry);
        }

        [Fact]
        public void Load_IgnoresWhitespaceBetweenChildElements()
        {
            var loader = CreateLoader();

            var border = (Border)loader.Load(
                """
                <Border>

                  <TextBlock Text="inside"/>

                </Border>
                """);

            Assert.Equal("inside", Assert.IsType<TextBlock>(border.Child).Text);
        }
    }

    /// <summary>
    /// A custom element used to exercise <c>x:Class</c>, <c>clr-namespace</c> prefixes, and short names.
    /// </summary>
    /// <remarks>
    /// Top-level rather than nested in the test class on purpose: a nested type's CLR name is
    /// <c>Outer+Inner</c>, which is not what a <c>clr-namespace</c> declaration can name.
    /// </remarks>
    public class NamedPanel : StackPanel
    {
    }
}
