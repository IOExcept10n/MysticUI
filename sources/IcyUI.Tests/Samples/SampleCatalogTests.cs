// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Samples
{
    public class SampleCatalogTests
    {
        public static TheoryData<string> Names
        {
            get
            {
                var names = new TheoryData<string>();
                foreach (SampleEntry entry in SampleCatalog.All)
                    names.Add(entry.Name);
                return names;
            }
        }

        [Fact]
        public void All_ListsTheTwentyDemosUnderTheAgreedCategories()
        {
            string[][] expected =
            [
                ["Basics", "Controls", "Styles", "Navigation"],
                ["Markup", "Markup", "Markup Styles", "Control Templates"],
                ["Layout", "Split Pane", "Expander", "Wrap Grid", "Scaling"],
                ["Items", "Items Control", "List Box", "Selector", "Tab Control", "Tree View"],
                ["Dialogs & Pickers", "Dialog", "Color Picker"],
                ["Design Tools", "Property Grid", "Design", "Editor"],
            ];

            string[][] actual = SampleCatalog.All
                .GroupBy(e => e.Category)
                .Select(g => g.Select(e => e.Name).Prepend(g.Key).ToArray())
                .ToArray();

            Assert.Equal(expected, actual);
            Assert.Equal(20, SampleCatalog.All.Count);
            Assert.Equal(SampleCatalog.All.Count, SampleCatalog.All.Select(e => e.Name).Distinct().Count());
        }

        [Fact]
        public void OnlyControlsAndStyles_ScrollThemselves() =>
            Assert.Equal(["Controls", "Styles"], SampleCatalog.All.Where(e => e.ScrollsItself).Select(e => e.Name));

        [Theory]
        [MemberData(nameof(Names))]
        public void EveryDemo_BuildsWithTheSessionAttached(string name)
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new System.Drawing.Size(1280, 720) })
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration configuration = builder.Build().UseDefaultTheme();
            DesignDemo.SessionFor(configuration);

            UIElement root = SampleCatalog.All.Single(e => e.Name == name).Build(configuration, "Airfool");

            Assert.NotNull(root);
        }
    }
}
