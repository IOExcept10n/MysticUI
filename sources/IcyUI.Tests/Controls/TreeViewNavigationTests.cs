// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewNavigationTests
    {
        private static readonly Vector2 Up = -Vector2.UnitY;
        private static readonly Vector2 Down = Vector2.UnitY;
        private static readonly Vector2 Left = -Vector2.UnitX;
        private static readonly Vector2 Right = Vector2.UnitX;

        private readonly TreeViewNode a, b, c, d, e;
        private readonly TreeView tree;
        private readonly FakeInputSystem input;

        public TreeViewNavigationTests()
        {
            c = Node("C");
            d = Node("D");
            b = Node("B", c, d);
            e = Node("E");
            a = Expanded("A", b, e);
            tree = CreateTree(a);
            (Canvas canvas, input) = CreateCanvas();
            Host(canvas, tree);
            canvas.Focus(tree);
        }

        private string Current => tree.Rows[tree.List.CurrentIndex].Item.ToString()!;

        [Fact]
        public void Focusing_StartsAtTheSelection_OrTheFirstRow()
        {
            Assert.Equal("A", Current);
        }

        [Fact]
        public void UpAndDown_MoveAndSelect()
        {
            Assert.True(Press(Down));
            Assert.Equal("B", Current);
            Assert.Same(b, tree.SelectedItem);
        }

        [Fact]
        public void UpOnTheFirstRow_LeavesTheTree()
        {
            Assert.False(Press(Up));
            Assert.Equal("A", Current);
        }

        [Fact]
        public void DownOnTheLastRow_LeavesTheTree()
        {
            Press(Down);
            Press(Down);
            Assert.Equal("E", Current);

            Assert.False(Press(Down));
        }

        [Fact]
        public void Right_ExpandsACollapsedBranch_ThenEntersIt()
        {
            Press(Down);

            Assert.True(Press(Right));
            Assert.True(b.IsExpanded);
            Assert.Equal("B", Current);

            Assert.True(Press(Right));
            Assert.Equal("C", Current);
        }

        [Fact]
        public void Right_OnAnExpandedBranch_MovesToItsFirstChild()
        {
            Assert.True(Press(Right));
            Assert.Equal("B", Current);
        }

        [Fact]
        public void Right_OnALeaf_IsNotHandled()
        {
            tree.Expand(b);
            tree.SelectedItem = c;
            ResetFocus();

            // Mid-tree: C has a next row (D), but Right still leaves the tree.
            Assert.False(Press(Right));
            Assert.Equal("C", Current);

            // Last row.
            tree.SelectedItem = e;
            ResetFocus();
            Assert.False(Press(Right));
            Assert.Equal("E", Current);
        }

        [Fact]
        public void Right_OnALeaf_GoesToTheControlLevelWithItsRow()
        {
            // The tree is 400 tall: scored from its whole bounds, the decoy (nearer its centre) would win.
            Button level = PlaceButton(40);
            PlaceButton(190);
            tree.Canvas!.Render();
            tree.SelectedItem = e;
            ResetFocus();

            Assert.True(Press(Right));
            Assert.Same(level, tree.Canvas.FocusedElement);
        }

        [Fact]
        public void Left_GoesToTheParent_ThenCollapses()
        {
            tree.Expand(b);
            tree.SelectedItem = c;
            ResetFocus();
            Assert.Equal("C", Current);

            Press(Left);
            Assert.Equal("B", Current);
            Press(Left);
            Assert.False(b.IsExpanded);
            Press(Left);
            Assert.Equal("A", Current);
        }

        [Fact]
        public void LeftOnAnExpandedRoot_CollapsesIt_ThenLeavesTheTree()
        {
            Assert.True(Press(Left));
            Assert.False(a.IsExpanded);

            Assert.False(Press(Left));
        }

        [Fact]
        public void ANonSelectableRow_BecomesCurrent_WithoutChangingTheSelection()
        {
            b.IsSelectable = false;
            tree.SelectedItem = a;

            Press(Down);

            Assert.Equal("B", Current);
            Assert.Same(a, tree.SelectedItem);
        }

        [Fact]
        public void Enter_TogglesTheCurrentRow()
        {
            Press(Down);

            input.Events.Navigation.RaiseSelectElement();

            Assert.True(b.IsExpanded);
        }

        [Fact]
        public void WithoutFocus_APressFocusesTheTree_WithoutAlsoMovingARow()
        {
            tree.Canvas!.Focus(null);
            Assert.Equal(-1, tree.List.CurrentIndex);

            // Nothing focused: the press goes to the canvas, which focuses the first element in Tab order.
            Assert.True(Press(Down));
            Assert.Same(tree, tree.Canvas.FocusedElement);
            Assert.Equal(0, tree.List.CurrentIndex);
        }

        private bool Press(Vector2 direction) => input.Events.Navigation.RaiseFocusChanging(direction).Handled;

        private Button PlaceButton(int y)
        {
            var button = new Button { Width = 40, Height = 20, Margin = new Thickness(300, y, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            tree.Canvas!.Add(button);
            return button;
        }

        private void ResetFocus()
        {
            tree.Canvas!.Focus(null);
            tree.Canvas.Focus(tree);
        }
    }
}
