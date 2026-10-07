// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Markup;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class SampleShellTests
    {
        private readonly Dictionary<string, int> builds = [];
        private readonly Dictionary<string, UIElement> built = [];

        [Fact]
        public void Startup_BuildsAndAttachesOnlyTheFirstDemo()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("B", "Three")]);

            Assert.Equal(new Dictionary<string, int> { ["One"] = 1 }, builds);
            Assert.NotNull(built["One"].Canvas);
            Assert.Equal("One", shell.Tree.SelectedItem!.ToString());
        }

        [Fact]
        public void Sidebar_GroupsDemosUnderNonSelectableExpandedCategories()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("B", "Three")]);

            Assert.Equal(["A", "One", "Two", "B", "Three"], shell.Tree.Rows.Select(r => r.Item.ToString()));
            var category = (TreeViewNode)shell.Tree.Items[0];
            Assert.False(category.IsSelectable);
            Assert.True(category.IsExpanded);
        }

        [Fact]
        public void SelectingADemo_BuildsItOnceAndDetachesThePreviousOne()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two")]);

            shell.Select("Two");

            Assert.Equal(1, builds["Two"]);
            Assert.NotNull(built["Two"].Canvas);
            Assert.Null(built["One"].Canvas);
        }

        [Fact]
        public void SelectingADemoAgain_ShowsTheSameInstanceAtItsScrollOffset()
        {
            var shell = Host([Fake("A", "One", height: 3000), Fake("A", "Two")]);
            var viewer = Assert.IsType<ScrollViewer>(shell.Content.Content);
            viewer.VerticalOffset = 500;

            shell.Select("Two");
            shell.Select("One");

            Assert.Equal(1, builds["One"]);
            Assert.Same(viewer, shell.Content.Content);
            Assert.Same(built["One"], viewer.Content);
            Assert.Equal(500, viewer.VerticalOffset);
        }

        [Fact]
        public void AWideDemo_IsLaidOutWithinTheContentArea_AndScrollsOnlyVertically()
        {
            Border? page = null;
            var wide = new SampleEntry("A", "Wide", (_, _) => page = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = new UIElement { Width = 3000, Height = 50 },
            });
            var shell = Host([wide]);

            var viewer = Assert.IsType<ScrollViewer>(shell.Content.Content);
            Assert.Equal(ScrollMode.Disabled, viewer.HorizontalScrollMode);
            Assert.Equal(ScrollMode.Enabled, viewer.VerticalScrollMode);
            Assert.Equal(shell.Content.ActualBounds.Width, page!.ActualBounds.Width);
        }

        [Fact]
        public void ADemoThatScrollsItself_IsShownUnwrapped()
        {
            var shell = Host([Fake("A", "One", scrollsItself: true)]);

            Assert.Same(built["One"], shell.Content.Content);
        }

        [Fact]
        public void ClickingACategory_KeepsTheShownDemo()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two")]);
            object shown = shell.Content.Content!;

            shell.Tree.SelectedItem = shell.Tree.Items[1];
            shell.Canvas.Render();

            Assert.Same(shown, shell.Content.Content);
            Assert.Equal(1, builds["One"]);
        }

        [Fact]
        public void TheDesignSession_TracksDemosBuiltAfterStartup()
        {
            UIElement? late = null;
            var lateEntry = new SampleEntry("A", "Late", (configuration, _) => late = new MarkupLoader(configuration).Load("<Border Width=\"10\" Height=\"10\"/>", "Late"));
            var shell = Host([Fake("A", "One"), lateEntry]);

            shell.Select("Late");

            Assert.NotNull(DesignDemo.SessionFor(shell.Configuration).FindDocument(late!, out _));
        }

        [Fact]
        public void SwitchingAwayWhileEditing_RemovesTheEditorOverlay()
        {
            SampleEntry editor = SampleCatalog.All.Single(e => e.Name == "Editor");
            var shell = Host([editor, Fake("A", "Other")]);
            Button edit = shell.Root.EnumerateVisualSubtree().OfType<Button>()
                .First(b => b.Content is TextBlock { Text: "Edit this page" });
            edit.Command!.Execute(null);
            Assert.NotEmpty(shell.Canvas.Overlays);

            shell.Select("Other");

            Assert.Empty(shell.Canvas.Overlays);
        }

        [Fact]
        public void TheSidebar_HasFocusOnceAttached()
        {
            var shell = Host([Fake("A", "One")]);

            Assert.Same(shell.Tree, shell.Canvas.FocusedElement);
        }

        [Fact]
        public void PageDown_SkipsCategoriesAndWraps()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two")]);

            Press(shell, Keys.PageDown);
            Assert.Equal("Two", shell.Tree.SelectedItem!.ToString());

            Press(shell, Keys.PageDown);
            Assert.Equal("One", shell.Tree.SelectedItem!.ToString());
        }

        [Fact]
        public void PageUp_GoesBackAndWrapsToTheLastDemo()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two"), Fake("B", "Three")]);

            Press(shell, Keys.PageUp);
            Assert.Equal("Three", shell.Tree.SelectedItem!.ToString());

            Press(shell, Keys.PageUp);
            Assert.Equal("Two", shell.Tree.SelectedItem!.ToString());
        }

        [Fact]
        public void Paging_IntoACollapsedCategory_ExpandsIt()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two")]);
            var b = (TreeViewNode)shell.Tree.Items[1];
            shell.Tree.Collapse(b);

            Press(shell, Keys.PageDown);

            Assert.True(shell.Tree.IsExpanded(b));
            Assert.Equal("Two", shell.Tree.SelectedItem!.ToString());
            Assert.Same(built["Two"], ((ScrollViewer)shell.Content.Content!).Content);
        }

        [Fact]
        public void Paging_WithNoDemoSelected_StartsFromTheEnds()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("A", "Three")]);

            shell.Tree.SelectedItem = null;
            Press(shell, Keys.PageDown);
            Assert.Equal("One", shell.Tree.SelectedItem!.ToString());

            shell.Tree.SelectedItem = null;
            Press(shell, Keys.PageUp);
            Assert.Equal("Three", shell.Tree.SelectedItem!.ToString());
        }

        [Fact]
        public void RightFromASidebarLeaf_EntersTheDemo_AndLeftComesBack()
        {
            Button? second = null;
            var demo = new SampleEntry("A", "Buttons", (_, _) =>
            {
                var panel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left };
                panel.Children.Add(new Button { Content = new TextBlock { Text = "First" } });
                second = new Button { Content = new TextBlock { Text = "Second" } };
                panel.Children.Add(second);
                return panel;
            });
            // "Buttons" is not the last row, so the old Right (walk to the next row) would have stayed in the tree.
            var shell = Host([demo, Fake("A", "Other")]);
            Assert.Same(shell.Tree, shell.Canvas.FocusedElement);

            // The "Buttons" row is the sidebar's second row (under category "A"), level with the second button.
            Assert.True(shell.Input.Events.Navigation.RaiseFocusChanging(System.Numerics.Vector2.UnitX).Handled);
            Assert.Same(second, shell.Canvas.FocusedElement);

            Assert.True(shell.Input.Events.Navigation.RaiseFocusChanging(-System.Numerics.Vector2.UnitX).Handled);
            Assert.Same(shell.Tree, shell.Canvas.FocusedElement);
        }

        [Fact]
        public void SidebarRows_ShowTheirOwnLabels_AfterCollapseAndExpand()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("B", "Three"), Fake("B", "Four")]);
            var a = (TreeViewNode)shell.Tree.Items[0];

            shell.Tree.Collapse(a);
            shell.Canvas.Render();
            AssertRowsShowTheirItems(shell.Tree);

            shell.Tree.Expand(a);
            shell.Canvas.Render();
            AssertRowsShowTheirItems(shell.Tree);
        }

        internal SampleEntry Fake(string category, string name, float height = 100, bool scrollsItself = false) =>
            new(category, name, (_, _) =>
            {
                builds[name] = builds.GetValueOrDefault(name) + 1;
                var element = new Border { Width = 200, Height = height };
                built[name] = element;
                return element;
            }, scrollsItself);

        internal static ShellHost Host(IReadOnlyList<SampleEntry> entries, FakeInputSystem? input = null)
        {
            input ??= new FakeInputSystem();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new System.Drawing.Size(1280, 720) })
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration configuration = builder.Build().UseDefaultTheme();
            UIElement root = SampleShell.Build(configuration, "Airfool", entries);
            var canvas = new Canvas(configuration) { IsVisible = true, IsInputEnabled = true };
            canvas.Add(root);
            canvas.Render();
            return new ShellHost(configuration, canvas, input, root);
        }

        private static void AssertRowsShowTheirItems(TreeView tree)
        {
            Assert.NotEmpty(tree.List.Realized);
            foreach (int index in tree.List.Realized.Keys)
            {
                string text = tree.List.Realized[index].EnumerateVisualSubtree().OfType<TextBlock>().Single().Text;
                Assert.Equal(tree.Rows[index].Item.ToString(), text);
            }
        }

        private static void Press(ShellHost shell, Keys key)
        {
            Assert.True(shell.Input.Events.RaiseGesture(new KeyGesture(key)));
            shell.Canvas.Render();
        }

        internal sealed record ShellHost(IcyConfiguration Configuration, Canvas Canvas, FakeInputSystem Input, UIElement Root)
        {
            public TreeView Tree => (TreeView)((SplitPane)Root).First!;

            public ContentControl Content => (ContentControl)((SplitPane)Root).Second!;

            public void Select(string name)
            {
                Tree.SelectedItem = Tree.Items.Cast<TreeViewNode>().SelectMany(c => c.Children).Single(n => n.ToString() == name);
                Canvas.Render();
            }
        }
    }
}
