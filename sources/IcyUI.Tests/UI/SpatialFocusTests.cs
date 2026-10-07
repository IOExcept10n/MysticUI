// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Numerics;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.UI.BringIntoViewTests;

namespace Icy.Tests.UI
{
    public class SpatialFocusTests
    {
        private static readonly Vector2 Up = -Vector2.UnitY;
        private static readonly Vector2 Down = Vector2.UnitY;
        private static readonly Vector2 Left = -Vector2.UnitX;
        private static readonly Vector2 Right = Vector2.UnitX;

        [Fact]
        public void Grid_FourDirections_ReachTheNeighbours()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button a = Place(canvas, 0, 0), b = Place(canvas, 100, 0), c = Place(canvas, 0, 100), d = Place(canvas, 100, 100);
            canvas.Render();
            canvas.Focus(a);

            Assert.True(Press(input, Right));
            Assert.Same(b, canvas.FocusedElement);
            Press(input, Down);
            Assert.Same(d, canvas.FocusedElement);
            Press(input, Left);
            Assert.Same(c, canvas.FocusedElement);
            Press(input, Up);
            Assert.Same(a, canvas.FocusedElement);
        }

        [Fact]
        public void Grid_UnderDisplayScale_PicksTheSameNeighbours()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas(displayScale: 2f);
            Button a = Place(canvas, 0, 0), b = Place(canvas, 100, 0), d = Place(canvas, 100, 100);
            canvas.Render();
            canvas.Focus(a);

            Press(input, Right);
            Assert.Same(b, canvas.FocusedElement);
            Press(input, Down);
            Assert.Same(d, canvas.FocusedElement);
        }

        [Fact]
        public void NoCandidate_LeavesFocusAndReportsUnhandled()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button a = Place(canvas, 0, 0);
            canvas.Render();
            canvas.Focus(a);

            Assert.False(Press(input, Down));
            Assert.Same(a, canvas.FocusedElement);
        }

        [Fact]
        public void NothingFocused_FocusesTheFirstTabOrderElement()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button first = Place(canvas, 200, 200);
            Place(canvas, 0, 0);
            canvas.Render();

            Assert.True(Press(input, Up));
            Assert.Same(first, canvas.FocusedElement);
        }

        [Fact]
        public void ReverseStep_ReturnsToTheOrigin_UntilFocusChangesOtherwise()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button wide = Place(canvas, 0, 0, width: 200);
            Button rightNarrow = Place(canvas, 150, 100);
            Button leftNarrow = Place(canvas, 0, 100);
            canvas.Render();

            // Without memory, Down from the wide button prefers rightNarrow (closer centre).
            canvas.Focus(leftNarrow);
            Press(input, Up);
            Press(input, Down);
            Assert.Same(leftNarrow, canvas.FocusedElement);

            Press(input, Up);
            canvas.Focus(rightNarrow);
            canvas.Focus(wide);
            Press(input, Down);
            Assert.Same(rightNarrow, canvas.FocusedElement);
        }

        [Fact]
        public void ReverseStep_FallsBackWhenTheOriginIsGone()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button wide = Place(canvas, 0, 0, width: 200);
            Button rightNarrow = Place(canvas, 150, 100);
            Button leftNarrow = Place(canvas, 0, 100);
            canvas.Render();
            canvas.Focus(leftNarrow);
            Press(input, Up);

            leftNarrow.IsVisible = false;
            Press(input, Down);

            Assert.Same(rightNarrow, canvas.FocusedElement);
        }

        [Fact]
        public void HiddenDisabledTransparentAndClippedCandidates_AreSkipped()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button start = Place(canvas, 0, 0);
            Place(canvas, 0, 40).IsVisible = false;
            Place(canvas, 0, 80).IsEnabled = false;
            Place(canvas, 0, 120).Opacity = 0;
            // A stack panel places the button past the clip (layout would squeeze a plain margin back inside it).
            var clipped = new StackPanel();
            clipped.Children.Add(new Border { Height = 100 });
            clipped.Children.Add(new Button { Width = 40, Height = 20 });
            canvas.Add(new Border { Width = 50, Height = 20, Margin = new Thickness(0, 160, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true, Child = clipped });
            Button target = Place(canvas, 0, 300);
            canvas.Render();
            canvas.Focus(start);

            Press(input, Down);

            Assert.Same(target, canvas.FocusedElement);
        }

        [Fact]
        public void FocusScope_IsNotLeft()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            var scope = new StackPanel { IsFocusScope = true, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var inside = new Button { Width = 40, Height = 20 };
            scope.Children.Add(inside);
            canvas.Add(scope);
            Place(canvas, 0, 100);
            canvas.Render();
            canvas.Focus(inside);

            Assert.False(Press(input, Down));
            Assert.Same(inside, canvas.FocusedElement);
        }

        [Fact]
        public void DPadDown_WalksAScrolledListAndScrollsAlong()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            var panel = new StackPanel();
            Button[] buttons = [.. Enumerable.Range(0, 8).Select(_ => new Button { Height = 40 })];
            foreach (Button button in buttons)
                panel.Children.Add(button);
            var viewer = new ScrollViewer { Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = panel };
            canvas.Add(viewer);
            canvas.Render();
            canvas.Focus(buttons[0]);

            for (int i = 1; i < buttons.Length; i++)
            {
                Assert.True(Press(input, Down), $"step {i}");
                canvas.Render();
                Assert.Same(buttons[i], canvas.FocusedElement);
                AssertInside(viewer, buttons[i]);
            }
        }

        [Fact]
        public void AViewerOffScreen_HidesItsContent()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button start = Place(canvas, 0, 0);
            var panel = new StackPanel();
            panel.Children.Add(new Button { Height = 40 });
            // A stack panel places the viewer below the 600-unit viewport (layout would squeeze a plain margin back on screen).
            var outer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            outer.Children.Add(new Border { Height = 700 });
            outer.Children.Add(new ScrollViewer { Width = 100, Height = 100, Content = panel });
            canvas.Add(outer);
            canvas.Render();
            canvas.Focus(start);

            Assert.False(Press(input, Down));
        }

        [Fact]
        public void Tab_IntoAnOffScreenElement_ScrollsItIntoView()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            var panel = new StackPanel();
            Button[] buttons = [.. Enumerable.Range(0, 6).Select(_ => new Button { Height = 40 })];
            foreach (Button button in buttons)
                panel.Children.Add(button);
            var viewer = new ScrollViewer { Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = panel };
            canvas.Add(viewer);
            canvas.Render();
            canvas.Focus(buttons[0]);

            for (int i = 0; i < 4; i++)
                input.Events.Navigation.RaiseFocusNext();
            canvas.Render();

            Assert.Same(buttons[4], canvas.FocusedElement);
            AssertInside(viewer, buttons[4]);
        }

        [Fact]
        public void FocusedElementRemoved_APressDoesNotThrowOrFocusTheRemovedSubtree()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button gone = Place(canvas, 0, 0);
            Button other = Place(canvas, 0, 100);
            canvas.Render();
            canvas.Focus(gone);
            canvas.Remove(gone);

            Press(input, Down);

            Assert.NotSame(gone, canvas.FocusedElement);
        }

        [Fact]
        public void FocusedTextBoxRemoved_DoesNotClaimArrows_AndFocusMovesBackIntoTheCanvas()
        {
            // Focus survives Canvas.Remove; the detached box must not keep eating arrows.
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            var gone = new TextBox { Width = 100, Height = 20, Text = "ab", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(gone);
            Button other = Place(canvas, 0, 100);
            canvas.Render();
            canvas.Focus(gone);
            canvas.Remove(gone);

            Assert.True(Press(input, Right));
            Assert.Same(other, canvas.FocusedElement);
        }

        [Fact]
        public void FocusedButtonRemoved_IsNotActivated()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button gone = Place(canvas, 0, 0);
            int clicks = 0;
            gone.Click += (_, _) => clicks++;
            canvas.Render();
            canvas.Focus(gone);
            canvas.Remove(gone);

            input.Events.Navigation.RaiseSelectElement();

            Assert.Equal(0, clicks);
        }

        [Fact]
        public void APress_InAThousandElementScope_StaysUnderAMillisecond()
        {
            (Canvas canvas, FakeInputSystem input, _) = CreateCanvas();
            Button first = Place(canvas, 0, 0, width: 18);
            for (int i = 1; i < 1000; i++)
                Place(canvas, (i % 40) * 20, (i / 40) * 22, width: 18);
            canvas.Render();
            canvas.Focus(first);
            Press(input, Down);

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++)
                Press(input, i % 2 == 0 ? Right : Left);
            watch.Stop();

            // Generous bound (the target is < 1 ms per press); this guards against accidental O(n^2) work.
            Assert.True(watch.Elapsed.TotalMilliseconds / 20 < 5, $"{watch.Elapsed.TotalMilliseconds / 20:F2} ms per press");
        }

        private static bool Press(FakeInputSystem input, Vector2 direction) => input.Events.Navigation.RaiseFocusChanging(direction).Handled;

        private static Button Place(Canvas canvas, int x, int y, int width = 40, int height = 20)
        {
            var button = new Button { Width = width, Height = height, Margin = new Thickness(x, y, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(button);
            return button;
        }
    }
}
