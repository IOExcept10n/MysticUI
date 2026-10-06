// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewSelectionTests
    {
        [Fact]
        public void SelectingAVisibleItem_SelectsItsRow()
        {
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(Expanded("A", b));
            Layout(tree);
            int changes = 0;
            tree.SelectionChanged += (_, _) => changes++;

            tree.SelectedItem = b;

            Assert.Same(b, tree.SelectedItem);
            Assert.Equal(1, tree.List.SelectedIndex);
            Assert.True(Row(tree, 1).IsSelected);
            Assert.Equal(1, changes);
        }

        [Fact]
        public void SelectingAHiddenItem_ExpandsItsAncestors()
        {
            TreeViewNode c = Node("C");
            TreeViewNode a = Node("A", Node("B", c));
            TreeView tree = CreateTree(a);

            tree.SelectedItem = c;

            Assert.Equal(["A", "B", "C"], Visible(tree));
            Assert.Same(c, tree.SelectedItem);
            Assert.Equal(2, tree.List.SelectedIndex);
        }

        [Fact]
        public void SelectingAnUnknownOrNonSelectableItem_ClearsTheSelection()
        {
            TreeViewNode a = Node("A");
            TreeViewNode locked = Node("L");
            locked.IsSelectable = false;
            TreeView tree = CreateTree(a, locked);
            tree.SelectedItem = a;

            tree.SelectedItem = locked;
            Assert.Null(tree.SelectedItem);

            tree.SelectedItem = a;
            tree.SelectedItem = Node("stranger");
            Assert.Null(tree.SelectedItem);
            Assert.Equal(-1, tree.List.SelectedIndex);
        }

        [Fact]
        public void Reveal_ReportsWhetherTheItemWasFound()
        {
            TreeViewNode c = Node("C");
            TreeView tree = CreateTree(Node("A", Node("B", c)));

            Assert.True(tree.Reveal(c));
            Assert.False(tree.Reveal(Node("stranger")));
        }

        [Fact]
        public void Reveal_TerminatesOnACycle()
        {
            var root = new object();
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ReferenceEquals(x, root) ? new[] { root } : null };

            Assert.False(tree.Reveal(new object()));
            Assert.True(tree.Reveal(root));
        }

        [Fact]
        public void Reveal_ScrollsADeepItemIntoView()
        {
            var roots = Enumerable.Range(0, 60).Select(i => Node($"R{i}")).ToArray();
            TreeViewNode deep = Node("deep");
            roots[59].Children.Add(deep);
            TreeView tree = CreateTree(roots);
            Layout(tree);

            tree.Reveal(deep);
            Layout(tree);

            Assert.True(tree.ScrollViewer.VerticalOffset > 0);
            Assert.Contains(60, tree.List.Realized.Keys);
        }

        [Fact]
        public void CollapsingAnAncestor_KeepsTheSelection()
        {
            TreeViewNode b = Node("B");
            TreeViewNode a = Expanded("A", b);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = b;

            tree.Collapse(a);
            Assert.Same(b, tree.SelectedItem);
            Assert.Equal(-1, tree.List.SelectedIndex);

            tree.Expand(a);
            Assert.Equal(1, tree.List.SelectedIndex);
        }

        [Fact]
        public void RowsInsertedAbove_KeepTheSameItemSelected()
        {
            TreeViewNode a = Node("A", Node("A1"), Node("A2"));
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(a, b);
            tree.SelectedItem = b;

            tree.Expand(a);

            Assert.Same(b, tree.SelectedItem);
            Assert.Equal(3, tree.List.SelectedIndex);
        }

        [Fact]
        public void RetargetingItemsSource_ClearsASelectionThatIsGone()
        {
            var kept = new object();
            var lost = new object();
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { kept, lost } };

            tree.SelectedItem = kept;
            tree.ItemsSource = new[] { kept };
            Assert.Same(kept, tree.SelectedItem);

            tree.SelectedItem = kept;
            tree.ItemsSource = new[] { lost };
            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void TappingASelectableRow_SelectsIt()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(Node("A"), b);
            Host(canvas, tree);

            Tap(input, Row(tree, 1));

            Assert.Same(b, tree.SelectedItem);
        }

        [Fact]
        public void TappingANonSelectableBranch_TogglesIt_WithoutSelecting()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            TreeViewNode category = Node("Cat", Node("leaf"));
            category.IsSelectable = false;
            TreeView tree = CreateTree(category);
            Host(canvas, tree);

            Tap(input, Row(tree, 0));

            Assert.True(category.IsExpanded);
            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void TappingANonSelectableLeaf_DoesNothing()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            TreeViewNode leaf = Node("leaf");
            leaf.IsSelectable = false;
            TreeView tree = CreateTree(leaf);
            Host(canvas, tree);

            Tap(input, Row(tree, 0));

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void TappingTheChevron_TogglesWithoutSelecting()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas(themed: true);
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            Host(canvas, tree);

            Tap(input, Row(tree, 0).Expander!);

            Assert.True(a.IsExpanded);
            Assert.Null(tree.SelectedItem);
        }
    }
}
