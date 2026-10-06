// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Markup;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TreeViewNodeTests
    {
        [Fact]
        public void Defaults_AreSelectableAndCollapsed()
        {
            var node = new TreeViewNode("A");

            Assert.True(node.IsSelectable);
            Assert.False(node.IsExpanded);
            Assert.Empty(node.Children);
            Assert.Equal("A", node.ToString());
        }

        [Fact]
        public void Setters_RaisePropertyChanged()
        {
            var node = new TreeViewNode();
            var changed = new List<string?>();
            ((INotifyPropertyChanged)node).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            node.Header = "H";
            node.IsExpanded = true;
            node.IsSelectable = false;
            node.Tag = 5;

            Assert.Equal([nameof(TreeViewNode.Header), nameof(TreeViewNode.IsExpanded), nameof(TreeViewNode.IsSelectable), nameof(TreeViewNode.Tag)], changed);
        }

        [Fact]
        public void ANodeTree_LoadsFromMarkup()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            var root = (TreeViewNode)new MarkupLoader(configuration).LoadObject(
                """
                <TreeViewNode Header="Basics" IsSelectable="False" IsExpanded="True">
                  <TreeViewNode Header="Controls" Tag="controls"/>
                  <TreeViewNode Header="Styles"/>
                </TreeViewNode>
                """);

            Assert.Equal("Basics", root.Header);
            Assert.False(root.IsSelectable);
            Assert.True(root.IsExpanded);
            Assert.Equal(["Controls", "Styles"], root.Children.Select(x => x.ToString()));
            Assert.Equal("controls", root.Children[0].Tag);
        }

        [Fact]
        public void AHierarchicalDataTemplate_LoadsItsChildrenPath()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            var template = (HierarchicalDataTemplate)new MarkupLoader(configuration).LoadObject(
                """<HierarchicalDataTemplate ChildrenPath="Children"><TextBlock/></HierarchicalDataTemplate>""");
            var node = new TreeViewNode("A") { Children = { new TreeViewNode("B") } };

            Assert.Equal("Children", template.ChildrenPath);
            Assert.Same(node.Children, template.GetChildren(node));
            Assert.Null(template.GetChildren(42));
        }
    }
}
