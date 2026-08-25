using System.ComponentModel;
using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Bindings;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Markup
{
    /// <summary>
    /// Covers the <c>{Name ...}</c> markup extension grammar (<see cref="MarkupExtensionSyntax"/>), the built-in
    /// <c>{Binding}</c> extension, and <see cref="UIElement.DataContext"/> inheritance - the M2 slice of the
    /// markup system.
    /// </summary>
    public class MarkupExtensionTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        private static MarkupLoader CreateLoader() => new(CreateConfiguration());

        private class TestViewModel : INotifyPropertyChanged
        {
            private string name = string.Empty;

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Name
            {
                get => name;
                set
                {
                    name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }

            public TestViewModel? Child { get; set; }
        }

        // MarkupExtensionSyntax

        [Theory]
        [InlineData("{Binding}", "Binding")]
        [InlineData("{Binding Path}", "Binding")]
        [InlineData("{StaticResource Foo}", "StaticResource")]
        public void TryParse_ReadsTheExtensionName(string text, string expectedName)
        {
            bool parsed = MarkupExtensionSyntax.TryParse(text, out string name, out _);

            Assert.True(parsed);
            Assert.Equal(expectedName, name);
        }

        [Fact]
        public void TryParse_ReadsAPositionalArgument()
        {
            bool parsed = MarkupExtensionSyntax.TryParse("{Binding Name}", out _, out List<(string? Key, string Value)> arguments);

            Assert.True(parsed);
            var argument = Assert.Single(arguments);
            Assert.Null(argument.Key);
            Assert.Equal("Name", argument.Value);
        }

        [Fact]
        public void TryParse_ReadsNamedArguments()
        {
            bool parsed = MarkupExtensionSyntax.TryParse(
                "{Binding Path=Name, Mode=TwoWay}",
                out _,
                out List<(string? Key, string Value)> arguments);

            Assert.True(parsed);
            Assert.Equal(2, arguments.Count);
            Assert.Equal(("Path", "Name"), arguments[0]);
            Assert.Equal(("Mode", "TwoWay"), arguments[1]);
        }

        [Fact]
        public void TryParse_UnquotesAValueThatContainsAComma()
        {
            bool parsed = MarkupExtensionSyntax.TryParse(
                "{Binding Path='A, B'}",
                out _,
                out List<(string? Key, string Value)> arguments);

            Assert.True(parsed);
            Assert.Equal(("Path", "A, B"), Assert.Single(arguments));
        }

        [Fact]
        public void TryParse_RejectsTextWithoutBraces()
        {
            Assert.False(MarkupExtensionSyntax.TryParse("Binding Name", out _, out _));
        }

        // {} escape and unsupported positions

        [Fact]
        public void Load_TreatsAnEscapedBraceAsLiteralText()
        {
            var loader = CreateLoader();

            var text = (TextBlock)loader.Load("<TextBlock>{}{Binding Name}</TextBlock>");

            Assert.Equal("{Binding Name}", text.Text);
        }

        [Fact]
        public void Load_LeavesExtensionLookingTextLiteralWhenWrittenAsBareContentSugar()
        {
            // Content sugar (a UIElement content property that isn't itself string-typed, e.g. Button.Content)
            // goes through IMarkupTextAdapter, which turns bare text into a whole child element and never resolves
            // extensions - so this is literal text on the generated TextBlock, not a binding.
            var loader = CreateLoader();

            var button = (Button)loader.Load("<Button>{Binding Name}</Button>");

            Assert.Equal("{Binding Name}", ((TextBlock)button.Content!).Text);
        }

        [Fact]
        public void Load_RejectsAnUnknownExtensionName()
        {
            var loader = CreateLoader();

            MarkupException ex = Assert.Throws<MarkupException>(() => loader.Load("<TextBlock Text=\"{Bindign Name}\"/>"));

            Assert.Contains("Bindign", ex.Description, StringComparison.Ordinal);
            Assert.Contains("Binding", ex.Description, StringComparison.Ordinal);
        }

        // {Binding} against an explicit Source

        [Fact]
        public void Binding_WithExplicitSource_PullsTheInitialValue()
        {
            var loader = CreateLoader();
            var source = new TestViewModel { Name = "Ada" };

            var text = (TextBlock)loader.Load("<TextBlock Text=\"{Binding Path=Name}\"/>");
            var binding = (Binding)Assert.Single(text.Bindings);
            binding.Source = source;
            binding.UpdateTarget();

            Assert.Equal("Ada", text.Text);
        }

        [Fact]
        public void Binding_PositionalArgument_SetsThePathProperty()
        {
            var loader = CreateLoader();
            var source = new TestViewModel { Name = "Grace" };

            var text = (TextBlock)loader.Load("<TextBlock Text=\"{Binding Name}\"/>");
            var binding = (Binding)Assert.Single(text.Bindings);
            binding.Source = source;
            binding.UpdateTarget();

            Assert.Equal("Grace", text.Text);
        }

        [Fact]
        public void Binding_ReactsToSourcePropertyChanged()
        {
            var loader = CreateLoader();
            var source = new TestViewModel { Name = "Ada" };
            var text = (TextBlock)loader.Load("<TextBlock Text=\"{Binding Path=Name}\"/>");
            var binding = (Binding)Assert.Single(text.Bindings);
            binding.Source = source;
            binding.UpdateTarget();

            source.Name = "Lovelace";

            Assert.Equal("Lovelace", text.Text);
        }

        [Fact]
        public void Binding_ToANonBindableProperty_ThrowsAtLoadTime()
        {
            // UIElement.Name is [NonBindable] - see Data/Bindings/Attributes/NonBindableAttribute.cs.
            var loader = CreateLoader();

            MarkupException ex = Assert.Throws<MarkupException>(() => loader.Load("<TextBlock x:Name=\"a\" Name=\"{Binding Path=Name}\"/>"));
            Assert.Contains("bind", ex.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Binding_OnAPlainDataObject_ThrowsAtLoadTime()
        {
            // ColumnDefinition is a plain data object, not a DependencyObject/BindableObject at all - it can't be
            // a binding target regardless of whether Width happens to be registered.
            var loader = CreateLoader();

            MarkupException ex = Assert.Throws<MarkupException>(() => loader.Load(
                """
                <Grid>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="{Binding Path=Name}"/>
                  </Grid.ColumnDefinitions>
                </Grid>
                """));
            Assert.Contains("doesn't support bindings", ex.Description, StringComparison.OrdinalIgnoreCase);
        }

        // {Binding ElementName=...}

        [Fact]
        public void Binding_WithElementName_ResolvesThroughTheNameScope()
        {
            var loader = CreateLoader();

            var root = (StackPanel)loader.Load(
                """
                <StackPanel>
                  <TextBox x:Name="input" Text="Typed"/>
                  <TextBlock Text="{Binding Path=Text, ElementName=input}"/>
                </StackPanel>
                """);

            var display = (TextBlock)root.Children[1];
            Assert.Equal("Typed", display.Text);
        }

        [Fact]
        public void Binding_WithAnUnknownElementName_ThrowsAtLoadTime()
        {
            var loader = CreateLoader();

            MarkupException ex = Assert.Throws<MarkupException>(() => loader.Load("<TextBlock Text=\"{Binding ElementName=nope}\"/>"));
            Assert.Contains("nope", ex.Description, StringComparison.Ordinal);
        }

        // {Binding} against the implicit DataContext

        [Fact]
        public void Binding_WithNoSource_TracksDataContextSetAfterLoad()
        {
            var loader = CreateLoader();

            var text = (TextBlock)loader.Load("<TextBlock Text=\"{Binding Path=Name}\"/>");
            text.DataContext = new TestViewModel { Name = "Root" };

            Assert.Equal("Root", text.Text);
        }

        [Fact]
        public void Binding_WithNoSource_TracksAnInheritedDataContextEstablishedAfterConstruction()
        {
            // The bound TextBlock is constructed - and its {Binding} extension evaluated - before it is added to
            // the StackPanel, so at bind time it has no Parent and therefore no effective DataContext yet. This is
            // the scenario DataContextChanged's cascade exists for.
            var loader = CreateLoader();

            var root = (StackPanel)loader.Load(
                """
                <StackPanel>
                  <TextBlock Text="{Binding Path=Name}"/>
                </StackPanel>
                """);
            root.DataContext = new TestViewModel { Name = "Inherited" };

            var text = (TextBlock)root.Children[0];
            Assert.Equal("Inherited", text.Text);
        }

        [Fact]
        public void Binding_WithNoSource_RebindsWhenDataContextIsReassigned()
        {
            var loader = CreateLoader();
            var text = (TextBlock)loader.Load("<TextBlock Text=\"{Binding Path=Name}\"/>");
            text.DataContext = new TestViewModel { Name = "First" };
            Assert.Equal("First", text.Text);

            text.DataContext = new TestViewModel { Name = "Second" };

            Assert.Equal("Second", text.Text);
        }

        // UIElement.DataContext inheritance itself

        [Fact]
        public void DataContext_IsInheritedFromParent()
        {
            var parent = new StackPanel();
            var child = new TextBlock();
            parent.Children.Add(child);
            var context = new object();

            parent.DataContext = context;

            Assert.Same(context, child.DataContext);
        }

        [Fact]
        public void DataContext_OwnValueOverridesTheInheritedOne()
        {
            var parent = new StackPanel { DataContext = new object() };
            var child = new TextBlock();
            parent.Children.Add(child);
            var own = new object();

            child.DataContext = own;

            Assert.Same(own, child.DataContext);
            Assert.NotSame(parent.DataContext, child.DataContext);
        }

        [Fact]
        public void DataContext_RaisesChangedOnDescendantsThatInheritIt()
        {
            var parent = new StackPanel();
            var child = new TextBlock();
            parent.Children.Add(child);
            int raised = 0;
            child.DataContextChanged += (_, _) => raised++;

            parent.DataContext = new object();

            Assert.Equal(1, raised);
        }

        [Fact]
        public void DataContext_DoesNotRaiseOnADescendantThatHasItsOwnValue()
        {
            var parent = new StackPanel();
            var child = new TextBlock { DataContext = new object() };
            parent.Children.Add(child);
            int raised = 0;
            child.DataContextChanged += (_, _) => raised++;

            parent.DataContext = new object();

            Assert.Equal(0, raised);
        }
    }
}
