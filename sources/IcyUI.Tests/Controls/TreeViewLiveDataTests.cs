// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewLiveDataTests
    {
        [Fact]
        public void AnAddedChild_LandsAfterItsPrecedingSiblingsSubtree()
        {
            TreeViewNode a = Expanded("A", Expanded("B", Node("B1")), Node("C"));
            TreeView tree = CreateTree(a, Node("Z"));

            a.Children.Insert(1, Node("new"));

            Assert.Equal(["A", "B", "B1", "new", "C", "Z"], Visible(tree));
            Assert.Equal(1, tree.Rows[3].Depth);
        }

        [Fact]
        public void ARemovedChild_TakesItsVisibleSubtree()
        {
            TreeViewNode a = Expanded("A", Expanded("B", Node("B1")), Node("C"));
            TreeView tree = CreateTree(a);

            a.Children.RemoveAt(0);

            Assert.Equal(["A", "C"], Visible(tree));
        }

        [Fact]
        public void ReplaceMoveAndReset_AreFollowed()
        {
            TreeViewNode a = Expanded("A", Node("B"), Expanded("C", Node("C1")), Node("D"));
            TreeView tree = CreateTree(a);

            a.Children.Move(1, 2);
            Assert.Equal(["A", "B", "D", "C", "C1"], Visible(tree));

            a.Children[0] = Node("B2");
            Assert.Equal(["A", "B2", "D", "C", "C1"], Visible(tree));

            a.Children.Clear();
            Assert.Equal(["A"], Visible(tree));
        }

        [Fact]
        public void RootChanges_AreFollowed()
        {
            var roots = new ObservableCollection<TreeViewNode> { Node("A"), Node("C") };
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = roots };

            roots.Insert(1, Node("B"));
            roots.RemoveAt(0);

            Assert.Equal(["B", "C"], Visible(tree));
        }

        [Fact]
        public void InlineItemChanges_AreFollowed()
        {
            TreeView tree = CreateTree(Node("A"));

            tree.Items.Add(Node("B"));

            Assert.Equal(["A", "B"], Visible(tree));
        }

        [Fact]
        public void AChildAddedToACollapsedRow_ShowsItsChevron()
        {
            TreeViewNode a = Node("A");
            TreeView tree = CreateTree(a);
            Layout(tree);
            Assert.False(Row(tree, 0).HasChildren);

            a.Children.Add(Node("B"));

            Assert.True(Row(tree, 0).HasChildren);
            Assert.Equal(["A"], Visible(tree));
        }

        [Fact]
        public void SettingANodesIsExpanded_ExpandsItsRows_AndRaisesTheEvent()
        {
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            int expandedEvents = 0;
            tree.ItemExpanded += (_, _) => expandedEvents++;

            a.IsExpanded = true;

            Assert.Equal(["A", "B"], Visible(tree));
            Assert.Equal(1, expandedEvents);
        }

        [Fact]
        public void RemovingTheSelectedItem_ClearsTheSelection()
        {
            TreeViewNode b = Node("B");
            TreeViewNode a = Expanded("A", b);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = b;

            a.Children.Remove(b);

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void RemovingTheCollapsedAncestorOfTheSelection_ClearsIt()
        {
            TreeViewNode c = Node("C");
            TreeViewNode b = Node("B", c);
            TreeViewNode a = Expanded("A", b);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = c;
            tree.Collapse(b);

            a.Children.Remove(b);

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void CollapsingAndRemoving_Unsubscribe()
        {
            var child = new Folder("child");
            var root = new Folder("root", child);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ((Folder)x).Kids };

            tree.Expand(root);
            Assert.Equal(1, root.Kids.Subscribers);
            Assert.Equal(1, child.Kids.Subscribers);

            tree.Collapse(root);
            Assert.Equal(1, root.Kids.Subscribers);
            Assert.Equal(0, child.Kids.Subscribers);

            tree.ItemsSource = null;
            Assert.Equal(0, root.Kids.Subscribers);
        }

        [Fact]
        public void ADuplicatedItem_IsSubscribedOnce_AndUpdatesEveryRow()
        {
            TreeViewNode shared = Expanded("S");
            TreeView tree = CreateTree(Expanded("A", shared), Expanded("B", shared));

            shared.Children.Add(Node("leaf"));

            Assert.Equal(["A", "S", "leaf", "B", "S", "leaf"], Visible(tree));
        }

        [Fact]
        public void Detaching_Unsubscribes_AndReattaching_ReflectsMissedChanges()
        {
            (Canvas canvas, _) = CreateCanvas();
            var child = new Folder("child");
            var root = new Folder("root", child);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ((Folder)x).Kids };
            tree.Expand(root);
            Host(canvas, tree);

            canvas.Remove(tree);
            Assert.Equal(0, root.Kids.Subscribers);
            root.Kids.Add(new Folder("late"));

            canvas.Add(tree);
            Assert.Equal(1, root.Kids.Subscribers);
            Assert.Equal(["root", "child", "late"], Visible(tree));
        }

        private sealed class Folder(string name, params Folder[] kids)
        {
            public CountingCollection<Folder> Kids { get; } = [.. kids];

            public override string ToString() => name;
        }

        private sealed class CountingCollection<T> : ObservableCollection<T>
        {
            public int Subscribers { get; private set; }

            public override event NotifyCollectionChangedEventHandler? CollectionChanged
            {
                add
                {
                    base.CollectionChanged += value;
                    Subscribers++;
                }

                remove
                {
                    base.CollectionChanged -= value;
                    Subscribers--;
                }
            }
        }
    }
}
