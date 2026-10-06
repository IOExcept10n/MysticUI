// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using Icy.UI.Controls;
using Xunit;
using Xunit.Abstractions;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewPoolingTests(ITestOutputHelper output)
    {
        [Fact]
        public void ScrollingThroughDeepAndShallowRows_KeepsEveryRecycledRowCorrect()
        {
            // A deep chain (depths 0..29) followed by 60 shallow roots, half of them with children.
            TreeViewNode chain = Expanded("d0");
            TreeViewNode tip = chain;
            for (int i = 1; i < 30; i++)
            {
                TreeViewNode next = Expanded($"d{i}");
                tip.Children.Add(next);
                tip = next;
            }

            var roots = new List<TreeViewNode> { chain };
            for (int i = 0; i < 60; i++)
                roots.Add(i % 2 == 0 ? Node($"s{i}", Node($"s{i}.child")) : Node($"s{i}"));
            TreeView tree = CreateTree([.. roots]);
            Layout(tree);

            for (float offset = 0; offset <= 90 * 20; offset += 70)
            {
                tree.ScrollViewer.VerticalOffset = offset;
                Layout(tree);
                AssertRealizedRowsMatch(tree);
            }

            for (float offset = 90 * 20; offset >= 0; offset -= 90)
            {
                tree.ScrollViewer.VerticalOffset = offset;
                Layout(tree);
                AssertRealizedRowsMatch(tree);
            }
        }

        [Fact]
        public void CollapsingAndReExpandingWhileScrolled_KeepsRowsCorrect()
        {
            TreeViewNode[] groups = [.. Enumerable.Range(0, 20).Select(g => Expanded($"g{g}", Node($"g{g}.a"), Node($"g{g}.b", Node($"g{g}.b.x")))) ];
            TreeView tree = CreateTree(groups);
            Layout(tree);
            tree.ScrollViewer.VerticalOffset = 300;
            Layout(tree);

            tree.Collapse(groups[5]);
            Layout(tree);
            AssertRealizedRowsMatch(tree);

            tree.Expand(groups[5]);
            tree.Expand(groups[5].Children[1]);
            Layout(tree);
            AssertRealizedRowsMatch(tree);
        }

        [Fact]
        public void RetargetingToASameShapeTree_LeavesNoOldContent()
        {
            TreeView tree = CreateTree(Expanded("A", Node("A1"), Node("A2")), Node("B"));
            Layout(tree);

            TreeViewNode[] fresh = [Expanded("X", Node("X1"), Node("X2")), Node("Y")];
            tree.Items.Clear();
            foreach (TreeViewNode node in fresh)
                tree.Items.Add(node);
            Layout(tree);

            Assert.Equal(["X", "X1", "X2", "Y"], Visible(tree));
            AssertRealizedRowsMatch(tree);
        }

        [Fact]
        public void ChangingTheChildrenSelector_RebuildsTheRows()
        {
            var root = new object();
            var first = new object[] { "one" };
            var second = new object[] { "two", "three" };
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ReferenceEquals(x, root) ? first : null };
            tree.Expand(root);
            Layout(tree);

            tree.ChildrenSelector = x => ReferenceEquals(x, root) ? second : null;
            Layout(tree);

            Assert.Equal(3, tree.Rows.Count);
            AssertRealizedRowsMatch(tree);
        }

        [Fact]
        public void ATenThousandNodeTree_RealizesOnlyTheViewport()
        {
            TreeViewNode root = Node("root");
            for (int g = 0; g < 100; g++)
            {
                TreeViewNode group = Node($"g{g}");
                for (int i = 0; i < 100; i++)
                    group.Children.Add(Node($"g{g}.{i}"));
                root.Children.Add(group);
            }

            TreeView tree = CreateTree(root);
            Layout(tree);

            var watch = Stopwatch.StartNew();
            tree.Expand(root);
            foreach (TreeViewNode group in root.Children)
                tree.Expand(group);
            watch.Stop();
            Layout(tree);
            output.WriteLine($"Expanding 10,100 rows took {watch.Elapsed.TotalMilliseconds:F1} ms");

            Assert.Equal(1 + 100 + 10_000, tree.Rows.Count);
            Assert.InRange(tree.List.Realized.Count, 1, 30);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"Expanding took {watch.Elapsed.TotalMilliseconds:F0} ms; the budget is 1000 ms.");
        }

        private static void AssertRealizedRowsMatch(TreeView tree)
        {
            foreach ((int index, ItemContainer container) in tree.List.Realized)
            {
                FlatRow row = tree.Rows[index];
                var item = (TreeViewItem)container;
                Assert.Equal(row.Depth, item.Depth);
                Assert.Equal(tree.HasChildren(row.Item), item.HasChildren);
                Assert.Equal(tree.IsExpanded(row.Item), item.IsExpanded);
                Assert.Equal(row.Depth * tree.Indent, item.IndentWidth);
                Assert.Same(row.Item, item.Content!.DataContext);
            }
        }
    }
}
