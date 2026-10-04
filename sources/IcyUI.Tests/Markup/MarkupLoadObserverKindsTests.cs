// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Markup;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupLoadObserverKindsTests
    {
        private const string Page = "<StackPanel Orientation=\"Horizontal\"><Border Width=\"10\"/><TextBlock Text=\"Hi\"/></StackPanel>";

        [Fact]
        public void DataTemplateBuild_ReportsADataTemplateContentScopeWithoutText()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.All);
            var template = (DataTemplate)loader.LoadObject("<DataTemplate><Border Height=\"20\"/></DataTemplate>");
            observer.Events.Clear();
            observer.Scopes.Clear();

            template.Build(new object());

            Assert.Equal(["Started:DataTemplateContent", "Created:Border@1:16", "Applied:Height", "Completed:Border"], observer.Events);
            Assert.Null(Assert.Single(observer.Scopes).SourceText);
        }

        [Fact]
        public void ControlTemplateLoadContent_ReportsATemplateContentScope()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.All);
            var template = (ControlTemplate)loader.LoadObject("<ControlTemplate TargetType=\"Button\"><Border/></ControlTemplate>");
            observer.Scopes.Clear();

            template.LoadContent(new Button());

            Assert.Equal(MarkupLoadScopeKind.TemplateContent, Assert.Single(observer.Scopes).Kind);
        }

        [Fact]
        public void TemplateKindsNotObserved_ProduceNoCalls()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.Document | MarkupLoadScopeKind.MergedDictionary);
            var template = (DataTemplate)loader.LoadObject("<DataTemplate><Border Height=\"20\"/></DataTemplate>");
            observer.Events.Clear();

            for (int i = 0; i < 10; i++)
                template.Build(new object());

            Assert.Empty(observer.Events);
        }

        [Fact]
        public void MergedDictionarySource_ReportsAMergedDictionaryScope()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.All, registerDictionaryImporter: true);

            loader.LoadObject(
                """
                <ResourceDictionary>
                  <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="Resources/theme.xml"/>
                  </ResourceDictionary.MergedDictionaries>
                </ResourceDictionary>
                """);

            Assert.Equal([MarkupLoadScopeKind.Document, MarkupLoadScopeKind.MergedDictionary], observer.Scopes.Select(x => x.Kind));
            Assert.NotNull(observer.Scopes[1].SourceText);
        }

        [Fact]
        public void UnobservedLoads_CreateNoScopesAndAllocateNothingExtra()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            var loader = new MarkupLoader(configuration);

            long withoutObserver = MeasureLoad(loader);

            configuration.Types.Markup.LoadObserver = new RecordingLoadObserver(MarkupLoadScopeKind.None);
            int scopesBefore = MarkupLoadScope.CreatedOnCurrentThread;
            long withFilteredObserver = MeasureLoad(loader);

            Assert.Equal(scopesBefore, MarkupLoadScope.CreatedOnCurrentThread);
            Assert.Equal(withoutObserver, withFilteredObserver);

            // Sanity check that the measurement can see the observed path's cost at all: it copies the source text.
            configuration.Types.Markup.LoadObserver = new RecordingLoadObserver(MarkupLoadScopeKind.Document);
            long observed = MeasureLoad(loader);
            Assert.True(observed >= withoutObserver + (Page.Length * sizeof(char)), $"observed {observed}, unobserved {withoutObserver}");
        }

        private static long MeasureLoad(MarkupLoader loader)
        {
            // Warm up JIT and every lazily-filled cache on this path, so only the load itself is measured.
            for (int i = 0; i < 5; i++)
                loader.Load(Page);

            long before = GC.GetAllocatedBytesForCurrentThread();
            loader.Load(Page);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static (MarkupLoader Loader, RecordingLoadObserver Observer) Create(MarkupLoadScopeKind kinds, bool registerDictionaryImporter = false)
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            if (registerDictionaryImporter)
                configuration.Assets.AssetResolver.RegisterImporter(new ResourceDictionaryImporter(configuration));

            var observer = new RecordingLoadObserver(kinds);
            configuration.Types.Markup.LoadObserver = observer;
            return (new MarkupLoader(configuration), observer);
        }
    }
}
