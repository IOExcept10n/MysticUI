# Spatial Focus Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Arrow keys, D-pad and left stick move focus spatially between controls; Enter/A activates the focused control;
controls that use arrows themselves get them first; keyboard/pad focus is always scrolled into view.

**Architecture:** Canvas becomes the only consumer of `INavigationEvents.FocusChanging`/`SelectElement`. It routes each
press through the focused element and its ancestors via new `UIElement.OnNavigate`/`OnActivate` virtuals (like
`OnScroll`), and runs a spatial move (pure scoring in `SpatialNavigation`, candidates/clipping in Canvas) when nothing
claims it. `NavigationEvents` gains a held-direction state machine (repeat, stick dead zone, Y flip).

**Tech Stack:** C# / .NET 10, xUnit, IcyUI core (`sources/IcyUI`), tests in `sources/IcyUI.Tests`.

**Spec:** `docs/superpowers/specs/2026-10-07-spatial-navigation-design.md`

## Global Constraints

- All code in core `IcyUI`; no engine project changes (MonoGame, Stride, FNA untouched).
- Every public/protected API gets complete XML docs (`<see cref>`, `<see langword>`, `<list>`, `<para>`).
- Block-scoped `namespace X { }`, the two-line copyright header on every new file, StyleCop clean (no new warnings in
  touched library files).
- Direction values passed to `OnNavigate` are one of the four unit vectors, UI space (+Y down).
- Spatial tolerance: 4 layout units, scaled by `Canvas.EffectiveScale`. Score: `major + 2 * minor`.
- New `NavigationEvents` defaults: `MinimalFocusChangeDistance = 0.5f`, `RepeatStartDelay = 0.4 s`,
  `RepeatDelay = 0.1 s`; stick release threshold `0.7 *` the dead zone.
- Commit after each task on `platform-independent`, ending messages with
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Build/test commands (run from `sources/`):
  - `dotnet build "IcyUI.sln"`
  - `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~<Name>"`
  - `dotnet test` prints "Passed!" even if the host crashes: always check the `Total:` count.

## Review Focus

1. **Canvas scale ≠ 1** (`FakeRenderContext.DisplayScale = 2`): spatial picks must be unchanged and BringIntoView must
   set offsets in layout units, not pixels. Tests: Task 2 Step 1 (`BringIntoView_UnderDisplayScale…`), Task 5 Step 1
   (`Grid_UnderDisplayScale…`).
2. **Focused element removed from the canvas** (focus survives `Canvas.Remove`): a press must not throw and must not
   focus anything inside the removed subtree. Test: Task 5 Step 1 (`FocusedElementRemoved…`).
3. **Reverse-step target gone** (removed or hidden after the move): the opposite press falls back to the normal search.
   Test: Task 5 Step 1 (`ReverseStep_FallsBackWhenTheOriginIsGone`).
4. **Overlapping sources**: holding an arrow, pressing a D-pad direction, then releasing the arrow must not stop the
   D-pad repeat. Test: Task 6 Step 1 (`ReleasingAnOlderSource_DoesNotStopTheNewerHeldDirection`).
5. **ComboBox text box**: Left/Right with the caret mid-text edit the caret and keep focus; Down while closed leaves the
   ComboBox. Test: Task 4 Step 1 (`ComboBoxTextBox_…`).

## Plan-time revisions to the spec

- `Selector.HookFocusGate` **stays**: it still gates the `CloseModal` subscription, which is out of scope. Only the
  `FocusChanging`/`SelectElement` subscriptions go.
- The spec's "sample shell sidebar" behavioural test: the shell doesn't exist yet. Task 5 covers the same behaviour with
  a ScrollViewer of buttons (`DPadDown_WalksAScrolledListAndScrollsAlong`).
- ScrollViewer reveal is an internal virtual hook on `UIElement` (`RevealScreenRectangle`) overridden by `ScrollViewer`,
  so `UIElement` doesn't reference a control type.

## File map

| File | Responsibility |
|---|---|
| `sources/IcyUI/UI/SpatialNavigation.cs` (new) | Pure geometry: `Snap`, `FindBest`. |
| `sources/IcyUI/UI/UIElement.cs` | `OnNavigate`, `OnActivate`, `GetScreenBounds`, `BringIntoView`, `RevealScreenRectangle`. |
| `sources/IcyUI/UI/Controls/ScrollViewer.cs` | `RevealScreenRectangle` override, `GetViewportScreenBounds`. |
| `sources/IcyUI/UI/Canvas.cs` | Becomes `partial`; subscribes routing; `MoveFocus(bool)` uses shared helpers; `Focus` clears reverse step. |
| `sources/IcyUI/UI/Canvas.Navigation.cs` (new) | Routing handlers, `MoveFocus(Vector2)`, candidates, clipping, reverse step. |
| `sources/IcyUI/UI/Controls/Button.cs` | `OnActivate`. |
| `sources/IcyUI/UI/Controls/Selector.cs`, `ComboBox.cs` | Migrate to `OnNavigate`/`OnActivate`. |
| `sources/IcyUI/UI/Controls/TreeView.cs` | Migrate to `OnNavigate`/`OnActivate`; drop subscriptions. |
| `sources/IcyUI/UI/Controls/TextBox.cs` | Left/Right caret via `OnNavigate`. |
| `sources/IcyUI/Input/Events/NavigationEvents.cs`, `INavigationEvents.cs` | Held-direction state machine, defaults, docs. |
| `sources/IcyUI.Tests/Input/FakeInputSystem.cs` | Settable keyboard modifiers, `FakeGamepadInput`, settable `Gamepad`. |
| Tests (new) | `UI/SpatialNavigationTests.cs`, `UI/BringIntoViewTests.cs`, `UI/NavigationRoutingTests.cs`, `UI/SpatialFocusTests.cs`, `Controls/TextBoxNavigationTests.cs`; extended `Input/NavigationEventsTests.cs`, `Samples/TreeViewDemoTests.cs`. |

---

### Task 1: Spatial scoring geometry

**Files:**
- Create: `sources/IcyUI/UI/SpatialNavigation.cs`
- Test: `sources/IcyUI.Tests/UI/SpatialNavigationTests.cs`

**Interfaces:**
- Produces: `internal static class Icy.UI.SpatialNavigation` with
  `public static Vector2 Snap(Vector2 direction)` and
  `public static int FindBest(Rectangle from, Vector2 direction, IReadOnlyList<Rectangle> candidates, float tolerance)`
  (returns the candidate index, `-1` for none; `direction` must already be snapped).

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class SpatialNavigationTests
    {
        private static readonly Vector2 Up = -Vector2.UnitY;
        private static readonly Vector2 Down = Vector2.UnitY;
        private static readonly Vector2 Left = -Vector2.UnitX;
        private static readonly Vector2 Right = Vector2.UnitX;

        [Theory]
        [InlineData(0.9f, 0.5f, 1, 0)]
        [InlineData(0.2f, -0.8f, 0, -1)]
        [InlineData(1f, 1f, 1, 0)]
        [InlineData(-0.3f, 0.1f, -1, 0)]
        [InlineData(0f, 0f, 0, 0)]
        public void Snap_PicksTheDominantAxis_DiagonalsGoHorizontal(float x, float y, float ex, float ey)
        {
            Assert.Equal(new Vector2(ex, ey), SpatialNavigation.Snap(new Vector2(x, y)));
        }

        [Fact]
        public void FindBest_InAGrid_PicksTheAdjacentCellInEachDirection()
        {
            // 3x3 grid of 10x10 cells, 20 apart; index = row * 3 + column.
            var cells = new List<Rectangle>();
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    cells.Add(new Rectangle(column * 20, row * 20, 10, 10));
            }

            Rectangle center = cells[4];
            List<Rectangle> others = [.. cells.Where(c => c != center)];
            Assert.Equal(cells[1], others[SpatialNavigation.FindBest(center, Up, others, 0)]);
            Assert.Equal(cells[7], others[SpatialNavigation.FindBest(center, Down, others, 0)]);
            Assert.Equal(cells[3], others[SpatialNavigation.FindBest(center, Left, others, 0)]);
            Assert.Equal(cells[5], others[SpatialNavigation.FindBest(center, Right, others, 0)]);
        }

        [Fact]
        public void FindBest_ACandidateInTheBeam_BeatsACloserDiagonalOne()
        {
            var from = new Rectangle(0, 0, 100, 20);
            var diagonal = new Rectangle(110, 25, 20, 10);
            var below = new Rectangle(0, 80, 100, 20);

            Assert.Equal(1, SpatialNavigation.FindBest(from, Down, [diagonal, below], 0));
        }

        [Fact]
        public void FindBest_ToleratesASmallOverlap_ButNotALargeOne()
        {
            var from = new Rectangle(0, 0, 50, 20);

            Assert.Equal(0, SpatialNavigation.FindBest(from, Down, [new Rectangle(0, 17, 50, 20)], 4));
            Assert.Equal(-1, SpatialNavigation.FindBest(from, Down, [new Rectangle(0, 15, 50, 20)], 4));
        }

        [Fact]
        public void FindBest_OnAFullTie_KeepsTheEarlierCandidate()
        {
            var from = new Rectangle(40, 0, 20, 20);
            var leftBelow = new Rectangle(0, 50, 20, 20);
            var rightBelow = new Rectangle(80, 50, 20, 20);

            Assert.Equal(0, SpatialNavigation.FindBest(from, Down, [leftBelow, rightBelow], 0));
            Assert.Equal(0, SpatialNavigation.FindBest(from, Down, [rightBelow, leftBelow], 0));
        }

        [Fact]
        public void FindBest_WithNothingAhead_ReturnsMinusOne()
        {
            var from = new Rectangle(0, 100, 20, 20);

            Assert.Equal(-1, SpatialNavigation.FindBest(from, Down, [new Rectangle(0, 0, 20, 20)], 0));
            Assert.Equal(-1, SpatialNavigation.FindBest(from, Down, [], 0));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SpatialNavigationTests"`
Expected: build error, `SpatialNavigation` does not exist.

- [ ] **Step 3: Implement**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.UI
{
    /// <summary>
    /// The geometry of a directional focus move: picks the rectangle a press in one direction goes to.
    /// </summary>
    /// <remarks>
    /// Pure and screen-space. <see cref="Canvas"/> collects the candidates and their rectangles; this class only scores
    /// them (see the spatial navigation design spec).
    /// </remarks>
    internal static class SpatialNavigation
    {
        /// <summary>Snaps <paramref name="direction"/> to its dominant axis.</summary>
        /// <param name="direction">Any direction, UI space (+Y down).</param>
        /// <returns>
        /// One of the four unit vectors, or <see cref="Vector2.Zero"/> for a zero direction. An exact diagonal snaps
        /// horizontally.
        /// </returns>
        public static Vector2 Snap(Vector2 direction)
        {
            if (direction == Vector2.Zero)
                return Vector2.Zero;

            return MathF.Abs(direction.X) >= MathF.Abs(direction.Y)
                ? new Vector2(MathF.Sign(direction.X), 0)
                : new Vector2(0, MathF.Sign(direction.Y));
        }

        /// <summary>Finds the best candidate for a move from <paramref name="from"/> in <paramref name="direction"/>.</summary>
        /// <param name="from">The focused element's screen rectangle.</param>
        /// <param name="direction">A snapped direction (see <see cref="Snap(Vector2)"/>).</param>
        /// <param name="candidates">The candidates' screen rectangles, in Tab order.</param>
        /// <param name="tolerance">How far, in screen units, a candidate may overlap <paramref name="from"/> and still count as ahead.</param>
        /// <returns>The winning index, or <c>-1</c> when no candidate is ahead.</returns>
        /// <remarks>
        /// <list type="number">
        /// <item><description>A candidate must be ahead: its near edge at or past the far edge of <paramref name="from"/>, minus <paramref name="tolerance"/>.</description></item>
        /// <item><description>Candidates overlapping <paramref name="from"/> on the cross axis (in its beam) beat all others.</description></item>
        /// <item><description>Within a group, the lowest <c>major + 2 * minor</c> wins: the edge gap along the axis plus twice the cross-axis gap.</description></item>
        /// <item><description>Ties go to the closer centre, then to the earlier candidate.</description></item>
        /// </list>
        /// </remarks>
        public static int FindBest(Rectangle from, Vector2 direction, IReadOnlyList<Rectangle> candidates, float tolerance)
        {
            int best = -1;
            bool bestInBeam = false;
            float bestScore = 0;
            float bestCenter = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                Rectangle candidate = candidates[i];
                float major = MajorGap(from, candidate, direction);
                if (major < -tolerance)
                    continue;

                (float minor, bool inBeam) = CrossGap(from, candidate, direction);
                float score = Math.Max(0, major) + (2 * minor);
                float center = Vector2.DistanceSquared(Center(from), Center(candidate));
                if (best < 0 || IsBetter(inBeam, score, center, bestInBeam, bestScore, bestCenter))
                {
                    best = i;
                    bestInBeam = inBeam;
                    bestScore = score;
                    bestCenter = center;
                }
            }

            return best;
        }

        private static bool IsBetter(bool inBeam, float score, float center, bool bestInBeam, float bestScore, float bestCenter)
        {
            if (inBeam != bestInBeam)
                return inBeam;
            if (score != bestScore)
                return score < bestScore;
            return center < bestCenter;
        }

        private static float MajorGap(Rectangle from, Rectangle candidate, Vector2 direction) =>
            direction.X > 0 ? candidate.Left - from.Right
            : direction.X < 0 ? from.Left - candidate.Right
            : direction.Y > 0 ? candidate.Top - from.Bottom
            : from.Top - candidate.Bottom;

        private static (float Gap, bool InBeam) CrossGap(Rectangle from, Rectangle candidate, Vector2 direction)
        {
            (int fromStart, int fromEnd, int start, int end) = direction.X != 0
                ? (from.Top, from.Bottom, candidate.Top, candidate.Bottom)
                : (from.Left, from.Right, candidate.Left, candidate.Right);
            if (start < fromEnd && fromStart < end)
                return (0, true);
            return (start >= fromEnd ? start - fromEnd : fromStart - end, false);
        }

        private static Vector2 Center(Rectangle rectangle) => new(rectangle.X + (rectangle.Width / 2f), rectangle.Y + (rectangle.Height / 2f));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SpatialNavigationTests"`
Expected: all pass (10 tests).

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/SpatialNavigation.cs sources/IcyUI.Tests/UI/SpatialNavigationTests.cs
git commit -m "Add the scoring geometry for spatial focus navigation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Screen bounds and `BringIntoView`

**Files:**
- Modify: `sources/IcyUI/UI/UIElement.cs` (next to `PointToScreen`, ~line 1765)
- Modify: `sources/IcyUI/UI/Controls/ScrollViewer.cs`
- Test: `sources/IcyUI.Tests/UI/BringIntoViewTests.cs`

**Interfaces:**
- Produces:
  - `internal Rectangle UIElement.GetScreenBounds()`: axis-aligned screen rectangle (empty when detached).
  - `public void UIElement.BringIntoView()`.
  - `internal virtual Point UIElement.RevealScreenRectangle(Rectangle target)`: default `Point.Empty`; returns the
    screen shift it applied to content.
  - `internal Rectangle ScrollViewer.GetViewportScreenBounds()`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.UI
{
    public class BringIntoViewTests
    {
        [Fact]
        public void BringIntoView_BelowTheViewport_AlignsTheBottomEdge()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100);

            items[4].BringIntoView();
            canvas.Render();

            Assert.Equal(100, viewer.VerticalOffset);
            AssertInside(viewer, items[4]);
        }

        [Fact]
        public void BringIntoView_AboveTheViewport_AlignsTheTopEdge()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100);
            viewer.VerticalOffset = 200;
            canvas.Render();

            items[1].BringIntoView();
            canvas.Render();

            Assert.Equal(40, viewer.VerticalOffset);
            AssertInside(viewer, items[1]);
        }

        [Fact]
        public void BringIntoView_AlreadyVisible_DoesNotScroll()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100);

            items[1].BringIntoView();

            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void BringIntoView_LargerThanTheViewport_AlignsTheLeadingEdge()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 30);

            items[2].BringIntoView();

            Assert.Equal(80, viewer.VerticalOffset);
        }

        [Fact]
        public void BringIntoView_Horizontally_AlignsTheRightEdge()
        {
            (Canvas canvas, _, _) = CreateCanvas();
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            Border[] items = [.. Enumerable.Range(0, 10).Select(_ => new Border { Width = 40, Height = 20 })];
            foreach (Border item in items)
                panel.Children.Add(item);
            var viewer = new ScrollViewer { Width = 100, Height = 50, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = panel };
            canvas.Add(viewer);
            canvas.Render();

            items[4].BringIntoView();

            Assert.Equal(100, viewer.HorizontalOffset);
        }

        [Fact]
        public void BringIntoView_NestedViewers_ScrollsBoth()
        {
            (Canvas canvas, _, _) = CreateCanvas();
            var inner = new StackPanel();
            Border[] items = [.. Enumerable.Range(0, 10).Select(_ => new Border { Height = 40 })];
            foreach (Border item in items)
                inner.Children.Add(item);
            var innerViewer = new ScrollViewer { Height = 100, Content = inner };
            var outerPanel = new StackPanel();
            outerPanel.Children.Add(new Border { Height = 150 });
            outerPanel.Children.Add(innerViewer);
            var outerViewer = new ScrollViewer { Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = outerPanel };
            canvas.Add(outerViewer);
            canvas.Render();

            items[4].BringIntoView();
            canvas.Render();

            Assert.Equal(100, innerViewer.VerticalOffset);
            Assert.Equal(150, outerViewer.VerticalOffset);
            AssertInside(outerViewer, items[4]);
        }

        [Fact]
        public void BringIntoView_ARealizedTreeRow_ScrollsTheTree()
        {
            (Canvas canvas, _) = Icy.Tests.Controls.TreeViewTestKit.CreateCanvas();
            TreeView tree = CreateTree([.. Enumerable.Range(0, 30).Select(i => Node($"n{i}"))]);
            tree.Width = 200;
            tree.Height = 100;
            tree.HorizontalAlignment = HorizontalAlignment.Left;
            tree.VerticalAlignment = VerticalAlignment.Top;
            canvas.Add(tree);
            canvas.Render();

            TreeViewItem row = Row(tree, 6);
            row.BringIntoView();
            canvas.Render();

            Assert.True(tree.ScrollViewer.VerticalOffset > 0);
            AssertInside(tree.ScrollViewer, Row(tree, 6));
        }

        [Fact]
        public void BringIntoView_UnderDisplayScale_SetsOffsetsInLayoutUnits()
        {
            (Canvas canvas, ScrollViewer viewer, Border[] items) = CreateList(viewportHeight: 100, displayScale: 2f);

            items[4].BringIntoView();

            Assert.Equal(100, viewer.VerticalOffset);
        }

        internal static (Canvas Canvas, ScrollViewer Viewer, Border[] Items) CreateList(int viewportHeight, float displayScale = 1f)
        {
            (Canvas canvas, _, _) = CreateCanvas(displayScale);
            var panel = new StackPanel();
            Border[] items = [.. Enumerable.Range(0, 10).Select(_ => new Border { Height = 40 })];
            foreach (Border item in items)
                panel.Children.Add(item);
            var viewer = new ScrollViewer { Width = 100, Height = viewportHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = panel };
            canvas.Add(viewer);
            canvas.Render();
            return (canvas, viewer, items);
        }

        internal static (Canvas Canvas, FakeInputSystem Input, FakeRenderContext Context) CreateCanvas(float displayScale = 1f)
        {
            var input = new FakeInputSystem();
            var context = new FakeRenderContext { ViewportSize = new Size(800, 600), DisplayScale = displayScale };
            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), context, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input, context);
        }

        internal static void AssertInside(ScrollViewer viewer, UIElement element)
        {
            Rectangle viewport = viewer.GetViewportScreenBounds();
            Rectangle bounds = element.GetScreenBounds();
            Assert.True(viewport.Contains(bounds), $"{bounds} is not inside the viewport {viewport}.");
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~BringIntoViewTests"`
Expected: build errors (`BringIntoView`, `GetScreenBounds`, `GetViewportScreenBounds` missing).

- [ ] **Step 3: Implement in `UIElement.cs`**, after `PointToSurface`:

```csharp
        /// <summary>
        /// Scrolls every enclosing scrollable ancestor (such as a <see cref="Controls.ScrollViewer"/>) just enough to
        /// show this element, innermost first.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>An element above or left of a viewport is aligned with its leading edge; one below or right, with its trailing edge.</description></item>
        /// <item><description>An element larger than a viewport is aligned with its leading edge.</description></item>
        /// <item><description>An element already fully visible doesn't scroll anything.</description></item>
        /// <item><description>Does nothing while this element isn't attached to a <see cref="UI.Canvas"/>.</description></item>
        /// </list>
        /// <para>
        /// <see cref="UI.Canvas"/> calls this after moving focus with the keyboard or a gamepad (Tab and spatial moves),
        /// but not after a tap or a programmatic <see cref="UI.Canvas.Focus(UIElement?)"/>.
        /// </para>
        /// </remarks>
        public void BringIntoView()
        {
            if (Canvas == null)
                return;

            // The tracked rectangle follows each applied scroll, so outer viewers see where the element will be
            // without a layout pass in between.
            Rectangle target = GetScreenBounds();
            for (UIElement? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                Point shift = ancestor.RevealScreenRectangle(target);
                target.Offset(-shift.X, -shift.Y);
            }
        }

        /// <summary>
        /// Gets this element's axis-aligned bounds in screen space (the same space as pointer positions).
        /// </summary>
        /// <returns>The bounds, or an empty rectangle while this element isn't attached to a <see cref="UI.Canvas"/>.</returns>
        internal Rectangle GetScreenBounds()
        {
            if (Canvas == null)
                return Rectangle.Empty;

            Size size = ActualBounds.Size;
            Point a = PointToScreen(Vector2.Zero);
            Point b = PointToScreen(new Vector2(size.Width, 0));
            Point c = PointToScreen(new Vector2(0, size.Height));
            Point d = PointToScreen(new Vector2(size.Width, size.Height));
            int left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            int top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            int right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            int bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// Scrolls this element's content so that <paramref name="target"/> becomes visible, if this element scrolls.
        /// </summary>
        /// <param name="target">The screen rectangle to reveal.</param>
        /// <returns>How far, in screen units, the content moved (positive when it moved up/left). Zero by default.</returns>
        internal virtual Point RevealScreenRectangle(Rectangle target) => Point.Empty;
```

- [ ] **Step 4: Implement in `ScrollViewer.cs`** (add `using System.Drawing;` / `using System.Numerics;` if missing):

```csharp
        /// <inheritdoc/>
        internal override Point RevealScreenRectangle(Rectangle target)
        {
            Rectangle viewport = GetViewportScreenBounds();
            if (viewport.Width <= 0 || viewport.Height <= 0 || ContentBounds.Width <= 0 || ContentBounds.Height <= 0)
                return Point.Empty;

            // Screen units per layout unit: the canvas scale and any LayoutScale above this viewer.
            float scaleX = viewport.Width / (float)ContentBounds.Width;
            float scaleY = viewport.Height / (float)ContentBounds.Height;
            float beforeX = HorizontalOffset;
            float beforeY = VerticalOffset;

            int dx = RevealDelta(target.Left, target.Right, viewport.Left, viewport.Right);
            int dy = RevealDelta(target.Top, target.Bottom, viewport.Top, viewport.Bottom);
            if (dx != 0)
                HorizontalOffset += dx / scaleX;
            if (dy != 0)
                VerticalOffset += dy / scaleY;

            return new Point((int)MathF.Round((HorizontalOffset - beforeX) * scaleX), (int)MathF.Round((VerticalOffset - beforeY) * scaleY));
        }

        /// <summary>Gets the visible content area (the viewport) in screen space.</summary>
        /// <returns>The viewport's screen rectangle, or an empty one while detached.</returns>
        internal Rectangle GetViewportScreenBounds()
        {
            if (Canvas == null)
                return Rectangle.Empty;

            var origin = new Vector2(ContentBounds.X - ActualBounds.X, ContentBounds.Y - ActualBounds.Y);
            Point topLeft = PointToScreen(origin);
            Point bottomRight = PointToScreen(origin + new Vector2(ContentBounds.Width, ContentBounds.Height));
            return Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }

        private static int RevealDelta(int start, int end, int viewStart, int viewEnd)
        {
            if (end - start > viewEnd - viewStart || start < viewStart)
                return start - viewStart;
            return end > viewEnd ? end - viewEnd : 0;
        }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~BringIntoViewTests"`
Expected: 8 pass. If `BringIntoView_ARealizedTreeRow_ScrollsTheTree` finds row 6 unrealized, the realization buffer is
smaller than assumed: use the highest realized index below the viewport instead (read `tree.List.Realized.Keys.Max()`),
keeping the assertion.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/UIElement.cs sources/IcyUI/UI/Controls/ScrollViewer.cs sources/IcyUI.Tests/UI/BringIntoViewTests.cs
git commit -m "Add UIElement.BringIntoView and screen-space bounds

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Routed `OnNavigate`/`OnActivate`; migrate Button, Selector, ComboBox, TreeView

All in one task: once Canvas routes, the old global subscriptions would double-handle presses, so the migration has to
land together.

**Files:**
- Modify: `sources/IcyUI/UI/UIElement.cs` (next to `OnScroll`, ~line 1935)
- Modify: `sources/IcyUI/UI/Canvas.cs` (class declaration line 28; `EnsureInputRoutingInitialized` ~line 723)
- Create: `sources/IcyUI/UI/Canvas.Navigation.cs`
- Modify: `sources/IcyUI/UI/Controls/Button.cs`, `Selector.cs`, `ComboBox.cs`, `TreeView.cs`
- Test: `sources/IcyUI.Tests/UI/NavigationRoutingTests.cs` (new); modify `Controls/SelectorTests.cs`,
  `Controls/ComboBoxTests.cs`

**Interfaces:**
- Consumes: `SpatialNavigation.Snap` (Task 1).
- Produces:
  - `protected internal virtual bool UIElement.OnNavigate(Vector2 direction)` and
    `protected internal virtual bool UIElement.OnActivate()`.
  - `Canvas` is `public partial class`; `Canvas.Navigation.cs` holds `private bool RouteNavigate(Vector2 direction)`
    and the handler `private void Navigation_FocusChanging(object? sender, AcceptableEventArgs<Vector2> e)`, which Task 5
    extends.

- [ ] **Step 1: Write the failing routing tests**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class NavigationRoutingTests
    {
        [Fact]
        public void AClaimingFocusedElement_HandlesThePress()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var probe = new Probe { Claim = true };
            canvas.Add(probe);
            canvas.Render();
            canvas.Focus(probe);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(new Vector2(0.2f, 0.9f)).Handled);
            Assert.Equal(Vector2.UnitY, probe.LastDirection);
        }

        [Fact]
        public void AnAncestor_GetsThePressTheFocusedElementLetsGo()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var child = new Probe();
            var parent = new ProbePanel { Claim = true };
            parent.Children.Add(child);
            canvas.Add(parent);
            canvas.Render();
            canvas.Focus(child);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(-Vector2.UnitX).Handled);
            Assert.Equal(-Vector2.UnitX, child.LastDirection);
            Assert.Equal(-Vector2.UnitX, parent.LastDirection);
        }

        [Fact]
        public void Activation_ClicksAFocusedButton_AndTogglesACheckBox()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var button = new Button();
            var checkBox = new CheckBox();
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            canvas.Add(button);
            canvas.Add(checkBox);
            canvas.Render();

            canvas.Focus(button);
            input.Events.Navigation.RaiseSelectElement();
            canvas.Focus(checkBox);
            input.Events.Navigation.RaiseSelectElement();

            Assert.Equal(1, clicks);
            Assert.True(checkBox.IsChecked == true);
        }

        [Fact]
        public void Activation_OfADisabledButton_DoesNothing()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var button = new Button();
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            canvas.Add(button);
            canvas.Render();
            canvas.Focus(button);
            button.IsEnabled = false;

            input.Events.Navigation.RaiseSelectElement();

            Assert.Equal(0, clicks);
        }

        [Fact]
        public void AnAppSubscriber_StillSeesEveryPress()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var probe = new Probe { Claim = true };
            canvas.Add(probe);
            canvas.Render();
            canvas.Focus(probe);
            int seen = 0;
            input.Events.Navigation.FocusChanging += (_, _) => seen++;

            input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY);

            Assert.Equal(1, seen);
        }

        [Fact]
        public void WithKeyboardNavigationDisabled_NothingIsRouted()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var probe = new Probe { Claim = true };
            canvas.Add(probe);
            canvas.Render();
            canvas.Focus(probe);
            canvas.IsKeyboardNavigationEnabled = false;

            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY).Handled);
            Assert.Null(probe.LastDirection);
        }

        private sealed class Probe : UIElement
        {
            public Probe()
            {
                IsFocusable = true;
                Width = 20;
                Height = 20;
            }

            public bool Claim { get; set; }

            public Vector2? LastDirection { get; private set; }

            protected internal override bool OnNavigate(Vector2 direction)
            {
                LastDirection = direction;
                return Claim;
            }
        }

        private sealed class ProbePanel : StackPanel
        {
            public bool Claim { get; set; }

            public Vector2? LastDirection { get; private set; }

            protected internal override bool OnNavigate(Vector2 direction)
            {
                LastDirection = direction;
                return Claim;
            }
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~NavigationRoutingTests"`
Expected: build errors (`OnNavigate` doesn't exist).

- [ ] **Step 3: Add the virtuals to `UIElement.cs`**, right after `OnScroll`:

```csharp
        /// <summary>
        /// Invoked by <see cref="UI.Canvas"/> for a directional navigation press (arrow keys, D-pad, left stick) while
        /// this element or one of its descendants has focus.
        /// </summary>
        /// <param name="direction">The pressed direction: one of the four unit vectors, in UI space (+Y down).</param>
        /// <returns>
        /// <see langword="true"/> to claim the press. <see cref="UI.Canvas"/> calls the focused element first, then
        /// each ancestor, and stops at the first claim. When nothing claims it, focus moves to the nearest control in
        /// <paramref name="direction"/> (see <see cref="UI.Canvas.MoveFocus(Vector2)"/>). The base implementation
        /// returns <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Claim only what you use, and let presses go at your edges (a list at its last row, a caret at the end of its
        /// text), so focus can move on to the next control.
        /// </remarks>
        protected internal virtual bool OnNavigate(Vector2 direction) => false;

        /// <summary>
        /// Invoked by <see cref="UI.Canvas"/> for an activation press (Enter, gamepad A) while this element or one of
        /// its descendants has focus.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> to claim the press; <see cref="UI.Canvas"/> then stops calling further ancestors.
        /// The base implementation returns <see langword="false"/>.
        /// </returns>
        protected internal virtual bool OnActivate() => false;
```

Note: the `<see cref="UI.Canvas.MoveFocus(Vector2)"/>` target arrives in Task 5. Until then, write it as
`<see cref="UI.Canvas.MoveFocus(bool)"/>` and switch it in Task 5 Step 3.

- [ ] **Step 4: Make `Canvas` partial and subscribe**

In `Canvas.cs` change `public class Canvas : ObservableDispatcherObject, IContainerLayout` to
`public partial class Canvas : ObservableDispatcherObject, IContainerLayout`. In `EnsureInputRoutingInitialized`, after
`events.Navigation.CloseModal += OnCloseModal;`, add:

```csharp
            events.Navigation.FocusChanging += Navigation_FocusChanging;
            events.Navigation.SelectElement += Navigation_SelectElement;
```

Create `Canvas.Navigation.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Data;

namespace Icy.UI
{
    /// <content>
    /// Directional and activation navigation: routes presses through the focused element, and moves focus spatially
    /// when nothing claims them.
    /// </content>
    public partial class Canvas
    {
        private void Navigation_FocusChanging(object? sender, AcceptableEventArgs<Vector2> e)
        {
            if (!IsKeyboardNavigationEnabled)
                return;

            Vector2 direction = SpatialNavigation.Snap(e.Data);
            if (direction == Vector2.Zero)
                return;

            if (RouteNavigate(direction))
                e.Handled = true;
        }

        private void Navigation_SelectElement(object? sender, EventArgs e)
        {
            if (!IsKeyboardNavigationEnabled)
                return;

            foreach (UIElement element in SelfAndAncestors(FocusedElement))
            {
                if (element.OnActivate())
                    return;
            }
        }

        private bool RouteNavigate(Vector2 direction)
        {
            foreach (UIElement element in SelfAndAncestors(FocusedElement))
            {
                if (element.OnNavigate(direction))
                    return true;
            }

            return false;
        }
    }
}
```

- [ ] **Step 5: `Button.OnActivate`** (add `using` nothing; insert after `OnTap`):

```csharp
        /// <inheritdoc/>
        /// <remarks>Clicks the button, like a tap, unless it's disabled.</remarks>
        protected internal override bool OnActivate()
        {
            if (!IsEnabled)
                return false;

            OnClick();
            return true;
        }
```

- [ ] **Step 6: Run the routing tests**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~NavigationRoutingTests"`
Expected: 6 pass.

- [ ] **Step 7: Migrate `Selector`**

In `Selector.cs`:
- In `SubscribeNavigation` and `UnsubscribeNavigation`, delete the `FocusChanging` and `SelectElement` lines (keep
  `CloseModal`). Update both summaries to say they handle Escape/gamepad B only; arrows and Enter arrive through
  `OnNavigate`/`OnActivate`.
- Replace the whole `OnNavigationFocusChanging` method (and its docs) with:

```csharp
        /// <inheritdoc/>
        /// <remarks>
        /// While the popup is open, moves <see cref="HighlightedIndex"/> by one in the pressed direction and claims the
        /// press. While it's closed, lets the press go, so focus moves on to the next control; Enter or gamepad A opens it
        /// (see <see cref="OnActivate"/>).
        /// </remarks>
        protected internal override bool OnNavigate(Vector2 direction)
        {
            if (!IsOpen || ItemCount == 0)
                return false;

            int delta = Math.Abs(direction.X) > Math.Abs(direction.Y)
                ? (direction.X < 0 ? -1 : 1)
                : (direction.Y < 0 ? -1 : 1);
            int next = HighlightedIndex == -1 ? 0 : HighlightedIndex + delta;
            HighlightedIndex = Math.Clamp(next, 0, ItemCount - 1);
            return true;
        }
```

- Replace the whole `OnNavigationSelectElement` method (keep its comment about the upper bound) with:

```csharp
        /// <inheritdoc/>
        /// <remarks>
        /// Closed: opens the popup. Open with a valid <see cref="HighlightedIndex"/>: commits it to
        /// <see cref="SelectingItemsControl.SelectedIndex"/> and closes. Always claims the press.
        /// </remarks>
        protected internal override bool OnActivate()
        {
            if (!IsOpen)
            {
                IsOpen = true;
                return true;
            }

            // Upper bound guarded too: the item count can shrink underneath a stale highlight (a live collection
            // change, or ComboBox's filtering) - committing it unguarded would throw from SelectedIndex's own range check.
            if (HighlightedIndex >= 0 && HighlightedIndex < ItemCount)
            {
                SelectedIndex = HighlightedIndex;
                IsOpen = false;
            }

            return true;
        }
```

- Update the `HookFocusGate` summary: the gate now only drives the `CloseModal` subscription.
- Remove `using` directives that became unused.

- [ ] **Step 8: Migrate `ComboBox`**

Replace `protected override void OnNavigationSelectElement(object? sender, EventArgs e)` with:

```csharp
        /// <inheritdoc/>
        /// <remarks>Closed: opens. Open: commits the highlighted match, or reverts the text when there's none.</remarks>
        protected internal override bool OnActivate()
        {
            if (!IsOpen)
            {
                IsOpen = true;
                return true;
            }

            CommitHighlightOrRevert();
            return true;
        }
```

Then replace every remaining mention of `OnNavigationSelectElement` in `ComboBox.cs` comments and `<see cref>`s with
`OnActivate`. ComboBox's focused element is its inner TextBox, a descendant, so its unclaimed presses reach this
override through the routing; no other change is needed.

- [ ] **Step 9: Migrate `TreeView`**

In `TreeView.cs`:
- Delete the `subscribedNavigation` field, `SubscribeNavigation`, `UnsubscribeNavigation` and
  `OnNavigationSelectElement`.
- In `OnAttached`, delete the comment and `if (IsFocused) SubscribeNavigation();`. In `OnDetached`, delete
  `UnsubscribeNavigation();`.
- `OnFocusChanged` becomes:

```csharp
        private void OnFocusChanged(object? sender, EventArgs e)
        {
            if (IsFocused)
            {
                currentRow ??= (selectedItem == null ? null : rows.FirstOrDefault(r => ReferenceEquals(r.Item, selectedItem)))
                    ?? (rows.Count > 0 ? rows[0] : null);
            }

            SyncList();
        }
```

- Replace `OnNavigationFocusChanging` (and its docs) with:

```csharp
        /// <summary>
        /// Moves through the rows. A press that would leave the tree (past the first or last row, or Left on a root with
        /// nothing to collapse) isn't claimed, so focus moves on to the next control.
        /// </summary>
        /// <param name="direction">The pressed direction.</param>
        /// <returns><see langword="true"/> when the tree used the press.</returns>
        protected internal override bool OnNavigate(Vector2 direction)
        {
            if (rows.Count == 0)
                return false;

            int index = currentRow == null ? -1 : rows.IndexOf(currentRow);
            if (index < 0)
            {
                MoveTo(0);
                return true;
            }

            FlatRow row = rows[index];
            if (MathF.Abs(direction.Y) >= MathF.Abs(direction.X))
            {
                int next = direction.Y < 0 ? index - 1 : index + 1;
                if (next < 0 || next >= rows.Count)
                    return false;
                MoveTo(next);
                return true;
            }

            if (direction.X > 0)
            {
                if (HasChildren(row.Item) && !IsExpanded(row.Item))
                {
                    Expand(row.Item);
                    return true;
                }

                if (index + 1 >= rows.Count)
                    return false;
                MoveTo(index + 1);
                return true;
            }

            if (HasChildren(row.Item) && IsExpanded(row.Item))
            {
                Collapse(row.Item);
                return true;
            }

            if (row.Parent == null)
                return false;
            MoveTo(rows.IndexOf(row.Parent));
            return true;
        }

        /// <summary>Expands or collapses the current row (Enter, gamepad A).</summary>
        /// <returns><see langword="true"/> when the current row has children and was toggled.</returns>
        protected internal override bool OnActivate()
        {
            if (currentRow == null || !HasChildren(currentRow.Item))
                return false;

            Toggle(currentRow.Item);
            return true;
        }
```

- Remove `using Icy.Input.Events;` if it's now unused.

- [ ] **Step 10: Update the selector tests for the agreed behaviour change**

In `Controls/SelectorTests.cs`:
- Rename `FocusChanging_WhileClosed_OpensAndHighlightsTheFirstItem` to
  `FocusChanging_WhileClosed_LetsThePressGo`, and replace its last two lines with:

```csharp
            Assert.False(input.Events.Navigation.RaiseFocusChanging(new Vector2(0, 1)).Handled);
            Assert.False(selector.IsOpen);
```

- In `SelectElement_OpenWithHighlight_CommitsAndCloses`, `CloseModal_WhileOpen_ClosesWithoutChangingSelection` and
  `Closing_ClearsTheHighlight`, insert `selector.IsOpen = true;` on the line before the first
  `input.Events.Navigation.RaiseFocusChanging(...)`.

In `Controls/ComboBoxTests.cs`, replace the reflection helpers:

```csharp
        private static void InvokeOnNavigationSelectElement(ComboBox comboBox) => comboBox.OnActivate();

        private static void InvokeOnNavigationFocusChanging(ComboBox comboBox, System.Numerics.Vector2 direction)
        {
            // Arrows only move the highlight while the popup is open; a closed ComboBox lets them go.
            comboBox.IsOpen = true;
            comboBox.OnNavigate(direction);
        }
```

Keep `InvokeOnNavigationCloseModal` unchanged.

- [ ] **Step 11: Run the affected suites**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Selector|FullyQualifiedName~ComboBox|FullyQualifiedName~Dropdown|FullyQualifiedName~TreeView|FullyQualifiedName~NavigationRoutingTests"`
Expected: all pass. A TreeView test failing on `Handled` is a real regression: the routing must reproduce today's
results exactly.

- [ ] **Step 12: Run everything and commit**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj"` and check `Total:`; all pass.

```bash
git add -A sources/IcyUI sources/IcyUI.Tests
git commit -m "Route navigation and activation through the focused element

Canvas now owns FocusChanging/SelectElement and calls OnNavigate/OnActivate
on the focused element and its ancestors. Button activates on Enter/A;
Selector, ComboBox and TreeView move off their focus-gated subscriptions.
A closed Dropdown no longer swallows arrows.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: TextBox edge escape

**Files:**
- Modify: `sources/IcyUI/UI/Controls/TextBox.cs` (`OnKeyDown`, ~line 248)
- Test: `sources/IcyUI.Tests/Controls/TextBoxNavigationTests.cs`

**Interfaces:**
- Consumes: `UIElement.OnNavigate` (Task 3), Canvas routing (Task 3).

- [ ] **Step 1: Write the failing tests**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Input;
using Icy.Tests.UI;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TextBoxNavigationTests
    {
        [Fact]
        public void LeftAndRight_MoveTheCaret_AndEscapeOnlyAtTheEdges()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var textBox = new TextBox { Width = 100, Height = 20, Text = "ab" };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);

            // The caret starts at 0: Left is at the edge, Right moves.
            Assert.False(input.Events.Navigation.RaiseFocusChanging(-Vector2.UnitX).Handled);
            Assert.True(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
            input.Events.Text.RaiseTextInput("X");
            Assert.Equal("aXb", textBox.Text);

            Assert.True(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
        }

        [Fact]
        public void UpAndDown_AreNeverClaimed()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var textBox = new TextBox { Width = 100, Height = 20, Text = "ab" };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);

            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY).Handled);
            Assert.False(input.Events.Navigation.RaiseFocusChanging(-Vector2.UnitY).Handled);
        }

        [Fact]
        public void TheRawLeftKey_NoLongerMovesTheCaretOnItsOwn()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var textBox = new TextBox { Width = 100, Height = 20, Text = "ab" };
            canvas.Add(textBox);
            canvas.Render();
            canvas.Focus(textBox);
            input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX);

            // Only the navigation route moves the caret now, so a lone KeyDown (no navigation event) changes nothing.
            input.Keyboard.RaiseKeyDown(Keys.Left);
            input.Events.Text.RaiseTextInput("X");

            Assert.Equal("aXb", textBox.Text);
        }

        [Fact]
        public void ComboBoxTextBox_KeepsLeftRightForTheCaret_AndDownLeavesWhileClosed()
        {
            (Canvas canvas, var input, _) = BringIntoViewTests.CreateCanvas();
            var comboBox = new ComboBox { Width = 120, Height = 24, ItemsSource = new List<object> { "Apple", "Banana" } };
            canvas.Add(comboBox);
            canvas.Render();
            TextBox textBox = comboBox.EnumerateVisualSubtree().OfType<TextBox>().First();
            canvas.Focus(textBox);
            textBox.Text = "ab";
            comboBox.IsOpen = false;

            Assert.True(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitX).Handled);
            Assert.Same(textBox, canvas.FocusedElement);
            Assert.False(input.Events.Navigation.RaiseFocusChanging(Vector2.UnitY).Handled);
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TextBoxNavigationTests"`
Expected: FAIL. The caret assertions fail: Left/Right aren't claimed through the routing yet.

- [ ] **Step 3: Implement**

In `TextBox.OnKeyDown`, delete the `case Keys.Left:` and `case Keys.Right:` blocks. Add (with `using System.Numerics;`):

```csharp
        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Left/Right move the caret by one character and claim the press.</description></item>
        /// <item><description>At the start (Left) or end (Right) of the text the press isn't claimed, so focus moves on.</description></item>
        /// <item><description>Up/Down are never claimed: the box is single-line.</description></item>
        /// </list>
        /// </remarks>
        protected internal override bool OnNavigate(Vector2 direction)
        {
            if (direction.X < 0 && caretIndex > 0)
                caretIndex--;
            else if (direction.X > 0 && caretIndex < Text.Length)
                caretIndex++;
            else
                return false;

            InvalidateVisual();
            return true;
        }
```

- [ ] **Step 4: Run TextBox and ComboBox tests**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TextBox|FullyQualifiedName~ComboBox"`
Expected: all pass. If an existing TextBox test drove the caret with `input.Keyboard.RaiseKeyDown(Keys.Left/Right)`,
switch it to `input.Events.Navigation.RaiseFocusChanging(∓Vector2.UnitX)` with the TextBox focused on a canvas.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/TextBox.cs sources/IcyUI.Tests/Controls
git commit -m "Move TextBox caret keys onto the navigation route with edge escape

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The spatial move

**Files:**
- Modify: `sources/IcyUI/UI/Canvas.Navigation.cs`
- Modify: `sources/IcyUI/UI/Canvas.cs` (`Focus` ~line 521, `MoveFocus(bool)` ~line 566)
- Modify: `sources/IcyUI/UI/UIElement.cs` (switch the Task 3 cref)
- Test: `sources/IcyUI.Tests/UI/SpatialFocusTests.cs`

**Interfaces:**
- Consumes: `SpatialNavigation.FindBest/Snap` (Task 1), `GetScreenBounds`, `BringIntoView`,
  `ScrollViewer.GetViewportScreenBounds` (Task 2), `RouteNavigate`, `Navigation_FocusChanging` (Task 3).
- Produces: `public bool Canvas.MoveFocus(Vector2 direction)`.

- [ ] **Step 1: Write the failing tests**

```csharp
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
            var clip = new Border { Width = 50, Height = 20, Margin = new Thickness(0, 160, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true };
            clip.Child = new Button { Width = 40, Height = 20, Margin = new Thickness(0, 100, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(clip);
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
            canvas.Add(new ScrollViewer { Width = 100, Height = 100, Margin = new Thickness(0, 700, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = panel });
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
```

The clip walk relies on a ScrollViewer's content sitting under its non-clipping chrome `Border`, whose parent is the
`ScrollViewer` (checked at plan time: `ScrollViewer` sets `ClipToBounds` on itself, not on `Chrome`).

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SpatialFocusTests"`
Expected: most fail (focus never moves; `Handled` is false).

- [ ] **Step 3: Implement**

In `Canvas.cs`:
- In `Focus`, right before `FocusedElement?.SetFocused(false);`, add `ClearReverseStep();`.
- Replace the body of `MoveFocus(bool forward)` with:

```csharp
            List<UIElement> focusable = [.. EnumerateFocusOrder(FocusedElement)];
            if (focusable.Count == 0)
                return;

            int currentIndex = FocusedElement != null ? focusable.IndexOf(FocusedElement) : -1;
            int nextIndex = currentIndex == -1
                ? (forward ? 0 : focusable.Count - 1)
                : ((currentIndex + (forward ? 1 : -1)) + focusable.Count) % focusable.Count;
            FocusFromNavigation(focusable[nextIndex]);
```

  and add to its `<remarks>` (create one if missing): "The newly focused element is brought into view (see
  `UIElement.BringIntoView`)."

In `UIElement.cs`, switch the Task 3 cref in `OnNavigate`'s docs to `<see cref="UI.Canvas.MoveFocus(Vector2)"/>`.

Replace `Canvas.Navigation.cs` with:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Data;
using Icy.UI.Controls;

namespace Icy.UI
{
    /// <content>
    /// Directional and activation navigation: routes presses through the focused element, and moves focus spatially
    /// when nothing claims them.
    /// </content>
    public partial class Canvas
    {
        // How far, in layout units, a candidate may overlap the focused element and still count as ahead.
        private const float SpatialTolerance = 4f;

        private readonly List<UIElement> spatialCandidates = [];
        private readonly List<Rectangle> spatialRectangles = [];
        private WeakReference<UIElement>? reverseOrigin;
        private WeakReference<UIElement>? reverseTarget;
        private Vector2 reverseDirection;

        /// <summary>
        /// Moves focus to the nearest focusable element in <paramref name="direction"/>, the way arrow keys, the D-pad
        /// and the left stick do when the focused element doesn't use the press.
        /// </summary>
        /// <param name="direction">Any direction, UI space (+Y down). It's snapped to its dominant axis.</param>
        /// <returns><see langword="true"/> when focus moved.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Candidates are the focusable, visible, enabled elements of the focused element's focus scope (see <see cref="UIElement.IsFocusScope"/>), or of the whole canvas without one.</description></item>
        /// <item><description>Elements clipped away are skipped, except content scrolled out of a <see cref="ScrollViewer"/> that is itself on screen.</description></item>
        /// <item><description>Candidates straight ahead (overlapping on the cross axis) win over diagonal ones; then the nearest wins.</description></item>
        /// <item><description>Pressing the opposite direction right after a move returns to where it started.</description></item>
        /// <item><description>With nothing focused, the first element in Tab order is focused. There is no wrap-around.</description></item>
        /// </list>
        /// <para>The newly focused element is brought into view (see <see cref="UIElement.BringIntoView"/>).</para>
        /// </remarks>
        public bool MoveFocus(Vector2 direction)
        {
            direction = SpatialNavigation.Snap(direction);
            if (direction == Vector2.Zero)
                return false;

            UIElement? from = FocusedElement;
            if (from == null || from.Canvas != this)
            {
                UIElement? first = EnumerateFocusOrder(null).FirstOrDefault(IsSpatiallyReachable);
                if (first == null)
                    return false;
                FocusFromNavigation(first);
                return true;
            }

            UIElement? target = TakeReverseStep(from, direction) ?? FindSpatialTarget(from, direction);
            if (target == null)
                return false;

            FocusFromNavigation(target);
            reverseOrigin = new WeakReference<UIElement>(from);
            reverseTarget = new WeakReference<UIElement>(target);
            reverseDirection = direction;
            return true;
        }

        private void Navigation_FocusChanging(object? sender, AcceptableEventArgs<Vector2> e)
        {
            if (!IsKeyboardNavigationEnabled)
                return;

            Vector2 direction = SpatialNavigation.Snap(e.Data);
            if (direction == Vector2.Zero)
                return;

            if (RouteNavigate(direction) || MoveFocus(direction))
                e.Handled = true;
        }

        private void Navigation_SelectElement(object? sender, EventArgs e)
        {
            if (!IsKeyboardNavigationEnabled)
                return;

            foreach (UIElement element in SelfAndAncestors(FocusedElement))
            {
                if (element.OnActivate())
                    return;
            }
        }

        private bool RouteNavigate(Vector2 direction)
        {
            foreach (UIElement element in SelfAndAncestors(FocusedElement))
            {
                if (element.OnNavigate(direction))
                    return true;
            }

            return false;
        }

        /// <summary>The Tab-order candidates of <paramref name="focused"/>'s focus scope (shared with <see cref="MoveFocus(bool)"/>).</summary>
        private IEnumerable<UIElement> EnumerateFocusOrder(UIElement? focused)
        {
            UIElement? scope = FindEnclosingFocusScope(focused);
            IEnumerable<UIElement> roots = scope != null ? scope.EnumerateVisualSubtree().Skip(1) : EnumerateAllElements();
            return roots.Where(e => e.IsFocusable && e.IsVisible);
        }

        private void FocusFromNavigation(UIElement element)
        {
            Focus(element);
            if (FocusedElement == element)
                element.BringIntoView();
        }

        private void ClearReverseStep()
        {
            reverseOrigin = null;
            reverseTarget = null;
        }

        private UIElement? TakeReverseStep(UIElement from, Vector2 direction)
        {
            if (direction != -reverseDirection
                || reverseTarget == null || !reverseTarget.TryGetTarget(out UIElement? target) || target != from
                || reverseOrigin == null || !reverseOrigin.TryGetTarget(out UIElement? origin))
            {
                return null;
            }

            return origin.Canvas == this && EnumerateFocusOrder(from).Contains(origin) && IsSpatiallyReachable(origin) ? origin : null;
        }

        private UIElement? FindSpatialTarget(UIElement from, Vector2 direction)
        {
            spatialCandidates.Clear();
            spatialRectangles.Clear();
            foreach (UIElement element in EnumerateFocusOrder(from))
            {
                if (element == from || IsAncestorOf(element, from) || !IsSpatiallyReachable(element))
                    continue;
                spatialCandidates.Add(element);
                spatialRectangles.Add(element.GetScreenBounds());
            }

            int best = SpatialNavigation.FindBest(from.GetScreenBounds(), direction, spatialRectangles, SpatialTolerance * EffectiveScale);
            UIElement? result = best < 0 ? null : spatialCandidates[best];
            spatialCandidates.Clear();
            return result;
        }

        private static bool IsAncestorOf(UIElement candidate, UIElement element)
        {
            for (UIElement? current = element.Parent; current != null; current = current.Parent)
            {
                if (current == candidate)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether <paramref name="element"/> can be reached: effectively visible and enabled, and not clipped away. A
        /// <see cref="ScrollViewer"/> doesn't hide its own content (it's reachable by scrolling), but the walk then
        /// continues from the viewer's own bounds, so a viewer that is itself hidden hides everything in it.
        /// </summary>
        private bool IsSpatiallyReachable(UIElement element)
        {
            if (element.Canvas != this)
                return false;

            Rectangle visible = element.GetScreenBounds();
            for (UIElement current = element; ; current = current.Parent!)
            {
                if (!current.IsVisible || current.Opacity <= 0 || !current.IsEnabled)
                    return false;

                UIElement? parent = current.Parent;
                if (parent == null)
                    break;
                if (parent is ScrollViewer)
                    visible = parent.GetScreenBounds();
                else if (parent.ClipToBounds)
                    visible = Rectangle.Intersect(visible, parent.GetScreenBounds());
                if (visible.Width <= 0 || visible.Height <= 0)
                    return false;
            }

            visible = Rectangle.Intersect(visible, new Rectangle(Point.Empty, Configuration.RenderContext.ViewportSize));
            return visible.Width > 0 && visible.Height > 0;
        }
    }
}
```

- [ ] **Step 4: Run the spatial tests, then everything**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SpatialFocusTests|FullyQualifiedName~BringIntoViewTests|FullyQualifiedName~NavigationRoutingTests"`
Expected: all pass.
Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj"`; check `Total:`. Expected: all pass. A TreeView or Selector test that
now reports `Handled == true` at an edge means its canvas has another focusable element in that direction: check the
test's intent; if it asserts "the tree lets go", assert `canvas.FocusedElement` moved instead.

- [ ] **Step 5: Commit**

```bash
git add -A sources/IcyUI sources/IcyUI.Tests
git commit -m "Move focus spatially when no control claims a directional press

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Held-direction input state machine

**Files:**
- Modify: `sources/IcyUI/Input/Events/NavigationEvents.cs`
- Modify: `sources/IcyUI/Input/Events/INavigationEvents.cs` (docs only)
- Modify: `sources/IcyUI.Tests/Input/FakeInputSystem.cs`
- Test: `sources/IcyUI.Tests/Input/NavigationEventsTests.cs`

**Interfaces:**
- Produces (tests): `FakeKeyboardInput.ModifierKeys { get; set; }`, `FakeInputSystem.Gamepad { get; set; }` typed
  `IGamepadInput?`, new `FakeGamepadInput` with `RaiseButtonPressed(GamePadButtons)`,
  `RaiseButtonReleased(GamePadButtons)`, `RaiseLeftStick(Vector2)`.

- [ ] **Step 1: Extend the fakes and write the failing tests**

In `FakeInputSystem.cs`:
- `FakeKeyboardInput`: replace `public ModifierKeys ModifierKeys => ModifierKeys.None;` with
  `public ModifierKeys ModifierKeys { get; set; }`.
- `FakeInputSystem`: replace `public IGamepadInput? Gamepad => null;` with `public IGamepadInput? Gamepad { get; set; }`.
- Add after `FakeKeyboardInput`:

```csharp
    /// <summary>
    /// A fake <see cref="IGamepadInput"/> that lets tests raise button and stick events directly.
    /// </summary>
    public sealed class FakeGamepadInput : IGamepadInput
    {
        public event EventHandler<GenericEventArgs<GamePadButtons>>? ButtonPressed;

        public event EventHandler<GenericEventArgs<GamePadButtons>>? ButtonReleased;

        public event EventHandler<GenericEventArgs<Vector2>>? LeftStickMove;

        public event EventHandler<GenericEventArgs<Vector2>>? RightStickMove;

        public event EventHandler<GenericEventArgs<float>>? LeftShoulderUpdate;

        public event EventHandler<GenericEventArgs<float>>? RightShoulderUpdate;

        public GamePadState GamePadInfo => default;

        public bool IsListening => true;

        public bool IsInitialized { get; private set; }

        public void Initialize() => IsInitialized = true;

        public bool DisableListening() => true;

        public bool EnableListening() => true;

        public void RaiseButtonPressed(GamePadButtons button) => ButtonPressed?.Invoke(this, new GenericEventArgs<GamePadButtons>(button));

        public void RaiseButtonReleased(GamePadButtons button) => ButtonReleased?.Invoke(this, new GenericEventArgs<GamePadButtons>(button));

        /// <summary>Raises <see cref="LeftStickMove"/> with a raw device value (+Y up, like real pads).</summary>
        public void RaiseLeftStick(Vector2 value) => LeftStickMove?.Invoke(this, new GenericEventArgs<Vector2>(value));
    }
```

(Add `using System.Numerics;` / `using Icy.Input.Devices;` if missing. If the compiler warns that the unused events
`RightStickMove`/`LeftShoulderUpdate`/`RightShoulderUpdate` are never raised, add `#pragma warning disable CS0067` around
them, as other fakes in the file do.)

Append to `NavigationEventsTests` (inside the class):

```csharp
        private static (NavigationEvents Navigation, FakeInputSystem Input, FakeGamepadInput Pad, List<Vector2> Raised) Create()
        {
            var pad = new FakeGamepadInput();
            var input = new FakeInputSystem { Gamepad = pad };
            var navigation = new NavigationEvents(input);
            navigation.Initialize();
            var raised = new List<Vector2>();
            navigation.FocusChanging += (_, e) => raised.Add(e.Data);
            return (navigation, input, pad, raised);
        }

        [Fact]
        public void Defaults_AreTunedForMenus()
        {
            (NavigationEvents navigation, _, _, _) = Create();

            Assert.Equal(0.5f, navigation.MinimalFocusChangeDistance);
            Assert.Equal(TimeSpan.FromSeconds(0.4), navigation.RepeatStartDelay);
            Assert.Equal(TimeSpan.FromSeconds(0.1), navigation.RepeatDelay);
        }

        [Fact]
        public void AHeldArrow_RepeatsAfterTheStartDelay_ThenAtTheInterval_AndStopsOnRelease()
        {
            (NavigationEvents navigation, FakeInputSystem input, _, List<Vector2> raised) = Create();

            input.Keyboard.RaiseKeyDown(Keys.Down);
            navigation.Update(TimeSpan.FromSeconds(0.3));
            Assert.Single(raised);

            navigation.Update(TimeSpan.FromSeconds(0.1));
            Assert.Equal(2, raised.Count);
            navigation.Update(TimeSpan.FromSeconds(0.1));
            Assert.Equal(3, raised.Count);

            input.Keyboard.RaiseKeyUp(Keys.Down);
            navigation.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(3, raised.Count);
            Assert.All(raised, d => Assert.Equal(Vector2.UnitY, d));
        }

        [Fact]
        public void APressedDPadButton_RaisesOnce_ThenRepeats_UntilReleased()
        {
            (NavigationEvents navigation, _, FakeGamepadInput pad, List<Vector2> raised) = Create();

            pad.RaiseButtonPressed(GamePadButtons.PadLeft);
            Assert.Equal([-Vector2.UnitX], raised);
            navigation.Update(TimeSpan.FromSeconds(0.4));
            Assert.Equal(2, raised.Count);

            pad.RaiseButtonReleased(GamePadButtons.PadLeft);
            navigation.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(2, raised.Count);
        }

        [Fact]
        public void ReleasingAnOlderSource_DoesNotStopTheNewerHeldDirection()
        {
            (NavigationEvents navigation, FakeInputSystem input, FakeGamepadInput pad, List<Vector2> raised) = Create();

            input.Keyboard.RaiseKeyDown(Keys.Down);
            pad.RaiseButtonPressed(GamePadButtons.PadRight);
            input.Keyboard.RaiseKeyUp(Keys.Down);
            navigation.Update(TimeSpan.FromSeconds(0.4));

            Assert.Equal([Vector2.UnitY, Vector2.UnitX, Vector2.UnitX], raised);
        }

        [Fact]
        public void TheStick_FiresOncePastTheDeadZone_FlipsY_AndIgnoresJitter()
        {
            (_, _, FakeGamepadInput pad, List<Vector2> raised) = Create();

            pad.RaiseLeftStick(new Vector2(0, 0.3f));
            Assert.Empty(raised);

            pad.RaiseLeftStick(new Vector2(0.1f, 0.8f));
            pad.RaiseLeftStick(new Vector2(0.12f, 0.85f));
            pad.RaiseLeftStick(new Vector2(0.05f, 0.9f));

            // Pushing up on a pad (+Y) is "up" in UI space (-Y).
            Assert.Equal([-Vector2.UnitY], raised);
        }

        [Fact]
        public void TheStick_ReleasesWithHysteresis_AndChangingAxisIsANewPress()
        {
            (NavigationEvents navigation, _, FakeGamepadInput pad, List<Vector2> raised) = Create();

            pad.RaiseLeftStick(new Vector2(0.9f, 0));
            pad.RaiseLeftStick(new Vector2(0.4f, 0));
            Assert.Single(raised);

            pad.RaiseLeftStick(new Vector2(0.1f, -0.9f));
            Assert.Equal([Vector2.UnitX, Vector2.UnitY], raised);

            pad.RaiseLeftStick(new Vector2(0, -0.3f));
            navigation.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(2, raised.Count);
        }

        [Theory]
        [InlineData(ModifierKeys.Ctrl)]
        [InlineData(ModifierKeys.Shift)]
        public void ModifiedArrows_AreLeftForEditing(ModifierKeys modifiers)
        {
            (_, FakeInputSystem input, _, List<Vector2> raised) = Create();
            input.Keyboard.ModifierKeys = modifiers;

            input.Keyboard.RaiseKeyDown(Keys.Left);

            Assert.Empty(raised);
        }
```

(If `NavigationEvents` is `internal`, the existing test already constructs it, so `InternalsVisibleTo` is in place.)

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~NavigationEventsTests"`
Expected: the new tests fail (defaults, no repeat, stick floods, modifiers not ignored); the original arrow theory passes.

- [ ] **Step 3: Implement the state machine in `NavigationEvents.cs`**

- Delete the `// TODO: implement repeat system.` comment.
- Change the defaults: `MinimalFocusChangeDistance { get; set; } = 0.5f;`,
  `RepeatDelay { get; set; } = TimeSpan.FromSeconds(0.1);`,
  `RepeatStartDelay { get; set; } = TimeSpan.FromSeconds(0.4);`.
- Add fields:

```csharp
        // The stick releases below this fraction of MinimalFocusChangeDistance, so a stick resting at the edge doesn't flicker.
        private const float StickReleaseFraction = 0.7f;

        private HeldSource heldSource;
        private Keys heldKey;
        private GamePadButtons heldButton;
        private Vector2 heldDirection;
        private TimeSpan heldTime;
        private bool repeating;
        private Vector2 stickDirection;
```

  and the nested enum at the end of the class:

```csharp
        private enum HeldSource
        {
            None,
            Key,
            PadButton,
            Stick,
        }
```

- `Update` becomes:

```csharp
        /// <inheritdoc/>
        /// <remarks>
        /// Repeats a held direction (arrow key, D-pad button or stick): first after <see cref="RepeatStartDelay"/>, then
        /// every <see cref="RepeatDelay"/>.
        /// </remarks>
        public void Update(TimeSpan deltaTime)
        {
            if (heldSource == HeldSource.None)
                return;

            heldTime += deltaTime;
            if (heldTime >= (repeating ? RepeatDelay : RepeatStartDelay))
            {
                repeating = true;
                heldTime = TimeSpan.Zero;
                OnDirectionalFocus(heldDirection);
            }
        }
```

- `Initialize` and `Devices_DeviceConnected`/`Devices_DeviceDisconnected`: also subscribe/unsubscribe
  `keyboard.KeyUp += Keyboard_KeyUp` and `gamepad.ButtonReleased += Gamepad_ButtonReleased` alongside the existing
  `KeyDown`/`ButtonPressed` lines (for `InputSystem.Keyboard`/`InputSystem.Gamepad` in `Initialize` too).
- In `Gamepad_ButtonPressed`, replace the four `PadUp/PadDown/PadLeft/PadRight` cases' bodies with
  `Press(<same direction>, HeldSource.PadButton, button: e.Data);`, for example:

```csharp
                case GamePadButtons.PadUp:
                    Press(-Vector2.UnitY, HeldSource.PadButton, button: e.Data);
                    break;
```

- In `Keyboard_KeyDown`, before the `switch`, add:

```csharp
            // Ctrl/Shift+arrow belong to editing (word jumps, selection), not to focus navigation.
            bool editingModifiers = (keyboard.ModifierKeys & (ModifierKeys.Ctrl | ModifierKeys.Shift)) != 0;
```

  and replace the four arrow cases with, e.g.:

```csharp
                case Keys.Up:
                    if (!editingModifiers)
                        Press(-Vector2.UnitY, HeldSource.Key, key: e.Data);
                    break;
```

- Replace `Gamepad_LeftStickMove` with:

```csharp
        private void Gamepad_LeftStickMove(object? sender, GenericEventArgs<Vector2> e)
        {
            // Devices report +Y as up; UI space is +Y down.
            var value = new Vector2(e.Data.X, -e.Data.Y);
            float magnitude = value.Length();
            if (stickDirection == Vector2.Zero)
            {
                if (magnitude > MinimalFocusChangeDistance)
                {
                    stickDirection = Snap(value);
                    Press(stickDirection, HeldSource.Stick);
                }

                return;
            }

            if (magnitude <= MinimalFocusChangeDistance * StickReleaseFraction)
            {
                stickDirection = Vector2.Zero;
                Release(HeldSource.Stick);
                return;
            }

            Vector2 snapped = Snap(value);
            if (snapped != stickDirection)
            {
                stickDirection = snapped;
                Press(snapped, HeldSource.Stick);
            }
        }
```

- Add:

```csharp
        private void Keyboard_KeyUp(object? sender, GenericEventArgs<Keys> e)
        {
            if (heldSource == HeldSource.Key && heldKey == e.Data)
                Release(HeldSource.Key);
        }

        private void Gamepad_ButtonReleased(object? sender, GenericEventArgs<GamePadButtons> e)
        {
            if (heldSource == HeldSource.PadButton && heldButton == e.Data)
                Release(HeldSource.PadButton);
        }

        /// <summary>Raises a direction now and makes it the held one; the latest press wins over any older one.</summary>
        private void Press(Vector2 direction, HeldSource source, Keys key = Keys.None, GamePadButtons button = default)
        {
            heldSource = source;
            heldKey = key;
            heldButton = button;
            heldDirection = direction;
            heldTime = TimeSpan.Zero;
            repeating = false;
            OnDirectionalFocus(direction);
        }

        private void Release(HeldSource source)
        {
            if (heldSource == source)
                heldSource = HeldSource.None;
        }

        private static Vector2 Snap(Vector2 value) =>
            MathF.Abs(value.X) >= MathF.Abs(value.Y) ? new Vector2(MathF.Sign(value.X), 0) : new Vector2(0, MathF.Sign(value.Y));
```

(`Snap` duplicates `Icy.UI.SpatialNavigation.Snap`'s rule for non-zero input; the input layer must not depend on
`Icy.UI`. If the project already allows `Icy.Input` → `Icy.UI` references, call `SpatialNavigation.Snap` instead.)

- In `INavigationEvents.cs`, extend the `FocusChanging` remarks and the `MinimalFocusChangeDistance` summary:

```csharp
        /// <remarks>
        /// <para>Direction of the navigation change is passed to event arguments, in UI space (+Y down).</para>
        /// <para>
        /// Raised once per press of an arrow key (without Ctrl or Shift), a D-pad button, or the left stick crossing
        /// <see cref="MinimalFocusChangeDistance"/>, then repeated while held (see <see cref="IStartRepeatEvents"/>).
        /// <see cref="UI.Canvas"/> routes it to the focused element and moves focus spatially when nothing claims it.
        /// </para>
        /// </remarks>
```

```csharp
        /// <summary>
        /// Gets or sets the stick deflection, from <c>0</c> to <c>1</c>, past which the left stick raises
        /// <see cref="FocusChanging"/>. The stick releases below 70% of it. Defaults to <c>0.5</c>.
        /// </summary>
```

- [ ] **Step 4: Run the input tests, then everything**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~NavigationEventsTests"`
Expected: all pass.
Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj"`; check `Total:`; all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/Input/Events sources/IcyUI.Tests/Input
git commit -m "Repeat held navigation directions and make the stick usable

One state machine for arrows, D-pad and stick: press fires once, holding
repeats (0.4 s, then every 0.1 s). The stick gets a 0.5 dead zone with
hysteresis and its Y axis flipped into UI space. Ctrl/Shift+arrows are
left to editing.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The original report, end to end; final verification

**Files:**
- Modify: `sources/IcyUI.Tests/Samples/TreeViewDemoTests.cs`

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Write the behavioural test**

Add to `TreeViewDemoTests` (it already has the themed-canvas setup in `TheLiveTree_ShowsEachRowsOwnTextAfterEdits`;
reuse the same builder lines, with `IsInputEnabled = true` on the canvas and the `FakeInputSystem` kept in a local):

```csharp
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

            // Three root rows: two Downs inside the tree, the third leaves it for the buttons below.
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
```

Focusing the tree makes row 0 current, so `Rows.Count - 1` Downs stay inside it and the next one leaves.

- [ ] **Step 2: Run it**

Run: `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewDemoTests"`
Expected: pass.

- [ ] **Step 3: Full verification**

Run, from `sources/`:
- `dotnet build "IcyUI.sln"`: 0 errors; no new warnings in touched library files
  (`dotnet build "IcyUI/IcyUI.csproj" 2>&1 | grep -E "warning" | grep -E "Canvas|SpatialNavigation|UIElement|ScrollViewer|Selector|ComboBox|TreeView|TextBox|Button|NavigationEvents"` prints nothing new).
- `dotnet test "IcyUI.Tests/IcyUI.Tests.csproj"`: check `Total:` and `Failed: 0`.

- [ ] **Step 4: Record plan-time revisions in the spec and commit**

Append to `docs/superpowers/specs/2026-10-07-spatial-navigation-design.md` a `## Plan-time revisions` section with the
three bullets from this plan's "Plan-time revisions to the spec".

```bash
git add sources/IcyUI.Tests/Samples/TreeViewDemoTests.cs docs/superpowers/specs/2026-10-07-spatial-navigation-design.md
git commit -m "Pin the TreeView demo's arrow and activation round trip

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Hand off for the smoke test**

Report to Ivan with the spec's smoke-test list (both engines, x64 and ARM64), calling out the Stride stick Y sign. Do not
claim any smoke test ran.
