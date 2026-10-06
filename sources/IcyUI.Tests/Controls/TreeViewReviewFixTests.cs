// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    /// <summary>Regressions from the whole-branch TreeView review.</summary>
    public class TreeViewReviewFixTests
    {
        [Fact]
        public void ADetachedTree_KeepsChangingWithoutThrowing_AndResyncsOnAttach()
        {
            (Canvas canvas, _) = CreateCanvas();
            TreeViewNode deep = Node("deep");
            TreeViewNode a = Node("A", Node("B", deep));
            TreeView tree = CreateTree(a, Node("Z"));
            Host(canvas, tree);
            canvas.Remove(tree);

            tree.SelectedItem = deep;

            Assert.Same(deep, tree.SelectedItem);
            canvas.Add(tree);
            Assert.Equal(2, tree.List.SelectedIndex);
        }

        [Fact]
        public void RemovingTheSelectionUnderACollapsedVisibleParent_ClearsIt()
        {
            TreeViewNode s = Node("S");
            TreeViewNode a = Expanded("A", s);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = s;
            tree.Collapse(a);

            a.Children.Remove(s);

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void ASelectionRemovedWhileItsParentWasHidden_ClearsWhenThatPartIsShownAgain()
        {
            TreeViewNode s = Node("S");
            TreeViewNode b = Expanded("B", s);
            TreeViewNode a = Expanded("A", b);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = s;
            tree.Collapse(a);

            b.Children.Remove(s);
            tree.Expand(a);

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void ReplacingOneCopyOfASharedItem_KeepsTheOtherCopyExpanded()
        {
            var leaf = new Folder("leaf");
            var shared = new Folder("S", leaf);
            var p1 = new Folder("P1", shared);
            var p2 = new Folder("P2", shared);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { p1, p2 }, ChildrenSelector = x => ((Folder)x).Kids };
            tree.Expand(p1);
            tree.Expand(p2);
            tree.Expand(shared);

            p1.Kids[0] = new Folder("Y");

            Assert.True(tree.IsExpanded(shared));
            Assert.Equal(["P1", "Y", "P2", "S", "leaf"], Visible(tree));
            tree.Expand(shared);
            Assert.Equal(["P1", "Y", "P2", "S", "leaf"], Visible(tree));
        }

        [Fact]
        public void NavigationKeepsWorking_AfterAFocusedTreeIsDetachedAndReattached()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            TreeView tree = CreateTree(Node("A"), Node("B"));
            Host(canvas, tree);
            canvas.Focus(tree);

            canvas.Remove(tree);
            canvas.Add(tree);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY).Handled);
        }

        [Fact]
        public void ExpansionState_DoesNotKeepRemovedDataAlive()
        {
            var roots = new ObservableCollection<Folder>();
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = roots, ChildrenSelector = x => ((Folder)x).Kids };

            WeakReference removed = AddExpandAndRemove(tree, roots);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(removed.IsAlive);
            GC.KeepAlive(tree);
        }

        [Fact]
        public void ACycleRow_DoesNotPickUpChildrenOnALiveChange()
        {
            var loop = new Folder("loop");
            loop.Kids.Add(loop);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { loop }, ChildrenSelector = x => ((Folder)x).Kids };
            tree.Expand(loop);

            loop.Kids.Add(new Folder("Y"));

            Assert.Equal(["loop", "loop", "Y"], Visible(tree));
            Assert.Equal([0, 1, 1], Depths(tree));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference AddExpandAndRemove(TreeView tree, ObservableCollection<Folder> roots)
        {
            var item = new Folder("gone", new Folder("child"));
            roots.Add(item);
            tree.Expand(item);
            roots.Remove(item);
            return new WeakReference(item);
        }

        private sealed class Folder(string name, params Folder[] kids)
        {
            public ObservableCollection<Folder> Kids { get; } = [.. kids];

            public override string ToString() => name;
        }
    }
}
