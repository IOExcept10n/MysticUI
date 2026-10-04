// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.Design;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class DesignSessionTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="box" Width="10"/>
              <TextBlock x:Name="label" Text="{Binding Path=Message}"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void Attach_InstallsTheObserverAndRefusesASecondSession()
        {
            using var host = new DesignTestHost();

            Assert.NotNull(host.Configuration.Types.Markup.LoadObserver);
            Assert.Throws<InvalidOperationException>(() => DesignSession.Attach(host.Configuration));
        }

        [Fact]
        public void Dispose_UninstallsTheObserver()
        {
            var host = new DesignTestHost();

            host.Dispose();

            Assert.Null(host.Configuration.Types.Markup.LoadObserver);
        }

        [Fact]
        public void FindDocument_MapsEveryMarkupElementToItsNode()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            foreach (string name in new[] { "root", "box", "label" })
            {
                UIElement element = DesignTestHost.Named<UIElement>(root, name);
                Assert.Same(document, host.Session.FindDocument(element, out NodeId id));
                ElementSyntax node = document.GetNode(id)!;
                Assert.Equal(element.GetType().Name, node.Name);
                Assert.Same(element, Assert.Single(document.GetObjects(id)));
            }
        }

        [Fact]
        public void Document_HoldsTheExactSourceText()
        {
            using var host = new DesignTestHost();

            (_, DesignDocument document) = host.Load(Page, "Pages/Main.xml");

            Assert.Equal(Page, document.Text);
            Assert.Equal("Pages/Main.xml", document.SourcePath);
            Assert.Equal(0, document.Version);
        }

        [Fact]
        public void Correlation_WorksWithCrlfTabsAndMultiLineAttributes()
        {
            using var host = new DesignTestHost();
            string markup = "<StackPanel x:Name=\"root\">\r\n\t<Border\r\n\t\tx:Name=\"box\"\r\n\t\tWidth=\"10\"/>\r\n\t<TextBlock x:Name=\"label\"/>\r\n</StackPanel>";

            (UIElement root, DesignDocument document) = host.Load(markup);

            Assert.Equal("Border", document.GetNode(host.IdOf(DesignTestHost.Named<Border>(root, "box")))!.Name);
            Assert.Equal("TextBlock", document.GetNode(host.IdOf(DesignTestHost.Named<TextBlock>(root, "label")))!.Name);
        }

        [Fact]
        public void Map_RecordsTheMemberAndBindingEachAttributeApplied()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var label = DesignTestHost.Named<TextBlock>(root, "label");

            Assert.True(document.Map.TryGetEntry(label, out var entry));
            Assert.Equal("Text", entry!.Members["Text"].Member.Name);
            IBinding binding = entry.Members["Text"].Binding!;
            Assert.Contains(binding, label.Bindings);
        }

        [Fact]
        public void RuntimeContentAndPagesLoadedBeforeAttach_AreUntracked()
        {
            var configuration = Icy.Tests.Markup.MarkupLoadObserverTests.CreateConfiguration();
            UIElement early = new MarkupLoader(configuration).Load("<StackPanel/>");
            using DesignSession session = DesignSession.Attach(configuration);
            var page = (StackPanel)new MarkupLoader(configuration).Load("<StackPanel/>");
            var runtime = new Border();
            page.Children.Add(runtime);

            Assert.Null(session.FindDocument(early, out _));
            Assert.Null(session.FindDocument(runtime, out _));
            Assert.NotNull(session.FindDocument(page, out _));
        }

        [Fact]
        public void SameFileLoadedTwice_SharesOneDocument()
        {
            using var host = new DesignTestHost();

            (UIElement first, DesignDocument document) = host.Load(Page, "Pages/Main.xml");
            (UIElement second, DesignDocument again) = host.Load(Page, "Pages/Main.xml");

            Assert.Same(document, again);
            Assert.Equal(2, document.GetObjects(host.IdOf(first)).Count);
            Assert.Contains(second, document.GetObjects(host.IdOf(first)));
        }

        [Fact]
        public void SamePathWithDifferentText_GetsItsOwnDocument()
        {
            using var host = new DesignTestHost();

            (_, DesignDocument first) = host.Load("<StackPanel/>", "Pages/Main.xml");
            (_, DesignDocument second) = host.Load("<Border/>", "Pages/Main.xml");

            Assert.NotSame(first, second);
        }

        [Fact]
        public void FailedLoad_IsNotTracked()
        {
            using var host = new DesignTestHost();

            Assert.Throws<MarkupException>(() => host.Loader.Load("<StackPanel Bogus=\"1\"/>", "bad.xml"));

            Assert.DoesNotContain(host.Session.Documents, x => x.SourcePath == "bad.xml");
        }
    }
}
