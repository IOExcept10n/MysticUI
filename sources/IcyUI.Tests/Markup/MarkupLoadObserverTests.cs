// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Text;
using System.Xml.Linq;
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
    public class MarkupLoadObserverTests
    {
        [Fact]
        public void Load_ReportsEveryStepInDocumentOrder()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            loader.Load("<StackPanel Orientation=\"Horizontal\">\n  <Border Width=\"10\"/>\n</StackPanel>");

            Assert.Equal(
            [
                "Started:Document",
                "Created:StackPanel@1:2",
                "Applied:Orientation",
                "Created:Border@2:4",
                "Applied:Width",
                "Completed:StackPanel",
            ],
            observer.Events);
        }

        [Fact]
        public void Scope_CarriesTheExactSourceTextPathAndRoot()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();
            const string text = "<StackPanel x:Name=\"root\"/>";

            UIElement root = loader.Load(text, "Pages/Main.xml");

            MarkupLoadScope scope = Assert.Single(observer.Scopes);
            Assert.Equal(MarkupLoadScopeKind.Document, scope.Kind);
            Assert.Equal(text, scope.SourceText);
            Assert.Equal("Pages/Main.xml", scope.SourcePath);
            Assert.Same(root, scope.Root);
            Assert.Same(MarkupNameScope.GetScope(root), scope.NameScope);
        }

        [Fact]
        public void Scope_SourceTextExcludesAByteOrderMark()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();
            const string text = "<Border Width=\"3\"/>";
            byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];

            loader.Load(new MemoryStream(bytes));

            Assert.Equal(text, Assert.Single(observer.Scopes).SourceText);
        }

        [Fact]
        public void MemberApplied_ReportsTheBindingAMarkupExtensionCreated()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            var block = (TextBlock)loader.Load("<TextBlock Text=\"{Binding Path=Message}\"/>");

            (XObject node, object target, MarkupMember member, IBinding? binding) = Assert.Single(observer.Members);
            Assert.IsType<XAttribute>(node);
            Assert.Same(block, target);
            Assert.Equal("Text", member.Name);
            Assert.NotNull(binding);
            Assert.Contains(binding, block.Bindings);
        }

        [Fact]
        public void LoadFailure_ReportsFailedAndStillThrows()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            Assert.Throws<MarkupException>(() => loader.Load("<StackPanel Bogus=\"1\"/>"));

            Assert.Equal(["Started:Document", "Created:StackPanel@1:2", "Failed"], observer.Events);
        }

        [Fact]
        public void MalformedXml_ReportsFailed()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            Assert.Throws<MarkupException>(() => loader.Load("<StackPanel>"));

            Assert.Equal(["Started:Document", "Failed"], observer.Events);
        }

        [Fact]
        public void ObserverThatObservesNothing_IsNeverCalled()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.None);

            loader.Load("<StackPanel><Border Width=\"10\"/></StackPanel>");

            Assert.Empty(observer.Events);
        }

        internal static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        private static (MarkupLoader Loader, RecordingLoadObserver Observer) Create(MarkupLoadScopeKind kinds = MarkupLoadScopeKind.All)
        {
            IcyConfiguration configuration = CreateConfiguration();
            var observer = new RecordingLoadObserver(kinds);
            configuration.Types.Markup.LoadObserver = observer;
            return (new MarkupLoader(configuration), observer);
        }
    }
}
