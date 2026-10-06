// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewFlatteningTests
    {
        [Fact]
        public void InlineNodes_AreTheRoots()
        {
            TreeView tree = CreateTree(Node("A"), Node("B"));

            Assert.Equal(["A", "B"], Visible(tree));
            Assert.Equal([0, 0], Depths(tree));
        }

        [Fact]
        public void ExpandedNodes_ShowTheirChildren()
        {
            TreeView tree = CreateTree(Expanded("A", Node("B"), Expanded("C", Node("D"))), Node("E"));

            Assert.Equal(["A", "B", "C", "D", "E"], Visible(tree));
            Assert.Equal([0, 1, 1, 2, 0], Depths(tree));
            Assert.Same(tree.Rows[2], tree.Rows[3].Parent);
        }

        [Fact]
        public void Expand_InsertsTheVisibleSubtree_AsOneChange()
        {
            TreeViewNode a = Node("A", Node("B"), Expanded("C", Node("D")));
            TreeView tree = CreateTree(a, Node("E"));
            var events = new List<NotifyCollectionChangedEventArgs>();
            ((INotifyCollectionChanged)tree.Rows).CollectionChanged += (_, e) => events.Add(e);

            tree.Expand(a);

            Assert.Equal(["A", "B", "C", "D", "E"], Visible(tree));
            NotifyCollectionChangedEventArgs change = Assert.Single(events);
            Assert.Equal(NotifyCollectionChangedAction.Add, change.Action);
            Assert.Equal(3, change.NewItems!.Count);
            Assert.True(a.IsExpanded);
        }

        [Fact]
        public void Collapse_HidesTheSubtree_AndReExpandRestoresIt()
        {
            TreeViewNode a = Expanded("A", Expanded("B", Node("C")));
            TreeView tree = CreateTree(a, Node("D"));

            tree.Collapse(a);
            Assert.Equal(["A", "D"], Visible(tree));

            tree.Expand(a);
            Assert.Equal(["A", "B", "C", "D"], Visible(tree));
        }

        [Fact]
        public void ExpandingAnItemThatIsNotVisible_OnlyRecordsTheState()
        {
            var hidden = new object();
            var root = new object();
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ReferenceEquals(x, root) ? new[] { hidden } : null };

            tree.Expand(hidden);

            Assert.True(tree.IsExpanded(hidden));
            Assert.Single(tree.Rows);
        }

        [Fact]
        public void ExpandAndCollapse_RaiseTheirEvents()
        {
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            var raised = new List<string>();
            tree.ItemExpanded += (_, e) => raised.Add("+" + e.Item);
            tree.ItemCollapsed += (_, e) => raised.Add("-" + e.Item);

            tree.Expand(a);
            tree.Expand(a);
            tree.Collapse(a);

            Assert.Equal(["+A", "-A"], raised);
        }

        [Fact]
        public void TheChildrenSelector_WinsOverTheNodesOwnChildren()
        {
            TreeViewNode a = Expanded("A", Node("ignored"));
            TreeView tree = CreateTree(a);

            tree.ChildrenSelector = x => ReferenceEquals(x, a) ? new[] { new TreeViewNode("chosen") } : null;

            Assert.Equal(["A", "chosen"], Visible(tree));
        }

        [Fact]
        public void AHierarchicalTemplate_GivesTheChildren()
        {
            var template = (HierarchicalDataTemplate)LoadDataTemplate(
                """<HierarchicalDataTemplate ChildrenPath="Kids"><Border Height="20"/></HierarchicalDataTemplate>""");
            var leaf = new Folder("leaf");
            var root = new Folder("root", leaf);
            var tree = new TreeView { ItemTemplate = template, ItemsSource = new[] { root } };

            tree.Expand(root);

            Assert.Equal(["root", "leaf"], Visible(tree));
        }

        [Fact]
        public void PlainEnumerableChildren_Work_AndStringsAreNotChildren()
        {
            // A plain array (not observable) holds the children; strings would be IEnumerable<char> but never count.
            object[] numbers = ["2", "3"];
            var tree = new TreeView
            {
                ItemTemplate = RowTemplate(),
                ItemsSource = new object[] { "text", numbers },
                ChildrenSelector = x => x as IEnumerable,
            };

            tree.Expand(numbers);

            Assert.Equal(4, tree.Rows.Count);
            Assert.Equal(["2", "3"], Visible(tree)[2..]);
            Assert.False(tree.HasChildren("text"));
        }

        [Fact]
        public void ACycle_IsShownOnce_AndNeverLoops()
        {
            var loop = new Folder("loop");
            loop.Kids.Add(loop);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { loop }, ChildrenSelector = x => ((Folder)x).Kids };

            tree.Expand(loop);

            Assert.Equal(["loop", "loop"], Visible(tree));
        }

        [Fact]
        public void ADuplicatedItem_ExpandsEverywhere()
        {
            TreeViewNode shared = Node("S", Node("leaf"));
            TreeView tree = CreateTree(Expanded("A", shared), Expanded("B", shared));

            tree.Expand(shared);

            Assert.Equal(["A", "S", "leaf", "B", "S", "leaf"], Visible(tree));
        }

        [Fact]
        public void SelectabilityComesFromThePredicate_ThenTheNode()
        {
            TreeViewNode a = Node("A");
            a.IsSelectable = false;
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(a, b);

            Assert.False(tree.IsSelectable(a));
            Assert.True(tree.IsSelectable(b));

            tree.IsItemSelectable = x => ReferenceEquals(x, a);
            Assert.True(tree.IsSelectable(a));
            Assert.False(tree.IsSelectable(b));
        }

        [Fact]
        public void ItemsSourceAndInlineItems_CannotBothBeUsed()
        {
            TreeView inline = CreateTree(Node("A"));
            Assert.Throws<InvalidOperationException>(() => inline.ItemsSource = new[] { "x" });

            var bound = new TreeView { ItemsSource = new[] { "x" } };
            Assert.Throws<InvalidOperationException>(() => bound.Items.Add(Node("A")));
        }

        [Fact]
        public void RealizedRows_AreStamped()
        {
            TreeView tree = CreateTree(Expanded("A", Node("B", Node("C"))));
            Layout(tree);

            TreeViewItem a = Row(tree, 0), b = Row(tree, 1);
            Assert.Equal((0, true, true), (a.Depth, a.HasChildren, a.IsExpanded));
            Assert.Equal((1, true, false), (b.Depth, b.HasChildren, b.IsExpanded));
            Assert.Equal(16f, b.IndentWidth);

            tree.Indent = 10;
            Assert.Equal(10f, Row(tree, 1).IndentWidth);
        }

        [Fact]
        public void ExpandingAVisibleRow_RestampsItsChevron()
        {
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            Layout(tree);

            tree.Expand(a);

            Assert.True(Row(tree, 0).IsExpanded);
        }

        [Fact]
        public void RowContent_IsBoundToTheItem_NotTheRow()
        {
            TreeViewNode a = Node("A");
            TreeView tree = CreateTree(a);
            Layout(tree);

            Assert.Same(a, Row(tree, 0).Content!.DataContext);
        }

        private sealed class Folder(string name, params Folder[] kids)
        {
            public ObservableCollection<Folder> Kids { get; } = [.. kids];

            public override string ToString() => name;
        }
    }
}
