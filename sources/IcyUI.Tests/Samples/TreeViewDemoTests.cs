// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class TreeViewDemoTests
    {
        [Fact]
        public void Build_LoadsTheStaticTree()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            UIElement root = TreeViewDemo.Build(configuration, "Airfool");
            TreeView tree = root.FindRequiredControl<TreeView>("StaticTree");

            Assert.Equal(3, tree.Items.Count);
            Assert.Equal(["Basics", "Controls", "Styles", "Items", "Design tools"], tree.Rows.Select(r => r.Item.ToString()));
        }

        [Fact]
        public void TheLiveButtons_EditTheLiveTree()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            UIElement root = TreeViewDemo.Build(configuration, "Airfool");
            TreeView live = root.FindRequiredControl<TreeView>("LiveTree");
            var first = (TreeViewDemo.DemoNode)live.Rows[0].Item;
            live.SelectedItem = first;

            root.FindRequiredControl<Button>("AddButton").OnTap();
            Assert.Equal(4, first.Children.Count);
            Assert.True(live.IsExpanded(first));

            root.FindRequiredControl<Button>("RevealButton").OnTap();
            Assert.Equal("Street", live.SelectedItem!.ToString());
            Assert.Contains("Street", root.FindRequiredControl<TextBlock>("Status").Text);
        }
    }
}
