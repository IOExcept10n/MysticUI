// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupLoaderConstructorBindingTests
    {
        private class RequiresName : UIElement
        {
            public RequiresName(string name)
            {
                BoundName = name;
            }

            public string BoundName { get; }

            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        private class RequiresNameAndState : UIElement
        {
            public RequiresNameAndState(string name, string state)
            {
                ConstructorName = name;
                ConstructorState = state;
            }

            public string ConstructorName { get; }
            public string ConstructorState { get; }

            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<RequiresName>();
            configuration.Types.Markup.RegisterShortName<RequiresNameAndState>();
            return configuration;
        }

        [Fact]
        public void CreateInstance_BindsAttributeToMatchingConstructorParameter_CaseInsensitive()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var element = (RequiresName)loader.Load("""<RequiresName nAmE="Alice"/>""");

            Assert.Equal("Alice", element.BoundName);
        }

        [Fact]
        public void CreateInstance_MissingRequiredAttribute_ThrowsMarkupException()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.Load("""<RequiresName/>"""));
            Assert.Contains("name", ex.Description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CreateInstance_ConstructorAttributeIsNotAlsoAppliedAsAnOrdinaryProperty()
        {
            // BoundName has no setter - if the loader tried to apply "Name" a second time as a property after
            // consuming it for the constructor, this would throw "read-only" instead of succeeding.
            var loader = new MarkupLoader(CreateConfiguration());

            var element = (RequiresName)loader.Load("""<RequiresName Name="Bob"/>""");

            Assert.Equal("Bob", element.BoundName);
        }

        [Fact]
        public void CreateInstance_DirectivesNotSilentlyDroppedWhenConstructorParameterNameCollides()
        {
            // Regression test: ensure x:Name directive is processed even when constructor parameter is named "name"
            // (the directive's LocalName is "Name", which could collide with a case-insensitive parameter match).
            // Both the constructor binding AND the x:Name registration must succeed.
            var loader = new MarkupLoader(CreateConfiguration());

            var element = (RequiresNameAndState)loader.Load("""<RequiresNameAndState x:Name="RegisteredName" name="ConstructorValue" state="Active"/>""");

            Assert.Equal("ConstructorValue", element.ConstructorName);
            Assert.Equal("Active", element.ConstructorState);
            Assert.Equal("RegisteredName", element.Name);  // x:Name must have been applied
        }
    }
}
