// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupBuilderTests
    {
        private static readonly Dictionary<string, string> NoNamespaces = [];

        [Fact]
        public void ParseFragment_ResolvesSuppliedPrefixesAndThePredeclaredX()
        {
            IMarkupBuilder builder = new MarkupLoader(MarkupLoadObserverTests.CreateConfiguration());

            XElement fragment = builder.ParseFragment(
                "<ui:Border x:Name=\"b\"/>",
                new Dictionary<string, string> { ["ui"] = MarkupNamespaces.Default });

            Assert.Equal(XName.Get("Border", MarkupNamespaces.Default), fragment.Name);
            Assert.NotNull(fragment.Attribute(MarkupNamespaces.DirectivesNamespace + "Name"));
        }

        [Fact]
        public void ParseFragment_UnknownPrefix_ThrowsMarkupException()
        {
            IMarkupBuilder builder = new MarkupLoader(MarkupLoadObserverTests.CreateConfiguration());

            Assert.Throws<MarkupException>(() => builder.ParseFragment("<ui:Border/>", NoNamespaces));
        }

        [Fact]
        public void BuildFragment_ResolvesStaticResourcesFromLiveAncestors()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked(
                """
                <StackPanel>
                  <StackPanel.Resources>
                    <Style x:Key="Accent" TargetType="Border" Width="42"/>
                  </StackPanel.Resources>
                </StackPanel>
                """);

            var built = (Border)builder.BuildFragment(scope, builder.ParseFragment("<Border Style=\"{StaticResource Accent}\"/>", NoNamespaces), root);

            Assert.Equal(42f, built.Style!.Setters["Width"]);
        }

        [Fact]
        public void BuildFragment_RegistersNamesAndReportsToTheScopesObserver()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<StackPanel/>");
            var observer = (RecordingLoadObserver)scope.Observer;
            observer.Events.Clear();

            object built = builder.BuildFragment(scope, builder.ParseFragment("<Border x:Name=\"added\" Width=\"5\"/>", NoNamespaces), root);

            Assert.Same(built, scope.NameScope!.Find("added"));
            Assert.Equal(["Created:Border@1:2", "Applied:Width"], observer.Events);
        }

        [Fact]
        public void BuildFragment_BindingFollowsTheDataContextOnceInserted()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<StackPanel/>");
            root.DataContext = new Model { Message = "hello" };

            var block = (TextBlock)builder.BuildFragment(scope, builder.ParseFragment("<TextBlock Text=\"{Binding Path=Message}\"/>", NoNamespaces), root);
            ((StackPanel)root).Children.Add(block);

            Assert.Equal("hello", block.Text);
        }

        [Fact]
        public void ApplyAttribute_SetsPropertiesAttachedPropertiesAndNames()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<Grid><Border x:Name=\"cell\"/></Grid>");
            var cell = (Border)scope.NameScope!.Find("cell")!;
            scope.NameScope.Unregister("cell");

            XElement tag = builder.ParseFragment("<Border Width=\"7\" Grid.Row=\"1\" x:Name=\"renamed\"/>", NoNamespaces);
            foreach (XAttribute attribute in tag.Attributes())
                builder.ApplyAttribute(scope, cell, attribute);

            Assert.Equal(7f, cell.Width);
            Assert.Equal(1, Grid.GetRow(cell));
            Assert.Same(cell, scope.NameScope.Find("renamed"));
            Assert.Null(scope.NameScope.Find("cell"));
        }

        [Fact]
        public void ApplyAttribute_BadValue_ThrowsAndLeavesTheValue()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<StackPanel><Border x:Name=\"b\" Width=\"3\"/></StackPanel>");
            var border = (Border)scope.NameScope!.Find("b")!;

            XAttribute bad = builder.ParseFragment("<Border Width=\"abc\"/>", NoNamespaces).Attribute("Width")!;

            Assert.Throws<MarkupException>(() => builder.ApplyAttribute(scope, border, bad));
            Assert.Equal(3f, border.Width);
        }

        [Fact]
        public void Unregister_RemovesOnlyThatName()
        {
            var names = new MarkupNameScope();
            var a = new Border();
            names.Register("a", a);
            names.Register("b", new Border());

            Assert.True(names.Unregister("a"));
            Assert.False(names.Unregister("a"));
            Assert.Null(names.Find("a"));
            Assert.NotNull(names.Find("b"));
        }

        private static (IMarkupBuilder Builder, MarkupLoadScope Scope, UIElement Root) LoadTracked(string markup)
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            var observer = new RecordingLoadObserver(MarkupLoadScopeKind.Document);
            configuration.Types.Markup.LoadObserver = observer;
            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(markup.ReplaceLineEndings("\n"));
            return (loader, observer.Scopes[0], root);
        }

        private sealed class Model : ObservableObject
        {
            private string message = string.Empty;

            public string Message
            {
                get => message;
                set => SetProperty(ref message, value);
            }
        }
    }
}
