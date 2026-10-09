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
        public void Build_BindingANonStringValueIntoAStringProperty_CoercesItToString()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding Count}"/></DataTemplate>""");

            var root = (TextBlock)template.Build(new { Count = 42 });

            Assert.Equal("42", root.Text);
        }

        [Fact]
        public void Build_BindingAnEnumItemIntoText_ShowsItsName()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");

            var root = (TextBlock)template.Build(DayOfWeek.Friday);

            Assert.Equal("Friday", root.Text);
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
            // precedent) - loading the document must not itself attempt to construct/resolve the content,
            // only Build does. Content that's guaranteed to fail construction (an unresolvable type name)
            // makes this discriminating: if LoadObject eagerly built the content, it would throw HERE; it
            // doesn't, proving the load is deferred. Build() is where construction actually happens, and it
            // does throw there for the same underlying reason - confirming the deferred content is real, not
            // just silently dropped.
            var configuration = CreateConfiguration();
            var loader = new MarkupLoader(configuration);

            var template = (DataTemplate)loader.LoadObject("""<DataTemplate><ThisTypeDoesNotExistAnywhere/></DataTemplate>""");

            Assert.Throws<MarkupException>(() => template.Build(new object()));
        }

        private sealed class Renamable : System.ComponentModel.INotifyPropertyChanged
        {
            private string name = "a";

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

            public string Name
            {
                get => name;
                set
                {
                    name = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
                }
            }

            public override string ToString() => name;
        }

        [Fact]
        public void Default_RereadsTheText_WhenTheItemRaisesPropertyChanged()
        {
            var first = new Renamable();
            var text = Assert.IsType<TextBlock>(DataTemplate.Default.Build(first));

            first.Name = "b";
            Assert.Equal("b", text.Text);

            // A reused container stops following the previous item.
            text.DataContext = new Renamable { Name = "c" };
            first.Name = "d";
            Assert.Equal("c", text.Text);
        }
    }
}
