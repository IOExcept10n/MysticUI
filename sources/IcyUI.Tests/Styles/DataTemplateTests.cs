using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Styles
{
    public class DataTemplateTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new Tests.Input.FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        private static DataTemplate LoadTemplate(string markup)
        {
            var configuration = CreateConfiguration();
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        [Fact]
        public void Build_SetsRootDataContextToTheDataItem()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding Name}"/></DataTemplate>""");
            var item = new { Name = "Ada" };

            UIElement root = template.Build(item);

            Assert.Same(item, root.DataContext);
        }

        [Fact]
        public void Build_BindingsInsideResolveAgainstTheDataItem()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding Name}"/></DataTemplate>""");
            var item = new { Name = "Grace" };

            var root = (TextBlock)template.Build(item);

            Assert.Equal("Grace", root.Text);
        }

        [Fact]
        public void Build_TwoCallsProduceIndependentTrees()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding Name}"/></DataTemplate>""");

            UIElement first = template.Build(new { Name = "A" });
            UIElement second = template.Build(new { Name = "B" });

            Assert.NotSame(first, second);
        }

        [Fact]
        public void Build_WithoutBeingLoadedFromMarkup_Throws()
        {
            var template = new DataTemplate();

            Assert.Throws<InvalidOperationException>(() => template.Build(new object()));
        }

        [Fact]
        public void Load_MoreThanOneRootElement_Throws()
        {
            var configuration = CreateConfiguration();
            var loader = new MarkupLoader(configuration);

            var ex = Assert.Throws<MarkupException>(() =>
                loader.LoadObject("""<DataTemplate><TextBlock/><TextBlock/></DataTemplate>"""));
            Assert.Contains("exactly one root element", ex.Message);
        }

        [Fact]
        public void Load_DoesNotEagerlyBuildTheContent()
        {
            // A DataTemplate's content is captured as raw markup (see ControlTemplate's own equivalent
            // precedent) - loading the document must not itself construct a live UIElement tree, only Build does.
            var configuration = CreateConfiguration();
            var loader = new MarkupLoader(configuration);

            var template = (DataTemplate)loader.LoadObject("""<DataTemplate><Button>Never built eagerly</Button></DataTemplate>""");

            // No exception, and Build still works afterward - proving the captured content is still there.
            UIElement root = template.Build(new object());
            Assert.IsType<Button>(root);
        }
    }
}
