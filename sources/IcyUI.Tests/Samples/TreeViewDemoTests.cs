// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Markup;
using Icy.Tests.Rendering;
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
    
        [Fact]
        public void TheLiveTree_ShowsEachRowsOwnTextAfterEdits()
        {
            // Regression: themed rows lost their {Binding Name} when the theme templated them, so pooled rows kept
            // showing the previous item's text and a rename never showed.
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new System.Drawing.Size(1200, 900) })
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration configuration = builder.Build().UseDefaultTheme();
            UIElement root = TreeViewDemo.Build(configuration, "Airfool");
            var canvas = new Canvas(configuration) { IsVisible = true };
            canvas.Add(root);
            canvas.Render();
            TreeView live = root.FindRequiredControl<TreeView>("LiveTree");
            var europe = (TreeViewDemo.DemoNode)live.Rows[0].Item;

            live.SelectedItem = europe;
            root.FindRequiredControl<Button>("AddButton").OnTap();
            canvas.Render();
            AssertRowsShowTheirItems(live);

            live.SelectedItem = europe.Children[0];
            root.FindRequiredControl<Button>("RenameButton").OnTap();
            canvas.Render();
            Assert.Equal("North*", RowText(live, 1));

            root.FindRequiredControl<Button>("RemoveButton").OnTap();
            canvas.Render();
            AssertRowsShowTheirItems(live);
        }

        [Fact]
        public void ArrowsLeaveTheLiveTree_AndComeBackFromTheButtons()
        {
            var input = new FakeInputSystem();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new System.Drawing.Size(1200, 900) })
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration configuration = builder.Build().UseDefaultTheme();
            UIElement root = TreeViewDemo.Build(configuration, "Airfool");
            var canvas = new Canvas(configuration) { IsVisible = true, IsInputEnabled = true };
            canvas.Add(root);
            canvas.Render();
            TreeView live = root.FindRequiredControl<TreeView>("LiveTree");
            Button add = root.FindRequiredControl<Button>("AddButton");
            canvas.Focus(live);

            // Two Downs move inside the tree (rows 0 -> 2); the next one leaves it for the button row below. Which button
            // wins depends on the theme's widths (they're all in the tree's beam), so only "a button" is pinned.
            for (int i = 0; i < live.Rows.Count - 1; i++)
                Assert.True(input.Events.Navigation.RaiseFocusChanging(System.Numerics.Vector2.UnitY).Handled);
            Assert.True(input.Events.Navigation.RaiseFocusChanging(System.Numerics.Vector2.UnitY).Handled);
            Assert.IsType<Button>(canvas.FocusedElement);

            input.Events.Navigation.RaiseFocusChanging(-System.Numerics.Vector2.UnitY);
            Assert.Same(live, canvas.FocusedElement);

            // Activation presses the focused button: Add puts a child under the selected root and expands it.
            live.SelectedItem = live.Rows[0].Item;
            canvas.Focus(add);
            int before = live.Rows.Count;
            input.Events.Navigation.RaiseSelectElement();
            Assert.True(live.Rows.Count > before);
        }

        private static string RowText(TreeView live, int index) =>
            live.List.Realized[index].EnumerateVisualSubtree().OfType<TextBlock>().Single().Text;

        private static void AssertRowsShowTheirItems(TreeView live)
        {
            Assert.NotEmpty(live.List.Realized);
            foreach (int index in live.List.Realized.Keys)
                Assert.Equal(live.Rows[index].Item.ToString(), RowText(live, index));
        }
    }
}
