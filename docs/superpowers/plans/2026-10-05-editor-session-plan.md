# Phase 10.3a: Core Support, Editor Session and Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The headless half of the editor overlay: core `Grid` spans and input/canvas support, an `EditorSession` that owns selection, modes and commands for one canvas, and placement strategies that turn moves, resizes and nudges into minimal markup edits.

**Architecture:** Core `IcyUI` gets five small engine-neutral changes. `IcyUI.Design` gets a new `Icy.Design.Editor` namespace: `EditorSession` (attach, modes, hit resolution, selection, blocked state), headless gestures (`MoveGesture`, `ResizeGesture`, nudge, delete) running inside one undo transaction each, `EditorCommands`/`EditorBindings` registered with first-wins dispatch, and `Icy.Design.Editor.Placement` with a strategy per container kind. 10.3b adds the visual `EditorFrame` on top.

**Tech Stack:** C# / .NET 10, xUnit v2, `IcyUI` core, `IcyUI.Design` (Phases 10.1 and 10.2).

**Spec:** `docs/superpowers/specs/2026-10-05-editor-frame-design.md` (sections 1–3, 5, and the testing rows for core, session and strategies). 10.3b (`docs/superpowers/plans/2026-10-05-editor-frame-plan.md`) covers section 4 and the demo.

## Global Constraints

- Core changes stay engine-neutral: nothing in `IcyUI.MonoGame`, `IcyUI.Stride` or `IcyUI.FNA` changes.
- **Every public API gets complete XML documentation** (`<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>` with `<list>`/`<para>` where useful, `<see cref>`/`<see langword>`). `IcyUI.Design` has `documentInternalElements: false`.
- Match the surrounding style: copyright header, block-scoped namespaces, StyleCop member order (fields, constructors, events, properties, methods; public before internal before private; static before instance).
- Layout limits use `float.NaN` as "unset"; never pass them to `Math.Min`/`Math.Max`/`float.Clamp` directly.
- **Warnings:** the baseline is **81**, measured with `dotnet build "sources/IcyUI.sln" --no-incremental`. It must not grow.
- **Tests:** the baseline is **1293**, all passing. `dotnet test` prints "Passed!" even if the host crashes; always read the Total.
- Test strings that span lines use `.ReplaceLineEndings("\n")`.
- Commit after every task on `platform-independent`, ending the message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- A child's container is always its **markup** parent mapped to the live object in the same load scope, never `UIElement.Parent`.

## Review Focus

1. **A stretched element resized from its edge** (the default alignment for most elements). Writing `Width` onto a `Stretch` element turns it into a centered one in IcyUI's arrange, so the element would jump. Expect the edge to follow the pointer with no jump. Test: Task 6, `ResizingAStretchedAxis_MovesTheMarginNotTheSize`.
2. **Moving an element out of a Grid into a StackPanel.** Expect `Grid.Row`/`Grid.Column`/spans removed from its markup, since they mean nothing in the new parent. Test: Task 7, `MovingOutOfAGrid_DropsTheGridAttributes`.
3. **A gesture that fails half way** (a `MoveElement` that succeeds followed by an attribute edit that fails). Expect the whole gesture rolled back and no undo entry left behind. Test: Task 7, `AFailingEditInsideAGesture_RollsTheWholeGestureBack`.
4. **The editor's key bindings next to a game's own binding on the same gesture.** Expect the editor to win in Edit mode, the game to get the key in Interact mode, and the game's binding to survive detaching. Test: Task 3, `HandledCommand_WinsAndStopsDispatch`, `HandledCommand_FallsThroughWhenItCantExecute`; Task 8, `Dispose_LeavesTheGamesOwnBindingsInPlace`.
5. **A page popup (a ComboBox dropdown) while editing.** Expect its content to be selectable, while the editor's own layers never are. Test: Task 4, `HitTest_SkipsTheEditorsOwnLayersButNotPagePopups`.

## Deviations from the spec (decided while planning)

1. **Strategies work in container-local space**, not surface space. `UIElement.PointToLocal` already composes the canvas pan/zoom, `EffectiveScale` and every ancestor transform, so strategies never need canvas internals. Indicators are returned in container-local space; 10.3b maps them to the surface with `PointToSurface`.
2. **The margin table follows IcyUI's arrange** (`UIElement.CalculateLocation`): `Center`, and `Stretch` with an explicit size, position at `slack / 2 + Margin.Left` and ignore `Margin.Right`. Moves are: Left `L += dx`; Right `R -= dx`; Center or sized Stretch `L += dx`; unsized Stretch `L += dx, R -= dx`.
3. **A stretched axis resizes through the margin (or the Grid span), not `Width`/`Height`.** Writing a size onto an unsized `Stretch` axis turns it into `Center`, which makes the element jump. Dragging that axis's edge moves the margin on that side instead; in a Grid it changes the span only.
4. **Two more small core changes:** `Canvas.HitTest(Point, Func<UIElement, bool>?)` (the session must hit-test past its own layers but not past page popups), and `UIElement.PointToSurface` no longer applies the canvas content transform to elements inside an overlay (a bug: overlay content is already in surface space).
5. **`IsKeyboardNavigationEnabled` gates the canvas's own navigation handlers** (`FocusNext`, `FocusPrevious`, `CloseModal`). Directional moves and `SelectElement` are handled by focused controls (`Selector`), which the session silences by clearing focus in Edit mode.
6. **Leaving a container drops its own attributes.** A strategy lists the attributes it owns (`Grid.Row` and the like); a move into a different container clears the old container's owned attributes.

---

### Task 1: `Grid.RowSpan` and `Grid.ColumnSpan`

**Files:**
- Modify: `sources/IcyUI/UI/Controls/Grid.cs`
- Test: `sources/IcyUI.Tests/Controls/GridSpanTests.cs`

**Interfaces:**
- Produces: `public static int Grid.GetRowSpan(UIElement)`, `Grid.SetRowSpan(UIElement, int)`, `Grid.GetColumnSpan(UIElement)`, `Grid.SetColumnSpan(UIElement, int)`; markup attributes `Grid.RowSpan`, `Grid.ColumnSpan`. Getters never return less than 1. Task 6 uses all four.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Controls/GridSpanTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Markup;
using Icy.Tests.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class GridSpanTests
    {
        [Fact]
        public void ColumnSpan_ArrangesAcrossTheSpannedTracks()
        {
            Grid grid = CreateGrid(columns: [50f, 50f, 50f]);
            var child = new UIElement();
            Grid.SetColumnSpan(child, 2);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 150, 40));

            Assert.Equal(new Rectangle(0, 0, 100, 40), child.ActualBounds);
        }

        [Fact]
        public void RowSpan_ArrangesAcrossTheSpannedTracks()
        {
            var grid = new Grid();
            for (int i = 0; i < 3; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = 20f });
            var child = new UIElement();
            Grid.SetRow(child, 1);
            Grid.SetRowSpan(child, 2);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 100, 60));

            Assert.Equal(new Rectangle(0, 20, 100, 40), child.ActualBounds);
        }

        [Fact]
        public void ASpanPastTheLastTrack_IsClamped()
        {
            Grid grid = CreateGrid(columns: [50f, 50f, 50f]);
            var child = new UIElement();
            Grid.SetColumn(child, 1);
            Grid.SetColumnSpan(child, 5);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 150, 40));

            Assert.Equal(new Rectangle(50, 0, 100, 40), child.ActualBounds);
        }

        [Fact]
        public void ASpanBelowOne_IsTreatedAsOne()
        {
            Grid grid = CreateGrid(columns: [50f, 50f]);
            var child = new UIElement();
            Grid.SetColumnSpan(child, 0);
            grid.Children.Add(child);

            grid.Arrange(new Rectangle(0, 0, 100, 40));

            Assert.Equal(1, Grid.GetColumnSpan(child));
            Assert.Equal(50, child.ActualBounds.Width);
        }

        [Fact]
        public void AutoTracks_GrowEvenlyForASpanningChild()
        {
            Grid grid = CreateGrid(columns: [GridLength.Auto, GridLength.Auto]);
            grid.Children.Add(new UIElement { Width = 30, Height = 10 });
            var wide = new UIElement { Width = 100, Height = 10 };
            Grid.SetColumnSpan(wide, 2);
            grid.Children.Add(wide);

            grid.Arrange(new Rectangle(0, 0, 300, 40));

            // Single-span children size the tracks first (30, 0); the spanning child's deficit of 70 is split evenly.
            Assert.Equal(65, grid.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(35, grid.ColumnDefinitions[1].ActualWidth);
        }

        [Fact]
        public void PixelTracks_DontGrowForASpanningChild()
        {
            Grid grid = CreateGrid(columns: [40f, GridLength.Auto]);
            var wide = new UIElement { Width = 100, Height = 10 };
            Grid.SetColumnSpan(wide, 2);
            grid.Children.Add(wide);

            grid.Arrange(new Rectangle(0, 0, 300, 40));

            Assert.Equal(40, grid.ColumnDefinitions[0].ActualWidth);
            Assert.Equal(60, grid.ColumnDefinitions[1].ActualWidth);
        }

        [Fact]
        public void Markup_SetsTheSpans()
        {
            var loader = new MarkupLoader(MarkupLoadObserverTests.CreateConfiguration());

            UIElement root = loader.Load("<Grid><Border x:Name=\"b\" Grid.RowSpan=\"3\" Grid.ColumnSpan=\"2\"/></Grid>");

            var border = (Border)MarkupNameScope.GetScope(root)!.Find("b")!;
            Assert.Equal(3, Grid.GetRowSpan(border));
            Assert.Equal(2, Grid.GetColumnSpan(border));
        }

        private static Grid CreateGrid(GridLength[] columns)
        {
            var grid = new Grid();
            foreach (GridLength width in columns)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            return grid;
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~GridSpanTests"`
Expected: the build FAILS (`SetColumnSpan`, `SetRowSpan`, `GetColumnSpan`, `GetRowSpan` not found).

- [ ] **Step 3: Implement**

In `Grid.cs`:

1. Replace the class remarks' last sentence ("Row/column spanning isn't supported in v1 - each child occupies exactly one cell.") with: "A child spans <c>RowSpan</c>×<c>ColumnSpan</c> cells (<see cref="GetRowSpan"/>, <see cref="GetColumnSpan"/>), clamped to the tracks that exist."
2. Add next to the existing `[AttachedProperty]` lines:

```csharp
    [AttachedProperty(nameof(GetRowSpan), nameof(SetRowSpan), PropertyName = "RowSpan")]
    [AttachedProperty(nameof(GetColumnSpan), nameof(SetColumnSpan), PropertyName = "ColumnSpan")]
```

3. Add after `GetRow` (keeping getters before setters, as the file does):

```csharp
        /// <summary>
        /// Gets how many columns <paramref name="element"/> spans within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to get the attached value for.</param>
        /// <returns>The column span, at least <c>1</c>. Defaults to <c>1</c>.</returns>
        public static int GetColumnSpan(UIElement element) => Math.Max(AttachedProperties.GetValue(element, "ColumnSpan", 1), 1);

        /// <summary>
        /// Gets how many rows <paramref name="element"/> spans within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to get the attached value for.</param>
        /// <returns>The row span, at least <c>1</c>. Defaults to <c>1</c>.</returns>
        public static int GetRowSpan(UIElement element) => Math.Max(AttachedProperties.GetValue(element, "RowSpan", 1), 1);
```

and after `SetRow`:

```csharp
        /// <summary>
        /// Sets how many columns <paramref name="element"/> spans within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to set the attached value for.</param>
        /// <param name="value">The column span. Values below <c>1</c> are treated as <c>1</c>; a span past the last column is clamped.</param>
        public static void SetColumnSpan(UIElement element, int value)
        {
            AttachedProperties.SetValue(element, "ColumnSpan", value);
            (element.Parent as Grid)?.InvalidateMeasure();
        }

        /// <summary>
        /// Sets how many rows <paramref name="element"/> spans within its parent <see cref="Grid"/>.
        /// </summary>
        /// <param name="element">The element to set the attached value for.</param>
        /// <param name="value">The row span. Values below <c>1</c> are treated as <c>1</c>; a span past the last row is clamped.</param>
        public static void SetRowSpan(UIElement element, int value)
        {
            AttachedProperties.SetValue(element, "RowSpan", value);
            (element.Parent as Grid)?.InvalidateMeasure();
        }
```

4. In `ArrangeContent`, replace the cell computation:

```csharp
                int column = ClampedTrack(GetColumn(child), colSizes.Length);
                int row = ClampedTrack(GetRow(child), rowSizes.Length);
                int columnSpan = ClampedSpan(column, GetColumnSpan(child), colSizes.Length);
                int rowSpan = ClampedSpan(row, GetRowSpan(child), rowSizes.Length);
                Rectangle cell = new(
                    content.X + (int)colOffsets[column],
                    content.Y + (int)rowOffsets[row],
                    (int)SpanSize(colSizes, column, columnSpan),
                    (int)SpanSize(rowSizes, row, rowSpan));
```

(`colSizes.Length` equals `Math.Max(ColumnDefinitions.Count, 1)`, so `ClampedTrack` keeps its meaning.)

5. Add the helpers after `ClampedTrack`:

```csharp
        private static int ClampedSpan(int start, int span, int trackCount) => Math.Clamp(span, 1, Math.Max(trackCount - start, 1));

        private static float SpanSize(float[] sizes, int start, int span)
        {
            float total = 0;
            for (int i = start; i < start + span; i++)
                total += sizes[i];
            return total;
        }
```

6. In `MaxChildTrackSize`, skip spanning children (they're distributed afterwards). After the `index != trackIndex` check add:

```csharp
                int span = isColumn ? GetColumnSpan(child) : GetRowSpan(child);
                int trackCount = Math.Max(isColumn ? ColumnDefinitions.Count : RowDefinitions.Count, 1);
                if (ClampedSpan(index, span, trackCount) > 1)
                    continue;
```

7. In `ResolveTracks`, right after the first loop that fills `sizes` (before `if (availableSpace is float available)`), add `DistributeSpanningDeficits(sizes, TrackLength, isColumn);`, and add the method after `MaxChildTrackSize`:

```csharp
        /// <summary>
        /// Grows the Auto tracks under each spanning child whose desired size exceeds its spanned tracks, splitting the
        /// deficit evenly between them. Pixel and Star tracks never grow for a spanning child.
        /// </summary>
        private void DistributeSpanningDeficits(float[] sizes, Func<int, GridLength> lengths, bool isColumn)
        {
            foreach (UIElement child in Children)
            {
                if (!child.IsVisible)
                    continue;

                int start = ClampedTrack(isColumn ? GetColumn(child) : GetRow(child), sizes.Length);
                int span = ClampedSpan(start, isColumn ? GetColumnSpan(child) : GetRowSpan(child), sizes.Length);
                if (span < 2)
                    continue;

                int autoCount = 0;
                for (int i = start; i < start + span; i++)
                {
                    if (lengths(i).UnitType == GridUnitType.Auto)
                        autoCount++;
                }

                if (autoCount == 0)
                    continue;

                Size desired = child.Measure();
                float wanted = isColumn ? desired.Width + child.Margin.Width : desired.Height + child.Margin.Height;
                float deficit = wanted - SpanSize(sizes, start, span);
                if (deficit <= 0)
                    continue;

                float share = deficit / autoCount;
                for (int i = start; i < start + span; i++)
                {
                    if (lengths(i).UnitType == GridUnitType.Auto)
                        sizes[i] += share;
                }
            }
        }
```

`ResolveTracks` declares `TrackLength` as a local function; pass it as the `lengths` argument.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Grid"`
Expected: PASS, `GridSpanTests` Total 7, and every existing `GridTests` test still passes.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1300, no failures), then `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep "Warning(s)"` (81).

```bash
git add sources/IcyUI/UI/Controls/Grid.cs sources/IcyUI.Tests/Controls/GridSpanTests.cs
git commit -m "Add Grid.RowSpan and Grid.ColumnSpan

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Canvas support: `IsMouseOverGUI`, `IsKeyboardNavigationEnabled`, filtered `HitTest`, overlay `PointToSurface`

**Files:**
- Modify: `sources/IcyUI/UI/Canvas.cs`, `sources/IcyUI/UI/UIElement.cs` (`PointToSurface`)
- Test: `sources/IcyUI.Tests/UI/CanvasEditorSupportTests.cs`

**Interfaces:**
- Produces: `public bool Canvas.IsMouseOverGUI { get; private set; }` (now computed); `public bool Canvas.IsKeyboardNavigationEnabled { get; set; }` (default `true`); `public UIElement? Canvas.HitTest(Point screenPoint, Func<UIElement, bool>? includeOverlay)`. Task 4 uses the last two.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/UI/CanvasEditorSupportTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasEditorSupportTests
    {
        [Fact]
        public void IsMouseOverGUI_FollowsTheHitTest()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            canvas.Add(Box(0, 0, 100, 100));

            input.Mouse.MouseInfo = new MouseInfo(new Point(50, 50));
            canvas.Render();
            Assert.True(canvas.IsMouseOverGUI);

            input.Mouse.MouseInfo = new MouseInfo(new Point(500, 500));
            canvas.Render();
            Assert.False(canvas.IsMouseOverGUI);
        }

        [Fact]
        public void IsMouseOverGUI_IsTrueOverAnOverlay()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            canvas.AddOverlay(Box(400, 400, 50, 50));

            input.Mouse.MouseInfo = new MouseInfo(new Point(420, 420));
            canvas.Render();

            Assert.True(canvas.IsMouseOverGUI);
        }

        [Fact]
        public void HitTest_WithAnOverlayFilter_SkipsTheFilteredOverlays()
        {
            (Canvas canvas, _) = CreateCanvas();
            UIElement page = Box(0, 0, 100, 100);
            canvas.Add(page);
            UIElement cover = Box(0, 0, 800, 600);
            canvas.AddOverlay(cover);
            canvas.Render();

            Assert.Same(cover, canvas.HitTest(new Point(10, 10)));
            Assert.Same(page, canvas.HitTest(new Point(10, 10), overlay => overlay != cover));
        }

        [Fact]
        public void KeyboardNavigationDisabled_IgnoresFocusMovesAndCloseModal()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            var first = new UIElement { IsFocusable = true, Width = 10, Height = 10 };
            canvas.Add(first);
            canvas.Render();

            canvas.IsKeyboardNavigationEnabled = false;
            input.Events.Navigation.RaiseFocusNext();
            Assert.Null(canvas.FocusedElement);

            canvas.IsKeyboardNavigationEnabled = true;
            input.Events.Navigation.RaiseFocusNext();
            Assert.Same(first, canvas.FocusedElement);
        }

        [Fact]
        public void PointToSurface_ForOverlayContent_IgnoresTheCanvasTransform()
        {
            (Canvas canvas, _) = CreateCanvas();
            canvas.Offset = new Vector2(50, 0);
            UIElement popup = Box(10, 20, 30, 30);
            canvas.AddOverlay(popup);
            canvas.Render();

            Assert.Equal(new Point(10, 20), popup.PointToSurface(Vector2.Zero));
        }

        private static UIElement Box(int x, int y, int width, int height) => new()
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(x, y, 0, 0),
        };

        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var canvas = new Canvas(new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()))
            {
                IsInputEnabled = true,
                IsVisible = true,
            };
            return (canvas, input);
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasEditorSupportTests"`
Expected: the build FAILS (`IsKeyboardNavigationEnabled` and the two-argument `HitTest` not found).

- [ ] **Step 3: Implement**

In `Canvas.cs`:

1. Replace `IsMouseOverGUI`:

```csharp
        /// <summary>
        /// Gets a value indicating whether the mouse is over anything this canvas draws: a root element or an overlay
        /// that hit-tests at the mouse position.
        /// </summary>
        /// <remarks>
        /// Updated once per <see cref="Render"/> while <see cref="IsInputEnabled"/> is <see langword="true"/>. A game checks
        /// it to decide whether its own scene should ignore the mouse this frame. A full-surface overlay, such as the
        /// design-time editor's capture layer, makes it <see langword="true"/> everywhere.
        /// </remarks>
        public bool IsMouseOverGUI { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this canvas acts on keyboard and gamepad navigation: moving focus with
        /// <see cref="Input.Events.INavigationEvents.FocusNext"/>/<see cref="Input.Events.INavigationEvents.FocusPrevious"/>
        /// and closing focus scopes with <see cref="Input.Events.INavigationEvents.CloseModal"/>.
        /// </summary>
        /// <remarks>
        /// <see langword="true"/> by default. Tools that take over the keyboard (the design-time editor in Edit mode) and
        /// game states such as cutscenes set it to <see langword="false"/>. Controls that handle navigation themselves
        /// while focused (a <c>ComboBox</c>, say) aren't affected; clear <see cref="FocusedElement"/> to silence them.
        /// </remarks>
        public bool IsKeyboardNavigationEnabled { get; set; } = true;
```

2. Replace the body of `HitTest(Point screenPoint)` with `=> HitTest(screenPoint, includeOverlay: null);` (keep its docs), and add the overload after it:

```csharp
        /// <summary>
        /// Determines which element is under the specified point, testing only the overlays <paramref name="includeOverlay"/>
        /// accepts, then the root elements.
        /// </summary>
        /// <param name="screenPoint">A point in screen/window space (the same space pointer/touch positions arrive in).</param>
        /// <param name="includeOverlay">
        /// Decides, per entry of <see cref="Overlays"/>, whether it takes part; <see langword="null"/> tests every overlay.
        /// Tools use it to look past their own overlays while still seeing the page's popups.
        /// </param>
        /// <returns>The topmost hit-testable element under the point, or <see langword="null"/> if none is.</returns>
        public UIElement? HitTest(Point screenPoint, Func<UIElement, bool>? includeOverlay)
        {
            // Overlays are tested in surface space - the physical point divided by EffectiveScale (last-added =
            // topmost = checked first, matching Overlays' own draw order). Unlike rootElements, they're arranged
            // against SurfaceSize without this canvas's own content transform (see RenderVisual/UpdateLayout), so
            // hit-testing must match rather than going through ScreenToCanvasSpace.
            Vector2 surfacePoint = ScreenToSurface(screenPoint);
            for (int i = overlayElements.Count - 1; i >= 0; i--)
            {
                if (includeOverlay != null && !includeOverlay(overlayElements[i]))
                    continue;

                UIElement? hit = overlayElements[i].HitTest(surfacePoint);
                if (hit != null)
                    return hit;
            }

            Vector2 canvasLocalPoint = ScreenToCanvasSpace(screenPoint);
            foreach (UIElement element in rootElements.OrderByDescending(e => e.ZIndex))
            {
                UIElement? hit = element.HitTest(canvasLocalPoint);
                if (hit != null)
                    return hit;
            }

            return null;
        }
```

3. In `EnsureInputRoutingInitialized`, gate the three navigation handlers:

```csharp
            events.Navigation.FocusNext += (_, _) =>
            {
                if (IsKeyboardNavigationEnabled)
                    MoveFocus(forward: true);
            };
            events.Navigation.FocusPrevious += (_, _) =>
            {
                if (IsKeyboardNavigationEnabled)
                    MoveFocus(forward: false);
            };
```

and at the top of `OnCloseModal` add `if (!IsKeyboardNavigationEnabled) return;` (as two lines, matching the file's guard style).

4. In `UpdateHover`, store the flag before the early return:

```csharp
            UIElement? hit = HitTest(Configuration.Input.Mouse.MouseInfo.Position);
            IsMouseOverGUI = hit != null;
            if (hit == hoveredElement)
                return;
```

In `UIElement.PointToSurface`, overlay content is already in surface space. Replace the method body with:

```csharp
            if (Canvas == null)
                return Point.Empty;

            Vector2 point = localPoint;
            UIElement root = this;
            for (UIElement? element = this; element != null; element = element.Parent)
            {
                if (element.IsTransformInvalid)
                    element.UpdateTransformMatrix();
                point = element.layoutTransform.Apply(point);
                root = element;
            }

            // Overlays (popups) are laid out in surface space already; only page content goes through the canvas's own pan/rotate/zoom.
            Vector2 surfacePoint = Canvas.IsOverlay(root) ? point : Canvas.CanvasToSurfaceSpace(point);
            return new Point((int)surfacePoint.X, (int)surfacePoint.Y);
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.UI"`
Expected: PASS, `CanvasEditorSupportTests` Total 5, and every other UI test still passes.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1305, no failures), then the warning count (81).

```bash
git add sources/IcyUI/UI/Canvas.cs sources/IcyUI/UI/UIElement.cs sources/IcyUI.Tests/UI/CanvasEditorSupportTests.cs
git commit -m "Make IsMouseOverGUI real, and add canvas support for editor overlays

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: First-wins command dispatch and single-command unregister

**Files:**
- Create: `sources/IcyUI/Input/KeyCommandTable.cs`
- Modify: `sources/IcyUI/Input/IInputEventSystem.cs`, `sources/IcyUI/Input/InputEventSystem.cs`, `sources/IcyUI.Tests/Input/FakeInputSystem.cs` (`FakeInputEventSystem`)
- Test: `sources/IcyUI.Tests/Input/KeyCommandTableTests.cs`

**Interfaces:**
- Produces:
  - `void IInputEventSystem.RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null, bool handlesGesture = false)`;
  - `void IInputEventSystem.UnregisterCommand(ICommand command, KeyGesture gesture)` (new overload; the gesture-wide one stays);
  - `internal sealed class KeyCommandTable` with `Add(ICommand, KeyGesture, object?, bool)`, `Remove(ICommand, KeyGesture)`, `RemoveGesture(KeyGesture)`, `bool Dispatch(KeyGesture)` (returns whether anything executed);
  - tests: `FakeInputEventSystem` keeps a real `KeyCommandTable` and exposes `bool RaiseGesture(KeyGesture)`. Task 8 uses it.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Input/KeyCommandTableTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input;
using Icy.Input.Devices;
using Xunit;

namespace Icy.Tests.Input
{
    public class KeyCommandTableTests
    {
        private static readonly KeyGesture Delete = new(Keys.Delete);

        [Fact]
        public void OrdinaryCommands_AllRun()
        {
            var table = new KeyCommandTable();
            int first = 0, second = 0;
            table.Add(new FakeCommand(() => true, () => first++), Delete, null, handlesGesture: false);
            table.Add(new FakeCommand(() => true, () => second++), Delete, null, handlesGesture: false);

            Assert.True(table.Dispatch(Delete));

            Assert.Equal(1, first);
            Assert.Equal(1, second);
        }

        [Fact]
        public void HandledCommand_WinsAndStopsDispatch()
        {
            var table = new KeyCommandTable();
            int game = 0, editor = 0;
            table.Add(new FakeCommand(() => true, () => game++), Delete, null, handlesGesture: false);
            table.Add(new FakeCommand(() => true, () => editor++), Delete, null, handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal(1, editor);
            Assert.Equal(0, game);
        }

        [Fact]
        public void HandledCommand_FallsThroughWhenItCantExecute()
        {
            var table = new KeyCommandTable();
            int game = 0, editor = 0;
            table.Add(new FakeCommand(() => true, () => game++), Delete, null, handlesGesture: false);
            table.Add(new FakeCommand(() => false, () => editor++), Delete, null, handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal(0, editor);
            Assert.Equal(1, game);
        }

        [Fact]
        public void TheLatestHandledRegistration_GoesFirst()
        {
            var table = new KeyCommandTable();
            var order = new List<string>();
            table.Add(new FakeCommand(() => true, () => order.Add("older")), Delete, null, handlesGesture: true);
            table.Add(new FakeCommand(() => true, () => order.Add("newer")), Delete, null, handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal(["newer"], order);
        }

        [Fact]
        public void RemovingOneCommand_KeepsTheOthersOnTheGesture()
        {
            var table = new KeyCommandTable();
            int game = 0;
            var editor = new FakeCommand(() => true, () => { });
            table.Add(new FakeCommand(() => true, () => game++), Delete, null, handlesGesture: false);
            table.Add(editor, Delete, null, handlesGesture: true);

            table.Remove(editor, Delete);
            table.Dispatch(Delete);

            Assert.Equal(1, game);
        }

        [Fact]
        public void TheArgument_IsPassedToTheCommand()
        {
            var table = new KeyCommandTable();
            object? received = null;
            var command = new ArgumentCommand(x => received = x);
            table.Add(command, Delete, "payload", handlesGesture: true);

            table.Dispatch(Delete);

            Assert.Equal("payload", received);
        }

        private sealed class ArgumentCommand(Action<object?> execute) : System.Windows.Input.ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute(parameter);
        }
    }
}
```

Check `FakeCommand`'s constructor in `sources/IcyUI.Tests` first (`grep -rn "class FakeCommand" sources/IcyUI.Tests`). If it doesn't take an execute action, add an optional `Action? execute = null` parameter that `Execute` invokes; keep existing callers compiling.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~KeyCommandTableTests"`
Expected: the build FAILS (`KeyCommandTable` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI/Input/KeyCommandTable.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;
using Icy.Input.Devices;

namespace Icy.Input
{
    /// <summary>
    /// Maps key gestures to commands. Commands that handle their gesture run first, newest first, and the first one
    /// that can execute stops dispatch; when none can, every ordinary command runs.
    /// </summary>
    internal sealed class KeyCommandTable
    {
        private readonly Dictionary<KeyGesture, List<Registration>> registrations = [];

        public void Add(ICommand command, KeyGesture gesture, object? argument, bool handlesGesture)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (!registrations.TryGetValue(gesture, out List<Registration>? list))
                registrations[gesture] = list = [];

            list.Add(new Registration(command, argument, handlesGesture));
        }

        public void Remove(ICommand command, KeyGesture gesture)
        {
            if (registrations.TryGetValue(gesture, out List<Registration>? list))
            {
                list.RemoveAll(x => ReferenceEquals(x.Command, command));
                if (list.Count == 0)
                    registrations.Remove(gesture);
            }
        }

        public void RemoveGesture(KeyGesture gesture) => registrations.Remove(gesture);

        public bool Dispatch(KeyGesture gesture)
        {
            if (!registrations.TryGetValue(gesture, out List<Registration>? list))
                return false;

            // A command may unregister commands while running, so dispatch over a copy.
            Registration[] snapshot = [.. list];
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                Registration handler = snapshot[i];
                if (handler.HandlesGesture && handler.Command.CanExecute(handler.Argument))
                {
                    handler.Command.Execute(handler.Argument);
                    return true;
                }
            }

            bool executed = false;
            foreach (Registration ordinary in snapshot)
            {
                if (!ordinary.HandlesGesture && ordinary.Command.CanExecute(ordinary.Argument))
                {
                    ordinary.Command.Execute(ordinary.Argument);
                    executed = true;
                }
            }

            return executed;
        }

        private sealed record Registration(ICommand Command, object? Argument, bool HandlesGesture);
    }
}
```

In `IInputEventSystem`, replace the two command members with:

```csharp
        /// <summary>
        /// Registers a command to invoke when the specified key combination is pressed.
        /// </summary>
        /// <param name="command">Command instance to register.</param>
        /// <param name="gesture">Keys combination to trigger the command.</param>
        /// <param name="argument">Argument to pass to the command when the gesture is pressed.</param>
        /// <param name="handlesGesture">
        /// <see langword="true"/> to let this command take the gesture over: commands registered this way are tried before
        /// the others, the most recently registered first, and the first whose <see cref="ICommand.CanExecute(object?)"/>
        /// returns <see langword="true"/> runs alone. When none of them can execute, every other command for the gesture
        /// runs as usual. <see langword="false"/> (the default) runs the command together with the gesture's other ordinary
        /// commands.
        /// </param>
        void RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null, bool handlesGesture = false);

        /// <summary>
        /// Unregisters every command bound to the specified key combination.
        /// </summary>
        /// <param name="gesture">Keyboard combination to remove the commands for.</param>
        void UnregisterCommand(KeyGesture gesture);

        /// <summary>
        /// Unregisters one command from the specified key combination, leaving the gesture's other commands in place.
        /// </summary>
        /// <param name="command">The command to remove.</param>
        /// <param name="gesture">The key combination it was registered for.</param>
        void UnregisterCommand(ICommand command, KeyGesture gesture);
```

In `InputEventSystem`:
- `RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null, bool handlesGesture = false) => listener.Commands.Add(command, gesture, argument, handlesGesture);`
- `UnregisterCommand(KeyGesture gesture) => listener.Commands.RemoveGesture(gesture);`
- add `public void UnregisterCommand(ICommand command, KeyGesture gesture) => listener.Commands.Remove(command, gesture);` with `/// <inheritdoc/>`.
- In `KeyboardListener`, replace the `commands` dictionary, `AddCommand` and `RemoveGesture` with `public KeyCommandTable Commands { get; } = new();`, and the body of `OnKeyDown` after the null check with `Commands.Dispatch(new KeyGesture(e.Data, keyboard.ModifierKeys));`.

In `FakeInputEventSystem` (tests), replace the two command methods with:

```csharp
        private readonly KeyCommandTable commands = new();

        public void RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null, bool handlesGesture = false) =>
            commands.Add(command, gesture, argument, handlesGesture);

        public void UnregisterCommand(KeyGesture gesture) => commands.RemoveGesture(gesture);

        public void UnregisterCommand(ICommand command, KeyGesture gesture) => commands.Remove(command, gesture);

        /// <summary>
        /// Presses <paramref name="gesture"/> as the real keyboard listener would.
        /// </summary>
        public bool RaiseGesture(KeyGesture gesture) => commands.Dispatch(gesture);
```

(Put the field at the top of the class, with `using Icy.Input;` if it's missing.)

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Input"`
Expected: PASS, `KeyCommandTableTests` Total 6.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1311, no failures), then the warning count (81). Both sample hosts must still compile (they call `RegisterCommand` with two arguments).

```bash
git add sources/IcyUI/Input sources/IcyUI.Tests/Input
git commit -m "Let commands take over a key gesture, and unregister single commands

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `EditorSession`: attach, modes, hit resolution, selection

**Files:**
- Create: `sources/IcyUI.Design/Editor/EditorMode.cs`, `sources/IcyUI.Design/Editor/EditorSelection.cs`, `sources/IcyUI.Design/Editor/EditorSession.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/EditorTestHost.cs`, `sources/IcyUI.Tests/Design/Editor/EditorSessionTests.cs`

**Interfaces:**
- Consumes: `Canvas.HitTest(Point, Func<UIElement, bool>?)`, `Canvas.IsKeyboardNavigationEnabled` (Task 2); `DesignSession.FindDocument`, `DesignDocument.IsEditable(ElementSyntax)` (internal), `DesignDocument.Map` (internal), `DesignDocument.SubtreeReplaced`, `DesignDocument.Changed`, `DesignDocument.IsInSync`, `DesignDocument.LiveErrors`, `DesignDocument.SyncStateChanged`.
- Produces (namespace `Icy.Design.Editor`):
  - `public enum EditorMode { Edit, Interact }`;
  - `public sealed record EditorSelection(DesignDocument Document, NodeId Node, UIElement Instance)`;
  - `public sealed class EditorSession : IDisposable` with `static EditorSession Attach(DesignSession design, Canvas canvas)`, `DesignSession Design`, `Canvas Canvas`, `EditorMode Mode { get; set; }`, `EditorSelection? Selection`, `bool IsBlocked`, `string? BlockedReason`, events `ModeChanged`, `SelectionChanged`, `BlockedChanged`, `UIElement? HitTest(Point screenPoint)`, `UIElement? ResolveSelectable(UIElement? hit)`, `bool Select(UIElement instance)`, `void Select(DesignDocument document, NodeId node)`, `void Clear()`, `void Dispose()`;
  - internal: `ISet<UIElement> OwnLayers` (10.3b adds its overlays here), `UIElement? FindLogicalParent(EditorSelection selection)`, `ElementSyntax? Syntax(EditorSelection selection)`, `MarkupLoadScope? ScopeOf(UIElement instance)`, `UIElement? FindInstance(DesignDocument document, NodeId node, MarkupLoadScope scope)`, `void ThrowIfDisposed()`.
  - tests: `EditorTestHost` (a `DesignTestHost` plus a rendered `Canvas` with `FakeInputSystem`), used by Tasks 5–8.

- [ ] **Step 1: Write the test host and the failing tests**

`sources/IcyUI.Tests/Design/Editor/EditorTestHost.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Design;
using Icy.Design.Editor;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;

namespace Icy.Tests.Design.Editor
{
    /// <summary>
    /// A tracked page on a rendered 800×600 canvas with fake input, and an attached <see cref="EditorSession"/>.
    /// </summary>
    internal sealed class EditorTestHost : IDisposable
    {
        public EditorTestHost(string markup)
        {
            Input = new FakeInputSystem();
            Configuration = new IcyConfiguration(Input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            Configuration.Types.Markup.RegisterShortName<PickyPanel>();
            Design = DesignSession.Attach(Configuration);
            Root = new MarkupLoader(Configuration).Load(markup.ReplaceLineEndings("\n"), "page.xml");
            Document = Design.FindDocument(Root, out _)!;
            Canvas = new Canvas(Configuration) { IsInputEnabled = true, IsVisible = true };
            Canvas.Add(Root);
            Canvas.Render();
            Session = EditorSession.Attach(Design, Canvas);
        }

        public FakeInputSystem Input { get; }

        public IcyConfiguration Configuration { get; }

        public DesignSession Design { get; }

        public UIElement Root { get; }

        public DesignDocument Document { get; }

        public Canvas Canvas { get; }

        public EditorSession Session { get; }

        public T Named<T>(string name)
            where T : UIElement =>
            (T)(MarkupNameScope.GetScope(Root)!.Find(name) ?? throw new InvalidOperationException($"No element named '{name}'."));

        public NodeId IdOf(UIElement element) =>
            Design.FindDocument(element, out NodeId id) != null ? id : throw new InvalidOperationException("Not tracked.");

        /// <summary>
        /// The screen point at <paramref name="element"/>'s local position (<paramref name="x"/>, <paramref name="y"/>).
        /// </summary>
        public Point At(UIElement element, int x = 1, int y = 1) => element.PointToScreen(new System.Numerics.Vector2(x, y));

        public void Render() => Canvas.Render();

        public void Dispose()
        {
            Session.Dispose();
            Design.Dispose();
        }
    }
}
```

`sources/IcyUI.Tests/Design/Editor/EditorSessionTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Editor;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorSessionTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Border x:Name="box" Width="100" Height="40"/>
              <Button x:Name="ok" Width="100" Height="30">OK</Button>
              <Border x:Name="frame" Width="120" Height="50">
                <TextBlock x:Name="inner" Text="Inside"/>
              </Border>
            </StackPanel>
            """;

        [Fact]
        public void Attach_StartsInEditModeWithNoSelection()
        {
            using var host = new EditorTestHost(Page);

            Assert.Equal(EditorMode.Edit, host.Session.Mode);
            Assert.Null(host.Session.Selection);
            Assert.False(host.Canvas.IsKeyboardNavigationEnabled);
        }

        [Fact]
        public void ResolveSelectable_PicksTheOwnerOfTemplateContent()
        {
            using var host = new EditorTestHost(Page);
            var ok = host.Named<Button>("ok");
            UIElement? hit = host.Canvas.HitTest(host.At(ok, 50, 15));

            Assert.NotNull(hit);
            Assert.Same(ok, host.Session.ResolveSelectable(hit));
        }

        [Fact]
        public void ResolveSelectable_PicksTheDeepestTrackedElement()
        {
            using var host = new EditorTestHost(Page);
            var inner = host.Named<TextBlock>("inner");

            Assert.Same(inner, host.Session.ResolveSelectable(host.Canvas.HitTest(host.At(inner))));
        }

        [Fact]
        public void HitTest_SkipsTheEditorsOwnLayersButNotPagePopups()
        {
            using var host = new EditorTestHost(Page);
            var layer = new UIElement { Width = 800, Height = 600 };
            host.Canvas.AddOverlay(layer);
            host.Session.OwnLayers.Add(layer);
            var popup = new UIElement { Width = 50, Height = 50, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(600, 400, 0, 0) };
            host.Canvas.AddOverlay(popup);
            host.Render();

            Assert.Same(host.Named<Border>("box"), host.Session.HitTest(host.At(host.Named<Border>("box"))));
            Assert.Same(popup, host.Session.HitTest(new System.Drawing.Point(610, 410)));
        }

        [Fact]
        public void Select_SetsTheSelectionAndRaisesSelectionChanged()
        {
            using var host = new EditorTestHost(Page);
            var box = host.Named<Border>("box");
            int changed = 0;
            host.Session.SelectionChanged += (_, _) => changed++;

            Assert.True(host.Session.Select(box));

            Assert.Equal(new EditorSelection(host.Document, host.IdOf(box), box), host.Session.Selection);
            Assert.Equal(1, changed);
        }

        [Fact]
        public void Select_RefusesUntrackedElements()
        {
            using var host = new EditorTestHost(Page);

            Assert.False(host.Session.Select(new Border()));
            Assert.Null(host.Session.Selection);
        }

        [Fact]
        public void TheSelection_FollowsARebuild()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("box"));

            // A changed element type rebuilds it in place (10.2), keeping its id.
            host.Document.ApplyText(host.Document.Text.Replace("<Border x:Name=\"box\" Width=\"100\" Height=\"40\"/>", "<Button x:Name=\"box\" Width=\"100\" Height=\"40\"/>", StringComparison.Ordinal));

            Assert.IsType<Button>(host.Session.Selection!.Instance);
            Assert.Same(host.Named<Button>("box"), host.Session.Selection.Instance);
        }

        [Fact]
        public void TheSelection_SurvivesAppliedText()
        {
            using var host = new EditorTestHost(Page);
            var box = host.Named<Border>("box");
            host.Session.Select(box);

            host.Document.ApplyText(host.Document.Text.Replace("Height=\"40\"", "Height=\"45\"", StringComparison.Ordinal));

            Assert.Same(box, host.Session.Selection!.Instance);
        }

        [Fact]
        public void TheSelection_ClearsWhenItsElementIsRemoved()
        {
            using var host = new EditorTestHost(Page);
            var box = host.Named<Border>("box");
            host.Session.Select(box);

            host.Document.Editor.RemoveElement(host.IdOf(box));

            Assert.Null(host.Session.Selection);
        }

        [Fact]
        public void EditMode_ClearsFocus_AndInteractRestoresIt()
        {
            using var host = new EditorTestHost(Page);
            var ok = host.Named<Button>("ok");
            host.Session.Mode = EditorMode.Interact;
            host.Canvas.Focus(ok);

            host.Session.Mode = EditorMode.Edit;
            Assert.Null(host.Canvas.FocusedElement);
            Assert.False(host.Canvas.IsKeyboardNavigationEnabled);

            host.Session.Mode = EditorMode.Interact;
            Assert.Same(ok, host.Canvas.FocusedElement);
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
        }

        [Fact]
        public void Dispose_RestoresNavigationAndFocus()
        {
            var host = new EditorTestHost(Page);
            host.Session.Mode = EditorMode.Interact;
            host.Canvas.Focus(host.Named<Button>("ok"));
            host.Session.Mode = EditorMode.Edit;

            host.Dispose();

            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
            Assert.Same(host.Named<Button>("ok"), host.Canvas.FocusedElement);
        }

        [Fact]
        public void IsBlocked_FollowsTheSelectedDocumentsSyncState()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("box"));
            int changed = 0;
            host.Session.BlockedChanged += (_, _) => changed++;

            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);
            Assert.True(host.Session.IsBlocked);
            Assert.NotNull(host.Session.BlockedReason);

            host.Document.ApplyText(host.Document.Text + "</StackPanel>");
            Assert.False(host.Session.IsBlocked);
            Assert.Equal(2, changed);
        }
    }
}
```

`EditorTestHost` registers `PickyPanel` (Phase 10.2's test type in `Icy.Tests.Design`); add `using Icy.Tests.Design;` if the compiler asks.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorSessionTests"`
Expected: the build FAILS (`Icy.Design.Editor` doesn't exist).

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editor/EditorMode.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor
{
    /// <summary>
    /// What pointer and keyboard input does while an <see cref="EditorSession"/> is attached.
    /// </summary>
    public enum EditorMode
    {
        /// <summary>
        /// The editor owns the input: clicks select, drags move and resize, and the editor's key bindings are active.
        /// The page's controls see no input.
        /// </summary>
        Edit,

        /// <summary>
        /// The page and the game behave normally, so the developer can open popups, switch tabs and so on; the
        /// selection stays visible, and only the mode toggle is bound.
        /// </summary>
        Interact,
    }
}
```

`sources/IcyUI.Design/Editor/EditorSelection.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The element an <see cref="EditorSession"/> has selected.
    /// </summary>
    /// <param name="Document">The design document the element's markup belongs to.</param>
    /// <param name="Node">The element's node in <paramref name="Document"/>.</param>
    /// <param name="Instance">
    /// The live copy that was picked. When the same file is loaded more than once, edits still apply to every copy;
    /// this is the one adorners follow.
    /// </param>
    public sealed record EditorSelection(DesignDocument Document, NodeId Node, UIElement Instance);
}
```

`sources/IcyUI.Design/Editor/EditorSession.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The headless part of the design-time editor for one <see cref="UI.Canvas"/>: the selection, the Edit/Interact
    /// mode, hit resolution, and (through <see cref="Commands"/> and the gesture methods) every edit the editor makes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session works across every <see cref="DesignDocument"/> whose pages are on the canvas. Each gesture and command
    /// becomes <see cref="MarkupEditor"/> operations, so undo, saving and hot reload work as for any other edit.
    /// </para>
    /// <para>
    /// <see cref="Dispose"/> removes only the editor's own footprint: its key bindings, and the focus and navigation state
    /// it changed. Edits are never rolled back; save the documents through <see cref="DesignDocument.Save"/> before or
    /// after detaching.
    /// </para>
    /// <para>The visual frame (<c>EditorFrame</c>) is built on top of a session. Tools such as an outline panel can share the
    /// same session, and therefore the same selection.</para>
    /// </remarks>
    public sealed class EditorSession : IDisposable
    {
        private readonly HashSet<DesignDocument> watched = [];
        private EditorMode mode = EditorMode.Edit;
        private EditorSelection? selection;
        private UIElement? focusBeforeEdit;
        private bool navigationBeforeEdit;
        private bool blocked;
        private bool disposed;

        private EditorSession(DesignSession design, Canvas canvas)
        {
            Design = design;
            Canvas = canvas;
        }

        /// <summary>
        /// Occurs when <see cref="Mode"/> changed.
        /// </summary>
        public event EventHandler? ModeChanged;

        /// <summary>
        /// Occurs when <see cref="Selection"/> changed, including when its live instance was rebuilt.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Occurs when <see cref="IsBlocked"/> or <see cref="BlockedReason"/> changed.
        /// </summary>
        public event EventHandler? BlockedChanged;

        /// <summary>
        /// Gets the design session whose documents this editor edits.
        /// </summary>
        public DesignSession Design { get; }

        /// <summary>
        /// Gets the canvas this editor works on.
        /// </summary>
        public Canvas Canvas { get; }

        /// <summary>
        /// Gets or sets the editor's mode.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>
        /// Entering <see cref="EditorMode.Edit"/> remembers and clears the canvas's focused element, so no text box takes
        /// typing, and sets <see cref="Canvas.IsKeyboardNavigationEnabled"/> to <see langword="false"/>.
        /// </description></item>
        /// <item><description>
        /// Entering <see cref="EditorMode.Interact"/> puts both back; the focus only when that element is still on the canvas.
        /// </description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public EditorMode Mode
        {
            get => mode;
            set
            {
                ThrowIfDisposed();
                if (mode == value)
                    return;

                mode = value;
                if (value == EditorMode.Edit)
                    EnterEdit();
                else
                    LeaveEdit();
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the selected element, or <see langword="null"/>.
        /// </summary>
        public EditorSelection? Selection => selection;

        /// <summary>
        /// Gets a value indicating whether the selected element's document can't take edits right now, because its text
        /// has errors or the live page couldn't follow it (see <see cref="DesignDocument.IsInSync"/>). Selection, undo and
        /// redo still work; gestures and editing commands are refused.
        /// </summary>
        public bool IsBlocked => blocked;

        /// <summary>
        /// Gets why <see cref="IsBlocked"/> is <see langword="true"/>, or <see langword="null"/>.
        /// </summary>
        public string? BlockedReason => !blocked
            ? null
            : selection!.Document.LiveErrors.Count > 0 ? selection.Document.LiveErrors[0].Message : "The markup has errors; fix them first.";

        /// <summary>
        /// Gets the overlays that belong to the editor itself. They never take part in <see cref="HitTest"/>.
        /// </summary>
        internal ISet<UIElement> OwnLayers { get; } = new HashSet<UIElement>();

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="design">The design session tracking the canvas's pages.</param>
        /// <param name="canvas">The canvas to edit.</param>
        /// <returns>The session; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="design"/> or <paramref name="canvas"/> is <see langword="null"/>.</exception>
        public static EditorSession Attach(DesignSession design, Canvas canvas)
        {
            ArgumentNullException.ThrowIfNull(design);
            ArgumentNullException.ThrowIfNull(canvas);

            var session = new EditorSession(design, canvas);
            session.EnterEdit();
            return session;
        }

        /// <summary>
        /// Finds the element under a screen point the way the editor sees the canvas: past the editor's own layers, but
        /// including the page's popups and dialogs.
        /// </summary>
        /// <param name="screenPoint">A point in screen space, where pointer positions arrive.</param>
        /// <returns>The topmost hit element, or <see langword="null"/>.</returns>
        public UIElement? HitTest(Point screenPoint) => Canvas.HitTest(screenPoint, overlay => !OwnLayers.Contains(overlay));

        /// <summary>
        /// Finds the element a click on <paramref name="hit"/> selects: the nearest element, from <paramref name="hit"/>
        /// up through its visual parents, that a tracked document owns and can edit.
        /// </summary>
        /// <param name="hit">The element under the pointer, or <see langword="null"/>.</param>
        /// <returns>The selectable element, or <see langword="null"/> when there's none.</returns>
        /// <remarks>
        /// A control's template parts and an items control's generated containers aren't elements of the document (or
        /// are opaque inside a property element), so a click on them selects the control that owns them.
        /// </remarks>
        public UIElement? ResolveSelectable(UIElement? hit)
        {
            for (UIElement? current = hit; current != null; current = current.Parent)
            {
                if (OwnLayers.Contains(current))
                    return null;
                if (Design.FindDocument(current, out NodeId node) is { } document && document.GetNode(node) is { } element && document.IsEditable(element))
                    return current;
            }

            return null;
        }

        /// <summary>
        /// Selects <paramref name="instance"/>.
        /// </summary>
        /// <param name="instance">A live element.</param>
        /// <returns><see langword="false"/>, changing nothing, when no tracked document can edit it.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public bool Select(UIElement instance)
        {
            ArgumentNullException.ThrowIfNull(instance);
            ThrowIfDisposed();
            if (Design.FindDocument(instance, out NodeId node) is not { } document || document.GetNode(node) is not { } element || !document.IsEditable(element))
                return false;

            SetSelection(new EditorSelection(document, node, instance));
            return true;
        }

        /// <summary>
        /// Selects a document's element, picking its first live copy on this canvas.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="node">The element's node.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The element has no live copy on this canvas, or can't be edited.</exception>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public void Select(DesignDocument document, NodeId node)
        {
            ArgumentNullException.ThrowIfNull(document);
            ThrowIfDisposed();
            UIElement instance = document.GetObjects(node).OfType<UIElement>().FirstOrDefault(x => ReferenceEquals(x.Canvas, Canvas))
                ?? throw new ArgumentException($"Element {node} has no live copy on this canvas.", nameof(node));
            if (!Select(instance))
                throw new ArgumentException($"Element {node} can't be edited.", nameof(node));
        }

        /// <summary>
        /// Clears the selection.
        /// </summary>
        public void Clear() => SetSelection(null);

        /// <summary>
        /// Detaches the editor: restores the canvas's focus and navigation, and stops watching documents. Edits stay.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            if (mode == EditorMode.Edit)
                LeaveEdit();
            SetSelection(null);
            foreach (DesignDocument document in watched)
                Unwatch(document);
            watched.Clear();
            disposed = true;
        }

        internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

        /// <summary>
        /// Gets the selected element's markup.
        /// </summary>
        internal static ElementSyntax? Syntax(EditorSelection selection) => selection.Document.GetNode(selection.Node);

        /// <summary>
        /// Gets the load scope a live instance was built in.
        /// </summary>
        internal static MarkupLoadScope? ScopeOf(DesignDocument document, UIElement instance) =>
            document.Map.TryGetEntry(instance, out var entry) ? entry.Scope : null;

        /// <summary>
        /// Finds <paramref name="node"/>'s live copy in <paramref name="scope"/>.
        /// </summary>
        internal static UIElement? FindInstance(DesignDocument document, NodeId node, MarkupLoadScope scope) =>
            document.FindObject(node, scope) as UIElement;

        /// <summary>
        /// Gets the live object of the selected element's markup parent, in the same load scope: the container that lays it
        /// out, even when a template's presenter is its visual parent.
        /// </summary>
        internal static UIElement? FindLogicalParent(EditorSelection selection)
        {
            if (Syntax(selection)?.Parent is not { } parent
                || selection.Document.GetNodeId(parent) is not NodeId parentId
                || ScopeOf(selection.Document, selection.Instance) is not { } scope)
            {
                return null;
            }

            return FindInstance(selection.Document, parentId, scope);
        }

        private void SetSelection(EditorSelection? value)
        {
            if (Equals(selection, value))
                return;

            selection = value;
            if (value != null && watched.Add(value.Document))
                Watch(value.Document);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            UpdateBlocked();
        }

        private void EnterEdit()
        {
            focusBeforeEdit = Canvas.FocusedElement;
            navigationBeforeEdit = Canvas.IsKeyboardNavigationEnabled;
            Canvas.Focus(null);
            Canvas.IsKeyboardNavigationEnabled = false;
        }

        private void LeaveEdit()
        {
            Canvas.IsKeyboardNavigationEnabled = navigationBeforeEdit;
            if (focusBeforeEdit is { } focus && ReferenceEquals(focus.Canvas, Canvas))
                Canvas.Focus(focus);
            focusBeforeEdit = null;
        }

        private void Watch(DesignDocument document)
        {
            document.SubtreeReplaced += OnSubtreeReplaced;
            document.Changed += OnDocumentChanged;
            document.SyncStateChanged += OnSyncStateChanged;
        }

        private void Unwatch(DesignDocument document)
        {
            document.SubtreeReplaced -= OnSubtreeReplaced;
            document.Changed -= OnDocumentChanged;
            document.SyncStateChanged -= OnSyncStateChanged;
        }

        private void OnSubtreeReplaced(object? sender, SubtreeReplacedEventArgs e)
        {
            if (selection is { } current && current.Node == e.Node && ReferenceEquals(current.Instance, e.OldElement))
                SetSelection(current with { Instance = e.NewElement });
        }

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
            // A removed element (by an edit, undo or applied text) can't stay selected. While the text is malformed the
            // ids still answer for the last valid tree, so the selection survives until the text is fixed.
            if (selection is { } current && ReferenceEquals(sender, current.Document) && current.Document.GetNode(current.Node) == null)
                SetSelection(null);
        }

        private void OnSyncStateChanged(object? sender, EventArgs e) => UpdateBlocked();

        private void UpdateBlocked()
        {
            bool value = selection is { } current && !current.Document.IsInSync;
            if (value == blocked && !value)
                return;

            blocked = value;
            BlockedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

This needs `DesignDocument.FindObject(NodeId, MarkupLoadScope)`, which is already `internal`. `DocumentChangedEventArgs` is public (10.1). `UpdateBlocked` raises `BlockedChanged` on every sync change while blocked, so the reason line updates too.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorSessionTests"`
Expected: PASS, Total 12.

If `IsBlocked_FollowsTheSelectedDocumentsSyncState` counts more than 2 events, the cause is `UpdateBlocked` firing on a sync change that leaves `blocked` false and unchanged; the early return covers that case, so check that the second `ApplyText` really restores the end tag.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1323, no failures), then the warning count (81).

```bash
git add sources/IcyUI.Design/Editor sources/IcyUI.Tests/Design/Editor
git commit -m "Add the headless editor session: modes, hit resolution and selection

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Placement types, registry and the margin fallback

**Files:**
- Create in `sources/IcyUI.Design/Editor/Placement/`: `AttributeEdit.cs`, `ResizeHandle.cs`, `PlacementChild.cs`, `PlacementContext.cs`, `PlacementTarget.cs`, `IPlacementStrategy.cs`, `IResizeOperation.cs`, `LayoutMath.cs`, `AxisKind.cs`, `AxisResize.cs`, `MarkupValues.cs`, `MarginPlacement.cs`, `PlacementRegistry.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/MarginPlacementTests.cs`

**Interfaces:**
- Produces (namespace `Icy.Design.Editor.Placement`, all public unless noted):
  - `readonly record struct AttributeEdit(string Name, string? Value)` (`Value == null` removes the attribute);
  - `[Flags] enum ResizeHandle { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8, TopLeft = Top | Left, TopRight = Top | Right, BottomLeft = Bottom | Left, BottomRight = Bottom | Right }`;
  - `readonly record struct PlacementChild(int Index, UIElement Instance)`;
  - `sealed class PlacementContext` with `UIElement Container`, `UIElement Element`, `IReadOnlyList<PlacementChild> Children`, `int ContentCount`, `Rectangle ElementBounds`, `Rectangle ContainerContent`, `bool IsCurrentContainer`, and static `Rectangle ToLocal(UIElement container, Rectangle absolute)`;
  - `sealed record PlacementTarget(int Index, IReadOnlyList<AttributeEdit> Edits, RectangleF Indicator, bool IndicatorIsLine)`;
  - `interface IResizeOperation { IReadOnlyList<AttributeEdit> Update(Vector2 delta); }`;
  - `interface IPlacementStrategy { IReadOnlyList<string> OwnedAttributes { get; } PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point); IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle); }`;
  - `static class LayoutMath` with `Thickness Move(UIElement element, Thickness start, int dx, int dy)` and `AxisResize ResizeAxis(...)` (see code);
  - `static class MarkupValues` with `string Format(Thickness)`, `string Format(int)`;
  - `class MarginPlacement : IPlacementStrategy` (the fallback; `AcceptsDrop` virtual);
  - `sealed class PlacementRegistry` with `void Register<TContainer>(IPlacementStrategy)`, `void Register(Type, IPlacementStrategy)`, `IPlacementStrategy Resolve(UIElement container)`; defaults are filled by Task 6.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Editor/MarginPlacementTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class MarginPlacementTests
    {
        [Theory]
        [InlineData(HorizontalAlignment.Left, false, "15,0,0,0")]
        [InlineData(HorizontalAlignment.Right, false, "0,0,-5,0")]
        [InlineData(HorizontalAlignment.Center, false, "15,0,0,0")]
        [InlineData(HorizontalAlignment.Stretch, false, "15,0,0,0")]
        [InlineData(HorizontalAlignment.Stretch, true, "15,0,-5,0")]
        public void Move_FollowsIcyArrangeForEveryAlignment(HorizontalAlignment alignment, bool stretched, string expected)
        {
            var element = new UIElement { HorizontalAlignment = alignment, Width = stretched ? float.NaN : 50 };

            Thickness moved = LayoutMath.Move(element, new Thickness(10, 0, 0, 0), 5, 0);

            Assert.Equal(expected, Expand(moved));
        }

        [Fact]
        public void Move_Vertical_UsesTopAndBottom()
        {
            var element = new UIElement { VerticalAlignment = VerticalAlignment.Bottom, Height = 20 };

            Thickness moved = LayoutMath.Move(element, new Thickness(0, 0, 0, 8), 0, 3);

            Assert.Equal(new Thickness(0, 0, 0, 5), moved);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, "0")]
        [InlineData(4, 4, 4, 4, "4")]
        [InlineData(4, 8, 4, 8, "4,8")]
        [InlineData(1, 2, 3, 4, "1,2,3,4")]
        public void Format_WritesTheShortestThickness(int left, int top, int right, int bottom, string expected)
        {
            Assert.Equal(expected, MarkupValues.Format(new Thickness(left, top, right, bottom)));
        }

        [Fact]
        public void ResizingTheEndEdge_WritesTheSize()
        {
            (PlacementContext context, _) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(12, 0));

            Assert.Contains(new AttributeEdit("Width", "62"), edits);
            Assert.DoesNotContain(edits, x => x.Name == "Margin");
        }

        [Fact]
        public void ResizingTheStartEdge_KeepsTheOppositeEdgeInPlace()
        {
            (PlacementContext context, _) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 0, 0, 0) });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Left).Update(new Vector2(-5, 0));

            Assert.Contains(new AttributeEdit("Width", "55"), edits);
            Assert.Contains(new AttributeEdit("Margin", "5,0,0,0"), edits);
        }

        [Fact]
        public void ResizingAStretchedAxis_MovesTheMarginNotTheSize()
        {
            (PlacementContext context, _) = Context(new UIElement { Height = 20, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4) });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-10, 0));

            Assert.DoesNotContain(edits, x => x.Name == "Width");
            Assert.Contains(new AttributeEdit("Margin", "4,4,14,4"), edits);
        }

        [Fact]
        public void TheSizeNeverGoesBelowOne()
        {
            (PlacementContext context, _) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

            IReadOnlyList<AttributeEdit> edits = new MarginPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-500, 0));

            Assert.Contains(new AttributeEdit("Width", "1"), edits);
        }

        [Fact]
        public void ADropInTheSameContainer_MovesByTheMargin()
        {
            (PlacementContext context, UIElement element) = Context(new UIElement { Width = 50, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 10, 0, 0) });

            // The element's top-left is at (10, 10); the drop asks for (30, 15).
            PlacementTarget? target = new MarginPlacement().GetDropTarget(context, new Vector2(30, 15));

            Assert.NotNull(target);
            Assert.Contains(new AttributeEdit("Margin", "30,15,0,0"), target.Edits);
            Assert.False(target.IndicatorIsLine);
            Assert.Equal(new RectangleF(30, 15, 50, 20), target.Indicator);
            Assert.Equal(element, context.Element);
        }

        [Fact]
        public void ASingleSlotContainer_RefusesADropWhenFilled()
        {
            var border = new Border { Width = 200, Height = 100, Child = new UIElement() };
            var dragged = new UIElement { Width = 10, Height = 10 };
            border.Arrange(new Rectangle(0, 0, 200, 100));
            var context = new PlacementContext(border, dragged, [new PlacementChild(0, border.Child!)], 1, new Rectangle(0, 0, 10, 10), isCurrentContainer: false);

            Assert.Null(new MarginPlacement().GetDropTarget(context, new Vector2(5, 5)));
        }

        [Fact]
        public void TheRegistry_PrefersTheMostDerivedRegistration()
        {
            var registry = new PlacementRegistry();
            var forPanel = new MarginPlacement();
            var forStack = new MarginPlacement();
            registry.Register<Panel>(forPanel);
            registry.Register<StackPanel>(forStack);

            Assert.Same(forStack, registry.Resolve(new StackPanel()));
            Assert.Same(forPanel, registry.Resolve(new Grid()));
        }

        [Fact]
        public void TheRegistry_FallsBackToTheMarginStrategy()
        {
            var registry = new PlacementRegistry();

            Assert.IsType<MarginPlacement>(registry.Resolve(new Border()));
        }

        private static (PlacementContext Context, UIElement Element) Context(UIElement element)
        {
            var panel = new Panel { Width = 300, Height = 200 };
            panel.Children.Add(element);
            panel.Arrange(new Rectangle(0, 0, 300, 200));
            var context = new PlacementContext(panel, element, [], 0, PlacementContext.ToLocal(panel, element.ActualBounds), isCurrentContainer: true);
            return (context, element);
        }

        private static string Expand(Thickness t) => $"{t.Left},{t.Top},{t.Right},{t.Bottom}";
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarginPlacementTests"`
Expected: the build FAILS (`Icy.Design.Editor.Placement` doesn't exist).

- [ ] **Step 3: Implement**

`AttributeEdit.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// One attribute a placement writes or removes.
    /// </summary>
    /// <param name="Name">The attribute name as written in markup, such as <c>Width</c> or <c>Grid.Row</c>.</param>
    /// <param name="Value">The value to write, or <see langword="null"/> to remove the attribute.</param>
    public readonly record struct AttributeEdit(string Name, string? Value);
}
```

`ResizeHandle.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// The edges a resize handle moves. Corner handles combine two edges.
    /// </summary>
    [Flags]
    public enum ResizeHandle
    {
        /// <summary>No edge.</summary>
        None = 0,

        /// <summary>The left edge.</summary>
        Left = 1,

        /// <summary>The top edge.</summary>
        Top = 2,

        /// <summary>The right edge.</summary>
        Right = 4,

        /// <summary>The bottom edge.</summary>
        Bottom = 8,

        /// <summary>The top-left corner.</summary>
        TopLeft = Top | Left,

        /// <summary>The top-right corner.</summary>
        TopRight = Top | Right,

        /// <summary>The bottom-left corner.</summary>
        BottomLeft = Bottom | Left,

        /// <summary>The bottom-right corner.</summary>
        BottomRight = Bottom | Right,
    }
}
```

`PlacementChild.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// A container's content child as a placement sees it.
    /// </summary>
    /// <param name="Index">
    /// The child's position among the container's markup content children, counted without the element being placed:
    /// the index <see cref="MarkupEditor.MoveElement"/> takes.
    /// </param>
    /// <param name="Instance">The child's live copy in the container's load scope.</param>
    public readonly record struct PlacementChild(int Index, UIElement Instance);
}
```

`PlacementContext.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// What a placement strategy knows about a container and the element being moved or resized in it. Rectangles are in
    /// the container's local space: (0, 0) is the container's top-left corner.
    /// </summary>
    public sealed class PlacementContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlacementContext"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="element">The element being placed.</param>
        /// <param name="children">The container's other content children that have live copies, in markup order.</param>
        /// <param name="contentCount">How many content children the container has in markup, without <paramref name="element"/>.</param>
        /// <param name="elementBounds">The element's bounds when the gesture started, in the container's local space.</param>
        /// <param name="isCurrentContainer">Whether <paramref name="element"/> is already a child of <paramref name="container"/>.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public PlacementContext(UIElement container, UIElement element, IReadOnlyList<PlacementChild> children, int contentCount, Rectangle elementBounds, bool isCurrentContainer)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(children);
            Container = container;
            Element = element;
            Children = children;
            ContentCount = contentCount;
            ElementBounds = elementBounds;
            IsCurrentContainer = isCurrentContainer;
            ContainerContent = ToLocal(container, container is IContainerLayout layout ? layout.ContentBounds : container.ActualBounds);
        }

        /// <summary>Gets the container.</summary>
        public UIElement Container { get; }

        /// <summary>Gets the element being placed.</summary>
        public UIElement Element { get; }

        /// <summary>Gets the container's other content children that have live copies, in markup order.</summary>
        public IReadOnlyList<PlacementChild> Children { get; }

        /// <summary>Gets how many content children the container has in markup, without the element being placed.</summary>
        public int ContentCount { get; }

        /// <summary>Gets the element's bounds when the gesture started, in the container's local space.</summary>
        public Rectangle ElementBounds { get; }

        /// <summary>Gets the area the container lays its children out in (inside its padding), in its local space.</summary>
        public Rectangle ContainerContent { get; }

        /// <summary>Gets a value indicating whether the element already is one of the container's children.</summary>
        public bool IsCurrentContainer { get; }

        /// <summary>
        /// Converts a rectangle in layout space (an element's <see cref="UIElement.ActualBounds"/>) into
        /// <paramref name="container"/>'s local space.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="absolute">A rectangle in the same space as the container's <see cref="UIElement.ActualBounds"/>.</param>
        /// <returns>The rectangle relative to the container's top-left corner.</returns>
        public static Rectangle ToLocal(UIElement container, Rectangle absolute)
        {
            ArgumentNullException.ThrowIfNull(container);
            return absolute with { X = absolute.X - container.ActualBounds.X, Y = absolute.Y - container.ActualBounds.Y };
        }
    }
}
```

`PlacementTarget.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Where a dropped element lands in a container.
    /// </summary>
    /// <param name="Index">The content index to move the element to, as <see cref="MarkupEditor.MoveElement"/> counts it.</param>
    /// <param name="Edits">The attributes to write or remove on the element with the move.</param>
    /// <param name="Indicator">The area to highlight while dragging, in the container's local space.</param>
    /// <param name="IndicatorIsLine">
    /// <see langword="true"/> when <paramref name="Indicator"/> is an insertion line (a zero-width or zero-height
    /// rectangle) rather than an area.
    /// </param>
    public sealed record PlacementTarget(int Index, IReadOnlyList<AttributeEdit> Edits, RectangleF Indicator, bool IndicatorIsLine);
}
```

`IResizeOperation.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// One resize gesture in progress. It remembers the element's state when the gesture started, so every update is
    /// computed from that state and rounding never accumulates.
    /// </summary>
    public interface IResizeOperation
    {
        /// <summary>
        /// Computes the attributes for the handle dragged by <paramref name="delta"/> since the gesture started.
        /// </summary>
        /// <param name="delta">The pointer's offset from where the gesture started, in the container's local units.</param>
        /// <returns>The attribute values the element should have now.</returns>
        IReadOnlyList<AttributeEdit> Update(Vector2 delta);
    }
}
```

`IPlacementStrategy.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Turns moves and resizes inside one kind of container into attribute edits: an insertion index for a stack, a
    /// cell for a grid, margins for anything else.
    /// </summary>
    /// <remarks>
    /// Register strategies for custom containers in <see cref="PlacementRegistry"/>. All coordinates are in the
    /// container's local space.
    /// </remarks>
    public interface IPlacementStrategy
    {
        /// <summary>
        /// Gets the attributes that only mean something inside this kind of container, such as <c>Grid.Row</c>. They're
        /// removed from an element that moves into a different container.
        /// </summary>
        IReadOnlyList<string> OwnedAttributes { get; }

        /// <summary>
        /// Decides where an element dropped at <paramref name="point"/> lands.
        /// </summary>
        /// <param name="context">The container and the dragged element.</param>
        /// <param name="point">Where the dragged element's top-left corner would be, in the container's local space.</param>
        /// <returns>The target, or <see langword="null"/> when the container can't take the element.</returns>
        PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point);

        /// <summary>
        /// Starts resizing an element of this container from <paramref name="handle"/>.
        /// </summary>
        /// <param name="context">The container and the element, as they were when the gesture started.</param>
        /// <param name="handle">The dragged handle.</param>
        /// <returns>The operation to update while the pointer moves.</returns>
        IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle);
    }
}
```

`MarkupValues.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Writes layout values the way people write them in markup.
    /// </summary>
    public static class MarkupValues
    {
        /// <summary>
        /// Formats a whole number in the invariant culture.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The markup text.</returns>
        public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Formats a thickness in its shortest form: <c>"4"</c>, <c>"4,8"</c> (horizontal, vertical) or <c>"1,2,3,4"</c>.
        /// </summary>
        /// <param name="value">The thickness.</param>
        /// <returns>The markup text.</returns>
        public static string Format(Thickness value)
        {
            if (value.Left == value.Right && value.Top == value.Bottom)
                return value.Left == value.Top ? Format(value.Left) : $"{Format(value.Left)},{Format(value.Top)}";
            return $"{Format(value.Left)},{Format(value.Top)},{Format(value.Right)},{Format(value.Bottom)}";
        }
    }
}
```

`LayoutMath.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Margin and size arithmetic that follows how IcyUI arranges an element inside its slot.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>Left</c>/<c>Top</c>: positioned at the start margin.</description></item>
    /// <item><description><c>Right</c>/<c>Bottom</c>: positioned against the end margin.</description></item>
    /// <item><description><c>Center</c>, and <c>Stretch</c> with an explicit size: centered in the slack, then offset by the start margin; the end margin is ignored.</description></item>
    /// <item><description><c>Stretch</c> with no size: fills the slot between both margins.</description></item>
    /// </list>
    /// </remarks>
    public static class LayoutMath
    {
        /// <summary>
        /// Gets the margin that moves <paramref name="element"/> by (<paramref name="dx"/>, <paramref name="dy"/>) without
        /// changing its size.
        /// </summary>
        /// <param name="element">The element, for its alignment and explicit size.</param>
        /// <param name="start">The margin to move from.</param>
        /// <param name="dx">The horizontal offset.</param>
        /// <param name="dy">The vertical offset.</param>
        /// <returns>The new margin.</returns>
        public static Thickness Move(UIElement element, Thickness start, int dx, int dy)
        {
            ArgumentNullException.ThrowIfNull(element);
            (int left, int right) = MoveAxis(Kind(element.HorizontalAlignment, element.Width), start.Left, start.Right, dx);
            (int top, int bottom) = MoveAxis(Kind(element.VerticalAlignment, element.Height), start.Top, start.Bottom, dy);
            return new Thickness(left, top, right, bottom);
        }

        /// <summary>
        /// Resizes one axis of an element by dragging its start or end edge.
        /// </summary>
        /// <param name="kind">How the element is aligned on this axis.</param>
        /// <param name="startSize">The element's size on this axis when the gesture started.</param>
        /// <param name="startMarginStart">The start margin (left or top) when the gesture started.</param>
        /// <param name="startMarginEnd">The end margin (right or bottom) when the gesture started.</param>
        /// <param name="delta">How far the dragged edge moved, positive towards the end.</param>
        /// <param name="isStartEdge">Whether the start edge (left or top) is dragged.</param>
        /// <param name="anchored">
        /// <see langword="true"/> when the container anchors the element on this axis (a stack's stacking direction), so
        /// only the size changes.
        /// </param>
        /// <returns>The new size (<see langword="null"/> when the size isn't written) and margins.</returns>
        public static AxisResize ResizeAxis(AxisKind kind, int startSize, int startMarginStart, int startMarginEnd, int delta, bool isStartEdge, bool anchored)
        {
            if (kind == AxisKind.Stretched)
            {
                // A size would turn the element into a centered one; move the margin on the dragged side instead.
                return isStartEdge
                    ? new AxisResize(null, startMarginStart + delta, startMarginEnd)
                    : new AxisResize(null, startMarginStart, startMarginEnd - delta);
            }

            int size = Math.Max(1, isStartEdge ? startSize - delta : startSize + delta);
            int applied = isStartEdge ? startSize - size : size - startSize;
            if (!isStartEdge || anchored)
                return new AxisResize(size, startMarginStart, startMarginEnd);

            // Keep the end edge where it was.
            return kind switch
            {
                AxisKind.Start => new AxisResize(size, startMarginStart + applied, startMarginEnd),
                AxisKind.Centered => new AxisResize(size, startMarginStart + (applied / 2), startMarginEnd),
                _ => new AxisResize(size, startMarginStart, startMarginEnd),
            };
        }

        /// <summary>
        /// Classifies an axis from its alignment and explicit size.
        /// </summary>
        /// <param name="alignment">The horizontal alignment.</param>
        /// <param name="size">The explicit width, or <see cref="float.NaN"/>.</param>
        /// <returns>The axis kind.</returns>
        public static AxisKind Kind(HorizontalAlignment alignment, float size) => alignment switch
        {
            HorizontalAlignment.Left => AxisKind.Start,
            HorizontalAlignment.Right => AxisKind.End,
            HorizontalAlignment.Stretch when float.IsNaN(size) => AxisKind.Stretched,
            _ => AxisKind.Centered,
        };

        /// <summary>
        /// Classifies an axis from its alignment and explicit size.
        /// </summary>
        /// <param name="alignment">The vertical alignment.</param>
        /// <param name="size">The explicit height, or <see cref="float.NaN"/>.</param>
        /// <returns>The axis kind.</returns>
        public static AxisKind Kind(VerticalAlignment alignment, float size) => alignment switch
        {
            VerticalAlignment.Top => AxisKind.Start,
            VerticalAlignment.Bottom => AxisKind.End,
            VerticalAlignment.Stretch when float.IsNaN(size) => AxisKind.Stretched,
            _ => AxisKind.Centered,
        };

        private static (int Start, int End) MoveAxis(AxisKind kind, int start, int end, int delta) => kind switch
        {
            AxisKind.End => (start, end - delta),
            AxisKind.Stretched => (start + delta, end - delta),
            _ => (start + delta, end),
        };
    }
}
```

`AxisKind.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// How an element is positioned on one axis.
    /// </summary>
    public enum AxisKind
    {
        /// <summary>Aligned to the start (left or top).</summary>
        Start,

        /// <summary>Aligned to the end (right or bottom).</summary>
        End,

        /// <summary>Centered, or stretched with an explicit size.</summary>
        Centered,

        /// <summary>Stretched with no explicit size: it fills the slot between its margins.</summary>
        Stretched,
    }
}
```

`AxisResize.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// The result of resizing one axis.
    /// </summary>
    /// <param name="Size">The new size, or <see langword="null"/> when the size isn't written.</param>
    /// <param name="MarginStart">The new start margin (left or top).</param>
    /// <param name="MarginEnd">The new end margin (right or bottom).</param>
    public readonly record struct AxisResize(int? Size, int MarginStart, int MarginEnd);
}
```

`MarginPlacement.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// The fallback placement: an element moves by its margin, alignment-aware, and resizes by its size (or, on a
    /// stretched axis, by the margin on the dragged side). Used for any container without a registered strategy.
    /// </summary>
    /// <remarks>
    /// A container whose markup content is a single slot (a <c>Border</c>'s child, a <c>ContentControl</c>'s content)
    /// accepts a dropped element only when the slot is empty.
    /// </remarks>
    public class MarginPlacement : IPlacementStrategy
    {
        /// <inheritdoc/>
        public virtual IReadOnlyList<string> OwnedAttributes => [];

        /// <inheritdoc/>
        public virtual PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (!AcceptsDrop(context))
                return null;

            int dx = (int)MathF.Round(point.X) - context.ElementBounds.X;
            int dy = (int)MathF.Round(point.Y) - context.ElementBounds.Y;
            var area = new RectangleF(point.X, point.Y, context.ElementBounds.Width, context.ElementBounds.Height);
            if (!context.IsCurrentContainer)
            {
                // From another container: keep the margin, append as the last child.
                return new PlacementTarget(context.ContentCount, [], context.ContainerContent, IndicatorIsLine: false);
            }

            Thickness margin = LayoutMath.Move(context.Element, context.Element.Margin, dx, dy);
            return new PlacementTarget(CurrentIndex(context), [new AttributeEdit("Margin", MarkupValues.Format(margin))], area, IndicatorIsLine: false);
        }

        /// <inheritdoc/>
        public virtual IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new Resize(context, handle, anchorHorizontal: false, anchorVertical: false);
        }

        /// <summary>
        /// Decides whether the container can take a dropped element at all.
        /// </summary>
        /// <param name="context">The container and the dragged element.</param>
        /// <returns>
        /// <see langword="true"/> for list containers and for an empty single slot; <see langword="false"/> for a filled
        /// single slot that doesn't already hold the element.
        /// </returns>
        protected virtual bool AcceptsDrop(PlacementContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool singleSlot = context.Container is Border || context.Container is UI.Controls.ContentControl;
            return !singleSlot || context.IsCurrentContainer || context.ContentCount == 0;
        }

        /// <summary>
        /// Gets the element's current content index, so a move inside the same container keeps its order.
        /// </summary>
        /// <param name="context">The container and the element.</param>
        /// <returns>The index <see cref="MarkupEditor.MoveElement"/> would leave unchanged.</returns>
        protected static int CurrentIndex(PlacementContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            int index = 0;
            foreach (PlacementChild child in context.Children)
            {
                if (child.Instance.ActualBounds.Y > context.Element.ActualBounds.Y || (child.Instance.ActualBounds.Y == context.Element.ActualBounds.Y && child.Instance.ActualBounds.X > context.Element.ActualBounds.X))
                    break;
                index = child.Index + 1;
            }

            return Math.Min(index, context.ContentCount);
        }

        /// <summary>
        /// The size-or-margin resize every strategy builds on.
        /// </summary>
        protected internal sealed class Resize(PlacementContext context, ResizeHandle handle, bool anchorHorizontal, bool anchorVertical) : IResizeOperation
        {
            private readonly Rectangle bounds = context.ElementBounds;
            private readonly Thickness margin = context.Element.Margin;
            private readonly AxisKind horizontal = LayoutMath.Kind(context.Element.HorizontalAlignment, context.Element.Width);
            private readonly AxisKind vertical = LayoutMath.Kind(context.Element.VerticalAlignment, context.Element.Height);

            /// <inheritdoc/>
            public IReadOnlyList<AttributeEdit> Update(Vector2 delta)
            {
                var edits = new List<AttributeEdit>(3);
                int left = margin.Left, top = margin.Top, right = margin.Right, bottom = margin.Bottom;
                if ((handle & (ResizeHandle.Left | ResizeHandle.Right)) != 0)
                {
                    AxisResize x = LayoutMath.ResizeAxis(horizontal, bounds.Width, margin.Left, margin.Right, (int)MathF.Round(delta.X), (handle & ResizeHandle.Left) != 0, anchorHorizontal);
                    if (x.Size is int width)
                        edits.Add(new AttributeEdit("Width", MarkupValues.Format(width)));
                    (left, right) = (x.MarginStart, x.MarginEnd);
                }

                if ((handle & (ResizeHandle.Top | ResizeHandle.Bottom)) != 0)
                {
                    AxisResize y = LayoutMath.ResizeAxis(vertical, bounds.Height, margin.Top, margin.Bottom, (int)MathF.Round(delta.Y), (handle & ResizeHandle.Top) != 0, anchorVertical);
                    if (y.Size is int height)
                        edits.Add(new AttributeEdit("Height", MarkupValues.Format(height)));
                    (top, bottom) = (y.MarginStart, y.MarginEnd);
                }

                var next = new Thickness(left, top, right, bottom);
                if (next != margin)
                    edits.Add(new AttributeEdit("Margin", MarkupValues.Format(next)));
                return edits;
            }
        }
    }
}
```

`PlacementRegistry.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Chooses the placement strategy for a container: the registration for the most derived of its types, or
    /// <see cref="MarginPlacement"/> when none matches.
    /// </summary>
    public sealed class PlacementRegistry
    {
        private readonly Dictionary<Type, IPlacementStrategy> strategies = [];
        private readonly MarginPlacement fallback = new();

        /// <summary>
        /// Registers the strategy for <typeparamref name="TContainer"/> and the containers derived from it, replacing any
        /// earlier registration for that type.
        /// </summary>
        /// <typeparam name="TContainer">The container type.</typeparam>
        /// <param name="strategy">The strategy.</param>
        public void Register<TContainer>(IPlacementStrategy strategy)
            where TContainer : UIElement => Register(typeof(TContainer), strategy);

        /// <summary>
        /// Registers the strategy for <paramref name="containerType"/> and the containers derived from it, replacing any
        /// earlier registration for that type.
        /// </summary>
        /// <param name="containerType">The container type; a <see cref="UIElement"/>.</param>
        /// <param name="strategy">The strategy.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="containerType"/> isn't a <see cref="UIElement"/>.</exception>
        public void Register(Type containerType, IPlacementStrategy strategy)
        {
            ArgumentNullException.ThrowIfNull(containerType);
            ArgumentNullException.ThrowIfNull(strategy);
            if (!typeof(UIElement).IsAssignableFrom(containerType))
                throw new ArgumentException($"'{containerType.Name}' isn't a UI element.", nameof(containerType));
            strategies[containerType] = strategy;
        }

        /// <summary>
        /// Gets the strategy for <paramref name="container"/>.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <returns>The registered strategy for its most derived registered type, or the margin fallback.</returns>
        public IPlacementStrategy Resolve(UIElement container)
        {
            ArgumentNullException.ThrowIfNull(container);
            for (Type? type = container.GetType(); type != null; type = type.BaseType)
            {
                if (strategies.TryGetValue(type, out IPlacementStrategy? strategy))
                    return strategy;
            }

            return fallback;
        }
    }
}
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarginPlacementTests"`
Expected: PASS, Total 19 (5 theory rows + 1 + 4 theory rows + 9 facts).

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1342, no failures), then the warning count (81).

```bash
git add sources/IcyUI.Design/Editor/Placement sources/IcyUI.Tests/Design/Editor/MarginPlacementTests.cs
git commit -m "Add placement strategies' types, the registry and the margin fallback

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: StackPanel and Grid placement

**Files:**
- Create: `sources/IcyUI.Design/Editor/Placement/StackPanelPlacement.cs`, `sources/IcyUI.Design/Editor/Placement/GridPlacement.cs`
- Modify: `sources/IcyUI.Design/Editor/Placement/PlacementRegistry.cs` (default registrations)
- Test: `sources/IcyUI.Tests/Design/Editor/StackAndGridPlacementTests.cs`

**Interfaces:**
- Consumes: Task 5's types; Task 1's span accessors.
- Produces: `public sealed class StackPanelPlacement : MarginPlacement`, `public sealed class GridPlacement : MarginPlacement` with `float SpanThreshold { get; set; }` (default `0.25f`); `PlacementRegistry`'s constructor registers both.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Editor/StackAndGridPlacementTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class StackAndGridPlacementTests
    {
        [Theory]
        [InlineData(5, 0)]
        [InlineData(25, 1)]
        [InlineData(45, 1)]
        [InlineData(55, 2)]
        [InlineData(500, 3)]
        public void AVerticalStack_DropsAtTheNearestGap(int y, int expected)
        {
            (StackPanel stack, UIElement[] children) = Stack(Orientation.Vertical, 3, 20);
            var dragged = new UIElement { Width = 10, Height = 20 };
            PlacementContext context = Context(stack, dragged, children, isCurrent: false);

            PlacementTarget? target = new StackPanelPlacement().GetDropTarget(context, new Vector2(0, y));

            Assert.NotNull(target);
            Assert.Equal(expected, target.Index);
            Assert.True(target.IndicatorIsLine);
            Assert.Equal(expected * 20, target.Indicator.Y);
        }

        [Fact]
        public void AHorizontalStack_DropsAlongX()
        {
            (StackPanel stack, UIElement[] children) = Stack(Orientation.Horizontal, 2, 30);
            PlacementContext context = Context(stack, new UIElement { Width = 10, Height = 10 }, children, isCurrent: false);

            PlacementTarget? target = new StackPanelPlacement().GetDropTarget(context, new Vector2(40, 0));

            Assert.Equal(1, target!.Index);
            Assert.Equal(30, target.Indicator.X);
        }

        [Fact]
        public void AStack_ResizesItsStackingAxisWithoutTouchingTheMargin()
        {
            (StackPanel stack, UIElement[] children) = Stack(Orientation.Vertical, 2, 20);
            PlacementContext context = Context(stack, children[1], [children[0]], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new StackPanelPlacement().BeginResize(context, ResizeHandle.Top).Update(new Vector2(0, -6));

            Assert.Equal([new AttributeEdit("Height", "26")], edits);
        }

        [Fact]
        public void AGrid_DropsIntoTheCellUnderThePoint()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement { Width = 10, Height = 10, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            PlacementTarget? target = new GridPlacement().GetDropTarget(context, new Vector2(150, 60));

            Assert.Contains(new AttributeEdit("Grid.Column", "1"), target!.Edits);
            Assert.Contains(new AttributeEdit("Grid.Row", "1"), target.Edits);
            Assert.Equal(new RectangleF(100, 50, 100, 50), target.Indicator);
        }

        [Fact]
        public void AGrid_RemovesRowAndColumnWhenTheyBecomeZero()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement { Width = 10, Height = 10 });
            Grid.SetRow(element, 2);
            Grid.SetColumn(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            PlacementTarget? target = new GridPlacement().GetDropTarget(context, new Vector2(5, 5));

            Assert.Contains(new AttributeEdit("Grid.Column", null), target!.Edits);
            Assert.Contains(new AttributeEdit("Grid.Row", null), target.Edits);
        }

        [Fact]
        public void AGridDrop_KeepsTheSpanButFitsItIn()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            Grid.SetColumnSpan(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            PlacementTarget? target = new GridPlacement().GetDropTarget(context, new Vector2(250, 5));

            // Column 2 can't hold a span of 2, so the element starts at column 1.
            Assert.Contains(new AttributeEdit("Grid.Column", "1"), target!.Edits);
            Assert.Equal(new RectangleF(100, 0, 200, 50), target.Indicator);
        }

        [Theory]
        [InlineData(20, 1)]
        [InlineData(26, 2)]
        [InlineData(126, 3)]
        public void ResizingPastTheThreshold_GrowsTheColumnSpan(int dx, int expectedSpan)
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(dx, 0));

            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", expectedSpan == 1 ? null : expectedSpan.ToString(System.Globalization.CultureInfo.InvariantCulture)), edits);
        }

        [Fact]
        public void PullingBackBelowTheThreshold_ShrinksTheSpan()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            Grid.SetColumnSpan(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            // The right edge at 200 moves to 120: less than a quarter into column 1.
            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(-80, 0));

            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", null), edits);
        }

        [Fact]
        public void ResizingTheStartEdge_MovesTheColumnBack()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement());
            Grid.SetColumn(element, 2);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            // The left edge at 200 moves to 160: more than a quarter into column 1 (which ends at 200).
            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Left).Update(new Vector2(-40, 0));

            Assert.Contains(new AttributeEdit("Grid.Column", "1"), edits);
            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", "2"), edits);
        }

        [Fact]
        public void AnExplicitWidthInAGrid_IsResizedWithTheSpan()
        {
            (Grid grid, UIElement element) = Grid3x3(new UIElement { Width = 80, HorizontalAlignment = HorizontalAlignment.Left });
            PlacementContext context = Context(grid, element, [], isCurrent: true);

            IReadOnlyList<AttributeEdit> edits = new GridPlacement().BeginResize(context, ResizeHandle.Right).Update(new Vector2(60, 0));

            Assert.Contains(new AttributeEdit("Width", "140"), edits);
            Assert.Contains(new AttributeEdit("Grid.ColumnSpan", "2"), edits);
        }

        [Fact]
        public void TheGridOwnsItsAttachedAttributes()
        {
            Assert.Equal(["Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan"], new GridPlacement().OwnedAttributes);
        }

        [Fact]
        public void TheDefaultRegistry_KnowsStacksAndGrids()
        {
            var registry = new PlacementRegistry();

            Assert.IsType<StackPanelPlacement>(registry.Resolve(new StackPanel()));
            Assert.IsType<GridPlacement>(registry.Resolve(new Grid()));
            Assert.IsType<MarginPlacement>(registry.Resolve(new Panel()));
        }

        private static (StackPanel Stack, UIElement[] Children) Stack(Orientation orientation, int count, int size)
        {
            var stack = new StackPanel { Orientation = orientation };
            var children = new UIElement[count];
            for (int i = 0; i < count; i++)
            {
                children[i] = new UIElement { Width = size, Height = size };
                stack.Children.Add(children[i]);
            }

            stack.Arrange(new Rectangle(0, 0, 300, 300));
            return (stack, children);
        }

        private static (Grid Grid, UIElement Element) Grid3x3(UIElement element)
        {
            var grid = new Grid();
            for (int i = 0; i < 3; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 100f });
                grid.RowDefinitions.Add(new RowDefinition { Height = 50f });
            }

            grid.Children.Add(element);
            grid.Arrange(new Rectangle(0, 0, 300, 150));
            return (grid, element);
        }

        private static PlacementContext Context(UIElement container, UIElement element, UIElement[] others, bool isCurrent)
        {
            PlacementChild[] children = [.. others.Select((x, i) => new PlacementChild(i, x))];
            Rectangle bounds = isCurrent ? PlacementContext.ToLocal(container, element.ActualBounds) : new Rectangle(0, 0, (int)element.Width, (int)element.Height);
            return new PlacementContext(container, element, children, children.Length, bounds, isCurrent);
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~StackAndGridPlacementTests"`
Expected: the build FAILS (`StackPanelPlacement`, `GridPlacement` not found).

- [ ] **Step 3: Implement**

`StackPanelPlacement.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Places elements in a <see cref="StackPanel"/>: a drop lands in the nearest gap between children, and a resize along
    /// the stacking direction changes only the size, since a stack anchors its children there.
    /// </summary>
    public sealed class StackPanelPlacement : MarginPlacement
    {
        /// <inheritdoc/>
        public override PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool vertical = ((StackPanel)context.Container).Orientation == Orientation.Vertical;
            float along = vertical ? point.Y : point.X;

            int index = context.ContentCount;
            float lineAt = float.NaN;
            foreach (PlacementChild child in context.Children)
            {
                Rectangle bounds = PlacementContext.ToLocal(context.Container, child.Instance.ActualBounds);
                float start = vertical ? bounds.Top : bounds.Left;
                float middle = vertical ? bounds.Top + (bounds.Height / 2f) : bounds.Left + (bounds.Width / 2f);
                if (along < middle)
                {
                    index = child.Index;
                    lineAt = start;
                    break;
                }

                lineAt = vertical ? bounds.Bottom : bounds.Right;
            }

            if (float.IsNaN(lineAt))
                lineAt = vertical ? context.ContainerContent.Top : context.ContainerContent.Left;

            Rectangle content = context.ContainerContent;
            RectangleF line = vertical
                ? new RectangleF(content.Left, lineAt, content.Width, 0)
                : new RectangleF(lineAt, content.Top, 0, content.Height);
            return new PlacementTarget(index, [], line, IndicatorIsLine: true);
        }

        /// <inheritdoc/>
        public override IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle)
        {
            ArgumentNullException.ThrowIfNull(context);
            bool vertical = ((StackPanel)context.Container).Orientation == Orientation.Vertical;
            return new Resize(context, handle, anchorHorizontal: !vertical, anchorVertical: vertical);
        }
    }
}
```

`GridPlacement.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// Places elements in a <see cref="Grid"/>: a drop lands in the cell under the pointer, and a resize grows or shrinks
    /// <c>Grid.RowSpan</c>/<c>Grid.ColumnSpan</c> as the dragged edge crosses into the next track.
    /// </summary>
    /// <remarks>
    /// <c>Grid.Row</c>, <c>Grid.Column</c> and the spans are written only when they differ from their defaults (0 and 1),
    /// and removed when they return to them. An axis the element stretches on changes only its span; an axis with an
    /// explicit size also writes <c>Width</c>/<c>Height</c>.
    /// </remarks>
    public sealed class GridPlacement : MarginPlacement
    {
        private static readonly string[] Owned = ["Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan"];

        /// <summary>
        /// Gets or sets how far, as a fraction of a track, the dragged edge must reach into the next track before the span
        /// grows to include it. Defaults to <c>0.25</c>.
        /// </summary>
        public float SpanThreshold { get; set; } = 0.25f;

        /// <inheritdoc/>
        public override IReadOnlyList<string> OwnedAttributes => Owned;

        /// <inheritdoc/>
        public override PlacementTarget? GetDropTarget(PlacementContext context, Vector2 point)
        {
            ArgumentNullException.ThrowIfNull(context);
            var grid = (Grid)context.Container;
            (float[] columns, float[] rows) = Tracks(grid, context);

            int columnSpan = Math.Min(Grid.GetColumnSpan(context.Element), columns.Length - 1);
            int rowSpan = Math.Min(Grid.GetRowSpan(context.Element), rows.Length - 1);
            int column = Math.Min(TrackAt(columns, point.X), columns.Length - 1 - columnSpan);
            int row = Math.Min(TrackAt(rows, point.Y), rows.Length - 1 - rowSpan);

            var area = RectangleF.FromLTRB(columns[column], rows[row], columns[column + columnSpan], rows[row + rowSpan]);
            AttributeEdit[] edits = [Index("Grid.Column", column), Index("Grid.Row", row)];
            int index = context.IsCurrentContainer ? CurrentIndex(context) : context.ContentCount;
            return new PlacementTarget(index, edits, area, IndicatorIsLine: false);
        }

        /// <inheritdoc/>
        public override IResizeOperation BeginResize(PlacementContext context, ResizeHandle handle)
        {
            ArgumentNullException.ThrowIfNull(context);
            return new SpanResize(this, context, handle);
        }

        private static AttributeEdit Index(string name, int value) => new(name, value == 0 ? null : MarkupValues.Format(value));

        private static AttributeEdit Span(string name, int value) => new(name, value == 1 ? null : MarkupValues.Format(value));

        /// <summary>
        /// Gets the track edges in the grid's local space: <c>n + 1</c> offsets for <c>n</c> tracks.
        /// </summary>
        private static (float[] Columns, float[] Rows) Tracks(Grid grid, PlacementContext context)
        {
            Rectangle content = context.ContainerContent;
            float[] columns = Edges(content.Left, grid.ColumnDefinitions.Select(x => x.ActualWidth), content.Width);
            float[] rows = Edges(content.Top, grid.RowDefinitions.Select(x => x.ActualHeight), content.Height);
            return (columns, rows);
        }

        private static float[] Edges(float start, IEnumerable<float> sizes, float whole)
        {
            List<float> edges = [start];
            foreach (float size in sizes)
                edges.Add(edges[^1] + size);
            if (edges.Count == 1)
                edges.Add(start + whole);
            return [.. edges];
        }

        private static int TrackAt(float[] edges, float position)
        {
            for (int i = 0; i < edges.Length - 1; i++)
            {
                if (position < edges[i + 1])
                    return i;
            }

            return edges.Length - 2;
        }

        private sealed class SpanResize : IResizeOperation
        {
            private readonly GridPlacement owner;
            private readonly ResizeHandle handle;
            private readonly IResizeOperation sizes;
            private readonly float[] columns;
            private readonly float[] rows;
            private readonly int column;
            private readonly int row;
            private readonly int columnSpan;
            private readonly int rowSpan;
            private readonly bool stretchedX;
            private readonly bool stretchedY;

            public SpanResize(GridPlacement owner, PlacementContext context, ResizeHandle handle)
            {
                this.owner = owner;
                this.handle = handle;
                var grid = (Grid)context.Container;
                (columns, rows) = Tracks(grid, context);
                column = Math.Clamp(Grid.GetColumn(context.Element), 0, columns.Length - 2);
                row = Math.Clamp(Grid.GetRow(context.Element), 0, rows.Length - 2);
                columnSpan = Math.Clamp(Grid.GetColumnSpan(context.Element), 1, columns.Length - 1 - column);
                rowSpan = Math.Clamp(Grid.GetRowSpan(context.Element), 1, rows.Length - 1 - row);
                stretchedX = LayoutMath.Kind(context.Element.HorizontalAlignment, context.Element.Width) == AxisKind.Stretched;
                stretchedY = LayoutMath.Kind(context.Element.VerticalAlignment, context.Element.Height) == AxisKind.Stretched;

                // Explicit sizes resize as in a stack: the cell, not the element, anchors it.
                ResizeHandle sized = handle;
                if (stretchedX)
                    sized &= ~(ResizeHandle.Left | ResizeHandle.Right);
                if (stretchedY)
                    sized &= ~(ResizeHandle.Top | ResizeHandle.Bottom);
                sizes = new Resize(context, sized, anchorHorizontal: true, anchorVertical: true);
            }

            public IReadOnlyList<AttributeEdit> Update(Vector2 delta)
            {
                var edits = new List<AttributeEdit>(sizes.Update(delta));
                if ((handle & ResizeHandle.Right) != 0)
                    edits.Add(Span("Grid.ColumnSpan", EndSpan(columns, column, columnSpan, delta.X)));
                if ((handle & ResizeHandle.Bottom) != 0)
                    edits.Add(Span("Grid.RowSpan", EndSpan(rows, row, rowSpan, delta.Y)));
                if ((handle & ResizeHandle.Left) != 0)
                {
                    (int start, int span) = StartSpan(columns, column, columnSpan, delta.X);
                    edits.Add(Index("Grid.Column", start));
                    edits.Add(Span("Grid.ColumnSpan", span));
                }

                if ((handle & ResizeHandle.Top) != 0)
                {
                    (int start, int span) = StartSpan(rows, row, rowSpan, delta.Y);
                    edits.Add(Index("Grid.Row", start));
                    edits.Add(Span("Grid.RowSpan", span));
                }

                return edits;
            }

            /// <summary>
            /// The span whose last track the dragged end edge reaches past the threshold.
            /// </summary>
            private int EndSpan(float[] edges, int start, int span, float delta)
            {
                float edge = edges[start + span] + delta;
                int last = start;
                for (int i = start + 1; i < edges.Length - 1; i++)
                {
                    float size = edges[i + 1] - edges[i];
                    if (edge > edges[i] + (owner.SpanThreshold * size))
                        last = i;
                }

                return last - start + 1;
            }

            /// <summary>
            /// The first track the dragged start edge reaches past the threshold, and the span that keeps the end track.
            /// </summary>
            private (int Start, int Span) StartSpan(float[] edges, int start, int span, float delta)
            {
                int end = start + span - 1;
                float edge = edges[start] + delta;
                int first = end;
                for (int i = end - 1; i >= 0; i--)
                {
                    float size = edges[i + 1] - edges[i];
                    if (edge < edges[i + 1] - (owner.SpanThreshold * size))
                        first = i;
                }

                return (first, end - first + 1);
            }
        }
    }
}
```

In `PlacementRegistry`, add a constructor after the fields:

```csharp
        /// <summary>
        /// Initializes a new instance of the <see cref="PlacementRegistry"/> class with the built-in strategies:
        /// <see cref="StackPanelPlacement"/> for <see cref="UI.Controls.StackPanel"/> and <see cref="GridPlacement"/> for
        /// <see cref="UI.Controls.Grid"/>.
        /// </summary>
        public PlacementRegistry()
        {
            Register<UI.Controls.StackPanel>(new StackPanelPlacement());
            Register<UI.Controls.Grid>(new GridPlacement());
        }
```

Task 5's `TheRegistry_PrefersTheMostDerivedRegistration` still passes: it registers its own strategies over the defaults.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PlacementTests"`
Expected: PASS, `StackAndGridPlacementTests` Total 18 (5 + 1 + 1 + 1 + 1 + 1 + 3 + 1 + 1 + 1 + 1 + 1), and every `MarginPlacementTests` test still passes.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1360, no failures), then the warning count (81).

```bash
git add sources/IcyUI.Design/Editor/Placement sources/IcyUI.Tests/Design/Editor/StackAndGridPlacementTests.cs
git commit -m "Add StackPanel and Grid placement, with span resizing

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Headless gestures: move, resize, nudge, delete

**Files:**
- Create: `sources/IcyUI.Design/Editor/MoveGesture.cs`, `sources/IcyUI.Design/Editor/ResizeGesture.cs`, `sources/IcyUI.Design/Editor/GestureEdits.cs`
- Modify: `sources/IcyUI.Design/Editor/EditorSession.cs` (`Placement`, `BeginMove`, `BeginResize`, `Nudge`, `DeleteSelection`, `LastEdited`)
- Test: `sources/IcyUI.Tests/Design/Editor/EditorGestureTests.cs`

**Interfaces:**
- Consumes: Tasks 4–6.
- Produces (namespace `Icy.Design.Editor`):
  - `public PlacementRegistry EditorSession.Placement { get; }`;
  - `public MoveGesture? EditorSession.BeginMove(Point screenPoint)`: `null` when nothing is selected, the session is blocked or the selection is the root;
  - `public sealed class MoveGesture` with `PlacementTarget? Target`, `UIElement? TargetContainer`, `Rectangle GhostBounds` (screen space), `void Update(Point screenPoint)`, `EditResult Complete()`, `void Cancel()`;
  - `public ResizeGesture? EditorSession.BeginResize(ResizeHandle handle, Point screenPoint)`;
  - `public sealed class ResizeGesture` with `void Update(Point screenPoint)`, `EditResult Complete()`, `void Cancel()`;
  - `public EditResult EditorSession.Nudge(int dx, int dy)`, `public EditResult EditorSession.DeleteSelection()`;
  - `public DesignDocument? EditorSession.LastEdited { get; }` (Task 8's Undo/Redo use it when nothing is selected).

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Editor/EditorGestureTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design;
using Icy.Design.Editor;
using Icy.Design.Editor.Placement;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorGestureTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Border x:Name="b" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Border x:Name="c" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Grid x:Name="grid" Width="200" Height="100">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="100"/>
                  <ColumnDefinition Width="100"/>
                </Grid.ColumnDefinitions>
                <Border x:Name="cell" Grid.Column="1"/>
              </Grid>
            </StackPanel>
            """;

        [Fact]
        public void AMove_ToAnotherGap_IsOneUndoStepWithAMinimalChange()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<Border>("a");
            host.Session.Select(a);
            string before = host.Document.Text;

            MoveGesture move = host.Session.BeginMove(host.At(a, 5, 5))!;
            move.Update(host.At(host.Named<Border>("c"), 5, 15));
            Assert.Equal(2, move.Target!.Index);
            Assert.True(move.Complete().Succeeded);

            var root = host.Named<StackPanel>("root");
            Assert.Same(a, root.Children[2]);
            Assert.True(host.Document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void AMove_IntoAGridCell_WritesTheCell()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<Border>("a");
            var grid = host.Named<Grid>("grid");
            host.Session.Select(a);

            MoveGesture move = host.Session.BeginMove(host.At(a, 1, 1))!;
            move.Update(host.At(grid, 150, 10));
            Assert.Same(grid, move.TargetContainer);
            move.Complete();

            Assert.Contains("<Border x:Name=\"a\" Width=\"100\" Height=\"20\" HorizontalAlignment=\"Left\" Grid.Column=\"1\"/>", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void MovingOutOfAGrid_DropsTheGridAttributes()
        {
            using var host = new EditorTestHost(Page);
            var cell = host.Named<Border>("cell");
            host.Session.Select(cell);

            MoveGesture move = host.Session.BeginMove(host.At(cell, 1, 1))!;
            move.Update(host.At(host.Named<Border>("a"), 1, 1));
            move.Complete();

            Assert.Contains("<Border x:Name=\"cell\"/>", host.Document.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Grid.Column", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void AMove_NeverDropsAnElementIntoItself()
        {
            using var host = new EditorTestHost(Page);
            var grid = host.Named<Grid>("grid");
            host.Session.Select(grid);

            MoveGesture move = host.Session.BeginMove(host.At(grid, 5, 5))!;
            move.Update(host.At(host.Named<Border>("cell"), 5, 5));

            Assert.NotSame(grid, move.TargetContainer);
            Assert.NotSame(host.Named<Border>("cell"), move.TargetContainer);
        }

        [Fact]
        public void ACancelledMove_ChangesNothing()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<Border>("a");
            host.Session.Select(a);
            string before = host.Document.Text;

            MoveGesture move = host.Session.BeginMove(host.At(a, 1, 1))!;
            move.Update(host.At(host.Named<Border>("c"), 1, 15));
            move.Cancel();

            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void AResize_FollowsThePointerLive_AndIsOneUndoStep()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<Border>("a");
            host.Session.Select(a);
            Point start = host.At(a, 100, 10);

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 10 });
            Assert.Equal(110f, a.Width);
            resize.Update(start with { X = start.X + 25 });
            Assert.Equal(125f, a.Width);
            Assert.True(resize.Complete().Succeeded);

            Assert.True(host.Document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(100f, a.Width);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void ACancelledResize_PutsTheSizeBack()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<Border>("a");
            host.Session.Select(a);
            string before = host.Document.Text;
            Point start = host.At(a, 100, 10);

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 30 });
            resize.Cancel();

            Assert.Equal(100f, a.Width);
            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void Nudge_WritesTheMargin()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));

            Assert.True(host.Session.Nudge(3, 0).Succeeded);
            Assert.True(host.Session.Nudge(2, 1).Succeeded);

            Assert.Contains("Margin=\"5,1,0,0\"", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void DeleteSelection_RemovesTheElement_ButNeverTheRoot()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("b"));
            Assert.True(host.Session.DeleteSelection().Succeeded);
            Assert.DoesNotContain("x:Name=\"b\"", host.Document.Text, StringComparison.Ordinal);
            Assert.Null(host.Session.Selection);

            host.Session.Select(host.Root);
            Assert.False(host.Session.DeleteSelection().Succeeded);
        }

        [Fact]
        public void TheRoot_CanBeResizedButNotMoved()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Root);

            Assert.Null(host.Session.BeginMove(host.At(host.Root)));
            Assert.NotNull(host.Session.BeginResize(ResizeHandle.Right, host.At(host.Root)));
        }

        [Fact]
        public void Gestures_AreRefusedWhileBlocked()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);

            Assert.Null(host.Session.BeginMove(host.At(host.Named<Border>("a"))));
            Assert.Null(host.Session.BeginResize(ResizeHandle.Right, host.At(host.Named<Border>("a"))));
            Assert.False(host.Session.Nudge(1, 0).Succeeded);
        }

        [Fact]
        public void AFailingEditInsideAGesture_RollsTheWholeGestureBack()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<Border>("a");
            host.Session.Select(a);
            string before = host.Document.Text;
            var failing = new FailingPlacement();
            host.Session.Placement.Register<Grid>(failing);

            MoveGesture move = host.Session.BeginMove(host.At(a, 1, 1))!;
            move.Update(host.At(host.Named<Grid>("grid"), 150, 10));
            EditResult result = move.Complete();

            Assert.False(result.Succeeded);
            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void LastEdited_IsTheDocumentOfTheLastGesture()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            host.Session.Nudge(1, 0);
            host.Session.Clear();

            Assert.Same(host.Document, host.Session.LastEdited);
        }

        /// <summary>
        /// A grid placement whose drop writes an attribute that isn't valid, so the move succeeds and the edit fails.
        /// </summary>
        private sealed class FailingPlacement : MarginPlacement
        {
            public override PlacementTarget? GetDropTarget(PlacementContext context, System.Numerics.Vector2 point) =>
                new(context.ContentCount, [new AttributeEdit("not a name", "1")], RectangleF.Empty, IndicatorIsLine: false);
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorGestureTests"`
Expected: the build FAILS (`BeginMove`, `MoveGesture`, `ResizeGesture` not found).

- [ ] **Step 3: Implement**

`GestureEdits.cs`, which applies attribute edits inside a transaction and rolls back on failure:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editor.Placement;

namespace Icy.Design.Editor
{
    /// <summary>
    /// Runs one gesture's edits as one undo step: a transaction that is undone as a whole when any edit fails.
    /// </summary>
    internal sealed class GestureEdits : IDisposable
    {
        private readonly DesignDocument document;
        private readonly IDisposable transaction;
        private readonly Dictionary<string, string?> applied = new(StringComparer.Ordinal);
        private bool edited;
        private bool closed;

        public GestureEdits(DesignDocument document, string description)
        {
            this.document = document;
            transaction = document.Editor.BeginTransaction(description);
        }

        /// <summary>
        /// Applies each edit whose value differs from what this gesture last wrote for that attribute.
        /// </summary>
        /// <returns>The first failure, or a success.</returns>
        public EditResult Apply(NodeId node, IEnumerable<AttributeEdit> edits)
        {
            foreach (AttributeEdit edit in edits)
            {
                if (applied.TryGetValue(edit.Name, out string? last) && last == edit.Value)
                    continue;

                EditResult result = edit.Value is { } value
                    ? document.Editor.SetAttribute(node, edit.Name, value)
                    : document.Editor.ClearAttribute(node, edit.Name);
                if (!result.Succeeded)
                    return result;

                applied[edit.Name] = edit.Value;
                edited = true;
            }

            return EditResult.Success();
        }

        /// <summary>
        /// Runs a structural edit inside the gesture.
        /// </summary>
        public EditResult Run(Func<EditResult> edit)
        {
            EditResult result = edit();
            if (result.Succeeded)
                edited = true;
            return result;
        }

        /// <summary>
        /// Closes the transaction, keeping its edits as one undo step.
        /// </summary>
        public void Commit()
        {
            if (!closed)
            {
                closed = true;
                transaction.Dispose();
            }
        }

        /// <summary>
        /// Closes the transaction and undoes it, leaving no undo entry.
        /// </summary>
        public void Rollback()
        {
            if (closed)
                return;

            closed = true;
            transaction.Dispose();
            if (edited)
            {
                document.Editor.UndoStack.Undo();
                document.Editor.UndoStack.DropRedo();
            }
        }

        public void Dispose() => Commit();
    }
}
```

`Rollback` needs `UndoStack.DropRedo()`, a new internal method in `sources/IcyUI.Design/UndoStack.cs` after `Clear`:

```csharp
        /// <summary>
        /// Forgets the redo history, after an undo that only took back an abandoned gesture.
        /// </summary>
        internal void DropRedo()
        {
            if (redo.Count == 0)
                return;

            redo.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
```

`MoveGesture.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// A move in progress: the selected element follows the pointer as a ghost, and lands where the container under the
    /// pointer places it when the gesture completes. Nothing is written before <see cref="Complete"/>.
    /// </summary>
    public sealed class MoveGesture
    {
        private readonly EditorSession session;
        private readonly EditorSelection selection;
        private readonly MarkupLoadScope scope;
        private readonly UIElement? sourceContainer;
        private readonly Point start;
        private readonly Rectangle startScreenBounds;
        private bool finished;

        internal MoveGesture(EditorSession session, EditorSelection selection, MarkupLoadScope scope, UIElement? sourceContainer, Point start)
        {
            this.session = session;
            this.selection = selection;
            this.scope = scope;
            this.sourceContainer = sourceContainer;
            this.start = start;
            UIElement element = selection.Instance;
            Point topLeft = element.PointToScreen(Vector2.Zero);
            Point bottomRight = element.PointToScreen(new Vector2(element.ActualBounds.Width, element.ActualBounds.Height));
            startScreenBounds = Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            GhostBounds = startScreenBounds;
        }

        /// <summary>
        /// Gets where the element would land, or <see langword="null"/> when nothing under the pointer can take it.
        /// </summary>
        public PlacementTarget? Target { get; private set; }

        /// <summary>
        /// Gets the container <see cref="Target"/> belongs to, or <see langword="null"/>.
        /// </summary>
        public UIElement? TargetContainer { get; private set; }

        /// <summary>
        /// Gets the dragged element's outline at the pointer, in screen space.
        /// </summary>
        public Rectangle GhostBounds { get; private set; }

        /// <summary>
        /// Moves the ghost and finds the drop target under <paramref name="screenPoint"/>.
        /// </summary>
        /// <param name="screenPoint">The pointer, in screen space.</param>
        public void Update(Point screenPoint)
        {
            if (finished)
                return;

            int dx = screenPoint.X - start.X;
            int dy = screenPoint.Y - start.Y;
            GhostBounds = startScreenBounds with { X = startScreenBounds.X + dx, Y = startScreenBounds.Y + dy };
            Target = null;
            TargetContainer = null;

            ElementSyntax? dragged = EditorSession.Syntax(selection);
            if (dragged == null)
                return;

            for (UIElement? candidate = session.ResolveSelectable(session.HitTest(screenPoint)); candidate != null; candidate = session.ResolveSelectable(candidate.Parent))
            {
                if (TryTarget(candidate, dragged, GhostBounds.Location))
                    return;
            }
        }

        /// <summary>
        /// Writes the move as one undo step, or does nothing when there's no target.
        /// </summary>
        /// <returns>The outcome; a failed edit rolls the whole move back.</returns>
        public EditResult Complete()
        {
            if (finished)
                return EditResult.Success();
            finished = true;

            if (Target is not { } target || TargetContainer is not { } container || session.IsBlocked)
                return EditResult.Success();
            if (session.Design.FindDocument(container, out NodeId containerId) != selection.Document)
                return EditResult.Success();

            var edits = new List<AttributeEdit>();
            bool changesContainer = !ReferenceEquals(container, sourceContainer);
            if (changesContainer && sourceContainer != null)
            {
                foreach (string owned in session.Placement.Resolve(sourceContainer).OwnedAttributes)
                    edits.Add(new AttributeEdit(owned, null));
            }

            edits.AddRange(target.Edits);
            var gesture = new GestureEdits(selection.Document, $"Move {EditorSession.Syntax(selection)?.Name}");
            EditResult moved = gesture.Run(() => selection.Document.Editor.MoveElement(selection.Node, containerId, target.Index));
            EditResult result = moved.Succeeded ? gesture.Apply(selection.Node, edits) : moved;
            if (result.Succeeded)
            {
                gesture.Commit();
                session.NoteEdited(selection.Document);
            }
            else
            {
                gesture.Rollback();
            }

            return result;
        }

        /// <summary>
        /// Abandons the move. Nothing was written, so nothing changes.
        /// </summary>
        public void Cancel()
        {
            finished = true;
            Target = null;
            TargetContainer = null;
        }

        private bool TryTarget(UIElement candidate, ElementSyntax dragged, Point ghostTopLeft)
        {
            DesignDocument document = selection.Document;
            if (ReferenceEquals(candidate, selection.Instance)
                || session.Design.FindDocument(candidate, out NodeId candidateId) != document
                || EditorSession.ScopeOf(document, candidate) != scope
                || document.GetNode(candidateId) is not { } candidateSyntax
                || ReferenceEquals(candidateSyntax, dragged)
                || dragged.IsAncestorOf(candidateSyntax)
                || !LiveAccepts(candidate))
            {
                return false;
            }

            var children = new List<PlacementChild>();
            int index = 0;
            foreach (ElementSyntax child in candidateSyntax.ContentElements)
            {
                if (ReferenceEquals(child, dragged))
                    continue;
                if (document.GetNodeId(child) is NodeId childId && EditorSession.FindInstance(document, childId, scope) is { } live)
                    children.Add(new PlacementChild(index, live));
                index++;
            }

            bool current = ReferenceEquals(candidate, sourceContainer);
            Rectangle bounds = current
                ? PlacementContext.ToLocal(candidate, selection.Instance.ActualBounds)
                : new Rectangle(0, 0, selection.Instance.ActualBounds.Width, selection.Instance.ActualBounds.Height);
            var context = new PlacementContext(candidate, selection.Instance, children, index, bounds, current);
            IPlacementStrategy candidateStrategy = session.Placement.Resolve(candidate);
            Vector2 local = candidate.PointToLocal(ghostTopLeft);
            if (candidateStrategy.GetDropTarget(context, local) is not { } found)
                return false;

            Target = found;
            TargetContainer = candidate;
            return true;
        }

        private bool LiveAccepts(UIElement candidate) =>
            Editing.LiveContent.TryResolve(candidate, selection.Document.Registry, out var member, out var list)
            && Editing.LiveContent.Accepts(member, list, selection.Instance.GetType());
    }
}
```

`ResizeGesture.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// A resize in progress: every <see cref="Update"/> writes the new attributes through the fast path, so the element
    /// follows the pointer live, and the whole gesture is one undo step.
    /// </summary>
    public sealed class ResizeGesture
    {
        private readonly EditorSession session;
        private readonly EditorSelection selection;
        private readonly UIElement container;
        private readonly IResizeOperation operation;
        private readonly GestureEdits edits;
        private readonly Vector2 start;
        private EditResult? failure;
        private bool finished;

        internal ResizeGesture(EditorSession session, EditorSelection selection, UIElement container, IResizeOperation operation, Point start)
        {
            this.session = session;
            this.selection = selection;
            this.container = container;
            this.operation = operation;
            this.start = container.PointToLocal(start);
            edits = new GestureEdits(selection.Document, $"Resize {EditorSession.Syntax(selection)?.Name}");
        }

        /// <summary>
        /// Resizes to the pointer at <paramref name="screenPoint"/>.
        /// </summary>
        /// <param name="screenPoint">The pointer, in screen space.</param>
        public void Update(Point screenPoint)
        {
            if (finished || failure != null)
                return;

            Vector2 delta = container.PointToLocal(screenPoint) - start;
            EditResult result = edits.Apply(selection.Node, operation.Update(delta));
            if (!result.Succeeded)
                failure = result;
        }

        /// <summary>
        /// Keeps the resize as one undo step.
        /// </summary>
        /// <returns>The outcome; when an update failed, the gesture is rolled back and the failure returned.</returns>
        public EditResult Complete()
        {
            if (finished)
                return failure ?? EditResult.Success();
            finished = true;

            if (failure != null)
            {
                edits.Rollback();
                return failure;
            }

            edits.Commit();
            session.NoteEdited(selection.Document);
            return EditResult.Success();
        }

        /// <summary>
        /// Abandons the resize and puts everything back, leaving no undo entry.
        /// </summary>
        public void Cancel()
        {
            if (finished)
                return;

            finished = true;
            edits.Rollback();
        }
    }
}
```

In `EditorSession`, add `using System.Drawing;`, `using Icy.Design.Editor.Placement;`, and:

```csharp
        /// <summary>
        /// Gets the placement strategies, by container type. Register strategies for custom containers here.
        /// </summary>
        public PlacementRegistry Placement { get; } = new();

        /// <summary>
        /// Gets the document the last gesture or command edited, or <see langword="null"/>. Undo and redo use it when
        /// nothing is selected.
        /// </summary>
        public DesignDocument? LastEdited { get; private set; }
```

(properties; place them with the others), and these methods after `Clear`:

```csharp
        /// <summary>
        /// Starts moving the selected element from <paramref name="screenPoint"/>.
        /// </summary>
        /// <param name="screenPoint">Where the pointer went down, in screen space.</param>
        /// <returns>
        /// The gesture, or <see langword="null"/> when nothing is selected, the selection is the root, or the session is
        /// <see cref="IsBlocked"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public MoveGesture? BeginMove(Point screenPoint)
        {
            ThrowIfDisposed();
            if (selection is not { } current || blocked || Syntax(current)?.Parent == null || ScopeOf(current.Document, current.Instance) is not { } scope)
                return null;

            return new MoveGesture(this, current, scope, FindLogicalParent(current), screenPoint);
        }

        /// <summary>
        /// Starts resizing the selected element from one of its handles.
        /// </summary>
        /// <param name="handle">The dragged handle.</param>
        /// <param name="screenPoint">Where the pointer went down, in screen space.</param>
        /// <returns>The gesture, or <see langword="null"/> when nothing is selected or the session is <see cref="IsBlocked"/>.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public ResizeGesture? BeginResize(ResizeHandle handle, Point screenPoint)
        {
            ThrowIfDisposed();
            if (selection is not { } current || blocked || handle == ResizeHandle.None)
                return null;

            // The root has no markup parent; its canvas slot works like a generic panel.
            UIElement container = FindLogicalParent(current) ?? (UIElement?)current.Instance.Parent ?? current.Instance;
            var context = new PlacementContext(container, current.Instance, [], 0, PlacementContext.ToLocal(container, current.Instance.ActualBounds), isCurrentContainer: true);
            IResizeOperation operation = ReferenceEquals(container, current.Instance)
                ? new MarginPlacement().BeginResize(context, handle)
                : Placement.Resolve(container).BeginResize(context, handle);
            return new ResizeGesture(this, current, container, operation, screenPoint);
        }

        /// <summary>
        /// Moves the selected element by its margin, alignment-aware, in any container.
        /// </summary>
        /// <param name="dx">The horizontal offset, in layout units.</param>
        /// <param name="dy">The vertical offset, in layout units.</param>
        /// <returns>The outcome; a failure when nothing is selected or the session is <see cref="IsBlocked"/>.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public EditResult Nudge(int dx, int dy)
        {
            ThrowIfDisposed();
            if (selection is not { } current)
                return EditResult.Failure(default, "Nothing is selected.");
            if (blocked)
                return EditResult.Failure(default, BlockedReason!);

            Thickness margin = LayoutMath.Move(current.Instance, current.Instance.Margin, dx, dy);
            using var gesture = new GestureEdits(current.Document, "Nudge");
            EditResult result = gesture.Apply(current.Node, [new AttributeEdit("Margin", MarkupValues.Format(margin))]);
            if (result.Succeeded)
                NoteEdited(current.Document);
            else
                gesture.Rollback();
            return result;
        }

        /// <summary>
        /// Removes the selected element and clears the selection.
        /// </summary>
        /// <returns>The outcome; a failure for the root, when nothing is selected, or while <see cref="IsBlocked"/>.</returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public EditResult DeleteSelection()
        {
            ThrowIfDisposed();
            if (selection is not { } current)
                return EditResult.Failure(default, "Nothing is selected.");
            if (blocked)
                return EditResult.Failure(default, BlockedReason!);

            EditResult result = current.Document.Editor.RemoveElement(current.Node);
            if (result.Succeeded)
                NoteEdited(current.Document);
            return result;
        }

        internal void NoteEdited(DesignDocument document) => LastEdited = document;
```

`using var gesture` commits on dispose, so `Nudge`'s explicit `Rollback` closes the transaction before the `using` disposes it (and `Commit` is then a no-op). Two nudges in a row are two undo steps; that's expected, since each is one gesture.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design"`
Expected: PASS, `EditorGestureTests` Total 13, and every other design test still passes.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1373, no failures), then the warning count (81).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/Editor/EditorGestureTests.cs
git commit -m "Add headless move, resize, nudge and delete gestures to the editor session

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `EditorCommands` and `EditorBindings`

**Files:**
- Create: `sources/IcyUI.Design/Editor/EditorCommand.cs`, `sources/IcyUI.Design/Editor/EditorCommands.cs`, `sources/IcyUI.Design/Editor/EditorBindings.cs`
- Modify: `sources/IcyUI.Design/Editor/EditorSession.cs` (`Commands`, `Bindings`, attach/dispose registration, `CanExecuteChanged` on mode/selection/blocked changes)
- Test: `sources/IcyUI.Tests/Design/Editor/EditorCommandTests.cs`

**Interfaces:**
- Consumes: Task 3's `RegisterCommand(..., handlesGesture: true)`, `UnregisterCommand(ICommand, KeyGesture)`, `FakeInputEventSystem.RaiseGesture`; Tasks 4 and 7.
- Produces:
  - `public sealed class EditorCommands` with `ICommand` properties `ToggleMode`, `Deselect`, `SelectParent`, `SelectFirstChild`, `SelectPrevious`, `SelectNext`, `Delete`, `Undo`, `Redo`, `NudgeLeft`, `NudgeRight`, `NudgeUp`, `NudgeDown`, `NudgeLeftLarge`, `NudgeRightLarge`, `NudgeUpLarge`, `NudgeDownLarge`;
  - `public sealed class EditorBindings : IEnumerable<KeyValuePair<KeyGesture, ICommand>>` with `void Bind(KeyGesture, ICommand)`, `bool Unbind(KeyGesture)`, indexer `ICommand? this[KeyGesture]`; changes while attached re-register the gesture;
  - `EditorSession.Commands`, `EditorSession.Bindings`; `EditorSession.LargeNudge` (`int`, default 10).

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Editor/EditorCommandTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editor;
using Icy.Input.Devices;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorCommandTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
              <StackPanel x:Name="group">
                <Border x:Name="first" Width="10" Height="10"/>
                <Border x:Name="second" Width="10" Height="10"/>
              </StackPanel>
            </StackPanel>
            """;

        [Fact]
        public void TheDefaultBindings_DriveTheCommands()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));

            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Down, ModifierKeys.Shift));

            Assert.Contains("Margin=\"1,10,0,0\"", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void SelectionCommands_WalkTheMarkupTree()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<StackPanel>("group"));

            Execute(host.Session.Commands.SelectFirstChild);
            Assert.Same(host.Named<Border>("first"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.SelectNext);
            Assert.Same(host.Named<Border>("second"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.SelectPrevious);
            Assert.Same(host.Named<Border>("first"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.SelectParent);
            Assert.Same(host.Named<StackPanel>("group"), host.Session.Selection!.Instance);

            Execute(host.Session.Commands.Deselect);
            Assert.Null(host.Session.Selection);
        }

        [Fact]
        public void Delete_And_Undo_Work_ThroughCommands()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            string before = host.Document.Text;

            Execute(host.Session.Commands.Delete);
            Assert.DoesNotContain("x:Name=\"a\"", host.Document.Text, StringComparison.Ordinal);

            // Nothing is selected now: undo uses the last edited document.
            Execute(host.Session.Commands.Undo);
            Assert.Equal(before, host.Document.Text);
        }

        [Fact]
        public void InteractMode_DisablesEverythingButTheToggle()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            host.Session.Mode = EditorMode.Interact;

            Assert.False(host.Session.Commands.Delete.CanExecute(null));
            Assert.False(host.Session.Commands.NudgeLeft.CanExecute(null));
            Assert.True(host.Session.Commands.ToggleMode.CanExecute(null));

            host.Input.Events.RaiseGesture(new KeyGesture(Keys.E, ModifierKeys.Ctrl | ModifierKeys.Shift));
            Assert.Equal(EditorMode.Edit, host.Session.Mode);
        }

        [Fact]
        public void WhileBlocked_EditingIsRefused_ButUndoAndSelectionWork()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);

            Assert.False(host.Session.Commands.Delete.CanExecute(null));
            Assert.False(host.Session.Commands.NudgeRight.CanExecute(null));
            Assert.True(host.Session.Commands.Undo.CanExecute(null));
            Assert.True(host.Session.Commands.Deselect.CanExecute(null));

            Execute(host.Session.Commands.Undo);
            Assert.False(host.Session.IsBlocked);
        }

        [Fact]
        public void EditorBindings_WinOverTheGamesBindingInEditMode()
        {
            using var host = new EditorTestHost(Page);
            int game = 0;
            var gameDelete = new FakeCommand(() => true, () => game++);
            host.Input.Events.RegisterCommand(gameDelete, new KeyGesture(Keys.Delete));
            host.Session.Select(host.Named<Border>("a"));

            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete));
            Assert.Equal(0, game);

            host.Session.Mode = EditorMode.Interact;
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete));
            Assert.Equal(1, game);
        }

        [Fact]
        public void Dispose_LeavesTheGamesOwnBindingsInPlace()
        {
            var host = new EditorTestHost(Page);
            int game = 0;
            host.Input.Events.RegisterCommand(new FakeCommand(() => true, () => game++), new KeyGesture(Keys.Delete));

            host.Dispose();
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete));

            Assert.Equal(1, game);
        }

        [Fact]
        public void RebindingWhileAttached_TakesEffectImmediately()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<Border>("a"));
            var gesture = new KeyGesture(Keys.D, ModifierKeys.Ctrl);

            host.Session.Bindings.Bind(gesture, host.Session.Commands.Delete);
            host.Input.Events.RaiseGesture(gesture);

            Assert.DoesNotContain("x:Name=\"a\"", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void TheDefaults_AvoidF1AndF2()
        {
            using var host = new EditorTestHost(Page);

            Assert.Null(host.Session.Bindings[new KeyGesture(Keys.F1)]);
            Assert.Null(host.Session.Bindings[new KeyGesture(Keys.F2)]);
            Assert.Same(host.Session.Commands.ToggleMode, host.Session.Bindings[new KeyGesture(Keys.E, ModifierKeys.Ctrl | ModifierKeys.Shift)]);
        }

        private static void Execute(System.Windows.Input.ICommand command)
        {
            Assert.True(command.CanExecute(null));
            command.Execute(null);
        }
    }
}
```

Check `Keys.D` and `Keys.F1`/`Keys.F2` exist in `Icy.Input.Devices.Keys` (`grep -n " D = \| F1 = \| F2 = " sources/IcyUI/Input/Devices/Keys.cs`); use whichever letter exists if the names differ.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorCommandTests"`
Expected: the build FAILS (`Commands`, `Bindings` not found).

- [ ] **Step 3: Implement**

`EditorCommand.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;

namespace Icy.Design.Editor
{
    /// <summary>
    /// An editor action as an <see cref="ICommand"/>, so keys, gamepads and toolbar buttons all invoke it the same way.
    /// </summary>
    internal sealed class EditorCommand(Func<bool> canExecute, Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter)
        {
            if (canExecute())
                execute();
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

`EditorCommands.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Windows.Input;
using Icy.Design.Syntax;

namespace Icy.Design.Editor
{
    /// <summary>
    /// Every action of an <see cref="EditorSession"/> as a command. Bind them to keys through
    /// <see cref="EditorSession.Bindings"/>, or invoke them from buttons, gamepads or touch gestures.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>In <see cref="EditorMode.Interact"/>, only <see cref="ToggleMode"/> can execute.</description></item>
    /// <item><description>Without a selection, only <see cref="ToggleMode"/>, <see cref="Undo"/> and <see cref="Redo"/> can execute.</description></item>
    /// <item><description>
    /// While <see cref="EditorSession.IsBlocked"/>, <see cref="Delete"/> and the nudges are refused; selection commands,
    /// <see cref="Undo"/> and <see cref="Redo"/> keep working, since undo is the way back out of broken markup.
    /// </description></item>
    /// </list>
    /// </remarks>
    public sealed class EditorCommands
    {
        private readonly EditorSession session;
        private readonly List<EditorCommand> all = [];

        internal EditorCommands(EditorSession session)
        {
            this.session = session;
            ToggleMode = Create(() => true, () => session.Mode = session.Mode == EditorMode.Edit ? EditorMode.Interact : EditorMode.Edit);
            Deselect = Create(Selecting, session.Clear);
            SelectParent = Create(Selecting, () => Move(x => x.Parent));
            SelectFirstChild = Create(Selecting, () => Move(x => x.ContentElements.FirstOrDefault()));
            SelectPrevious = Create(Selecting, () => Move(x => Sibling(x, -1)));
            SelectNext = Create(Selecting, () => Move(x => Sibling(x, 1)));
            Delete = Create(Editing, () => session.DeleteSelection());
            Undo = Create(() => Editable && Target()?.Editor.UndoStack.CanUndo == true, () => Target()!.Editor.UndoStack.Undo());
            Redo = Create(() => Editable && Target()?.Editor.UndoStack.CanRedo == true, () => Target()!.Editor.UndoStack.Redo());
            NudgeLeft = Nudge(-1, 0, large: false);
            NudgeRight = Nudge(1, 0, large: false);
            NudgeUp = Nudge(0, -1, large: false);
            NudgeDown = Nudge(0, 1, large: false);
            NudgeLeftLarge = Nudge(-1, 0, large: true);
            NudgeRightLarge = Nudge(1, 0, large: true);
            NudgeUpLarge = Nudge(0, -1, large: true);
            NudgeDownLarge = Nudge(0, 1, large: true);
        }

        /// <summary>Gets the command that switches between <see cref="EditorMode.Edit"/> and <see cref="EditorMode.Interact"/>.</summary>
        public ICommand ToggleMode { get; }

        /// <summary>Gets the command that clears the selection.</summary>
        public ICommand Deselect { get; }

        /// <summary>Gets the command that selects the nearest editable ancestor in markup.</summary>
        public ICommand SelectParent { get; }

        /// <summary>Gets the command that selects the first editable content child in markup.</summary>
        public ICommand SelectFirstChild { get; }

        /// <summary>Gets the command that selects the previous editable sibling in markup.</summary>
        public ICommand SelectPrevious { get; }

        /// <summary>Gets the command that selects the next editable sibling in markup.</summary>
        public ICommand SelectNext { get; }

        /// <summary>Gets the command that removes the selected element (never the root).</summary>
        public ICommand Delete { get; }

        /// <summary>Gets the command that undoes the selected document's last step, or the last edited document's when nothing is selected.</summary>
        public ICommand Undo { get; }

        /// <summary>Gets the command that redoes the selected document's last undone step, or the last edited document's when nothing is selected.</summary>
        public ICommand Redo { get; }

        /// <summary>Gets the command that moves the selected element one unit left.</summary>
        public ICommand NudgeLeft { get; }

        /// <summary>Gets the command that moves the selected element one unit right.</summary>
        public ICommand NudgeRight { get; }

        /// <summary>Gets the command that moves the selected element one unit up.</summary>
        public ICommand NudgeUp { get; }

        /// <summary>Gets the command that moves the selected element one unit down.</summary>
        public ICommand NudgeDown { get; }

        /// <summary>Gets the command that moves the selected element left by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeLeftLarge { get; }

        /// <summary>Gets the command that moves the selected element right by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeRightLarge { get; }

        /// <summary>Gets the command that moves the selected element up by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeUpLarge { get; }

        /// <summary>Gets the command that moves the selected element down by <see cref="EditorSession.LargeNudge"/>.</summary>
        public ICommand NudgeDownLarge { get; }

        private bool Editable => session.Mode == EditorMode.Edit;

        internal void RaiseCanExecuteChanged()
        {
            foreach (EditorCommand command in all)
                command.RaiseCanExecuteChanged();
        }

        private static ElementSyntax? Sibling(ElementSyntax element, int step)
        {
            if (element.Parent is not { } parent)
                return null;

            List<ElementSyntax> siblings = [.. parent.ContentElements];
            int index = siblings.IndexOf(element) + step;
            return index >= 0 && index < siblings.Count ? siblings[index] : null;
        }

        private bool Selecting() => Editable && session.Selection != null;

        private bool Editing() => Selecting() && !session.IsBlocked;

        private DesignDocument? Target() => session.Selection?.Document ?? session.LastEdited;

        private EditorCommand Create(Func<bool> canExecute, Action execute)
        {
            var command = new EditorCommand(canExecute, execute);
            all.Add(command);
            return command;
        }

        private EditorCommand Nudge(int x, int y, bool large) =>
            Create(Editing, () =>
            {
                int step = large ? session.LargeNudge : 1;
                session.Nudge(x * step, y * step);
            });

        /// <summary>
        /// Moves the selection along the markup tree to the first editable element <paramref name="next"/> reaches.
        /// </summary>
        private void Move(Func<ElementSyntax, ElementSyntax?> next)
        {
            EditorSelection current = session.Selection!;
            if (EditorSession.ScopeOf(current.Document, current.Instance) is not { } scope)
                return;

            // Walks past elements with no editable live copy (an untracked or opaque step) until one selects; the step
            // limit guards against a function that cycles.
            int limit = current.Document.Syntax.Elements.Count();
            ElementSyntax? element = EditorSession.Syntax(current) is { } start ? next(start) : null;
            for (int steps = 0; element != null && steps < limit; steps++, element = next(element))
            {
                if (current.Document.GetNodeId(element) is NodeId id
                    && EditorSession.FindInstance(current.Document, id, scope) is { } live
                    && session.Select(live))
                {
                    return;
                }
            }
        }
    }
}
```

`EditorBindings.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Windows.Input;
using Icy.Input;
using Icy.Input.Devices;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The key gestures an <see cref="EditorSession"/> registers for its <see cref="EditorCommands"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gestures are registered with <c>handlesGesture: true</c> (see
    /// <see cref="IInputEventSystem.RegisterCommand(ICommand, KeyGesture, object?, bool)"/>), so in
    /// <see cref="EditorMode.Edit"/> the editor's commands win over a game's own bindings for the same keys, and in
    /// <see cref="EditorMode.Interact"/> (when they can't execute) the game gets the keys back.
    /// </para>
    /// <para>The defaults:</para>
    /// <list type="table">
    /// <item><term>Ctrl+Shift+E</term><description><see cref="EditorCommands.ToggleMode"/>. F1/F2 are left to the game's debug tools.</description></item>
    /// <item><term>Esc</term><description><see cref="EditorCommands.Deselect"/></description></item>
    /// <item><term>Alt+Up / Alt+Down</term><description><see cref="EditorCommands.SelectParent"/> / <see cref="EditorCommands.SelectFirstChild"/></description></item>
    /// <item><term>Alt+Left / Alt+Right</term><description><see cref="EditorCommands.SelectPrevious"/> / <see cref="EditorCommands.SelectNext"/></description></item>
    /// <item><term>Delete</term><description><see cref="EditorCommands.Delete"/></description></item>
    /// <item><term>Ctrl+Z</term><description><see cref="EditorCommands.Undo"/></description></item>
    /// <item><term>Ctrl+Y, Ctrl+Shift+Z</term><description><see cref="EditorCommands.Redo"/></description></item>
    /// <item><term>Arrows / Shift+Arrows</term><description>The nudges, by 1 and by <see cref="EditorSession.LargeNudge"/>.</description></item>
    /// </list>
    /// <para>Changes made while the session is attached take effect immediately.</para>
    /// </remarks>
    public sealed class EditorBindings : IEnumerable<KeyValuePair<KeyGesture, ICommand>>
    {
        private readonly Dictionary<KeyGesture, ICommand> bindings = [];
        private IInputEventSystem? registered;

        internal EditorBindings(EditorCommands commands)
        {
            Bind(new KeyGesture(Keys.E, ModifierKeys.Ctrl | ModifierKeys.Shift), commands.ToggleMode);
            Bind(new KeyGesture(Keys.Escape), commands.Deselect);
            Bind(new KeyGesture(Keys.Up, ModifierKeys.Alt), commands.SelectParent);
            Bind(new KeyGesture(Keys.Down, ModifierKeys.Alt), commands.SelectFirstChild);
            Bind(new KeyGesture(Keys.Left, ModifierKeys.Alt), commands.SelectPrevious);
            Bind(new KeyGesture(Keys.Right, ModifierKeys.Alt), commands.SelectNext);
            Bind(new KeyGesture(Keys.Delete), commands.Delete);
            Bind(new KeyGesture(Keys.Z, ModifierKeys.Ctrl), commands.Undo);
            Bind(new KeyGesture(Keys.Y, ModifierKeys.Ctrl), commands.Redo);
            Bind(new KeyGesture(Keys.Z, ModifierKeys.Ctrl | ModifierKeys.Shift), commands.Redo);
            Bind(new KeyGesture(Keys.Left), commands.NudgeLeft);
            Bind(new KeyGesture(Keys.Right), commands.NudgeRight);
            Bind(new KeyGesture(Keys.Up), commands.NudgeUp);
            Bind(new KeyGesture(Keys.Down), commands.NudgeDown);
            Bind(new KeyGesture(Keys.Left, ModifierKeys.Shift), commands.NudgeLeftLarge);
            Bind(new KeyGesture(Keys.Right, ModifierKeys.Shift), commands.NudgeRightLarge);
            Bind(new KeyGesture(Keys.Up, ModifierKeys.Shift), commands.NudgeUpLarge);
            Bind(new KeyGesture(Keys.Down, ModifierKeys.Shift), commands.NudgeDownLarge);
        }

        /// <summary>
        /// Gets the command bound to <paramref name="gesture"/>, or <see langword="null"/>.
        /// </summary>
        /// <param name="gesture">The key gesture.</param>
        /// <returns>The bound command, or <see langword="null"/>.</returns>
        public ICommand? this[KeyGesture gesture] => bindings.GetValueOrDefault(gesture);

        /// <summary>
        /// Binds <paramref name="gesture"/> to <paramref name="command"/>, replacing any earlier binding for it.
        /// </summary>
        /// <param name="gesture">The key gesture.</param>
        /// <param name="command">The command; usually one of <see cref="EditorSession.Commands"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
        public void Bind(KeyGesture gesture, ICommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            Unbind(gesture);
            bindings[gesture] = command;
            registered?.RegisterCommand(command, gesture, null, handlesGesture: true);
        }

        /// <summary>
        /// Removes the binding for <paramref name="gesture"/>.
        /// </summary>
        /// <param name="gesture">The key gesture.</param>
        /// <returns><see langword="true"/> when a binding was removed.</returns>
        public bool Unbind(KeyGesture gesture)
        {
            if (!bindings.Remove(gesture, out ICommand? old))
                return false;

            registered?.UnregisterCommand(old, gesture);
            return true;
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<KeyGesture, ICommand>> GetEnumerator() => bindings.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal void Register(IInputEventSystem events)
        {
            registered = events;
            foreach ((KeyGesture gesture, ICommand command) in bindings)
                events.RegisterCommand(command, gesture, null, handlesGesture: true);
        }

        internal void Unregister()
        {
            if (registered == null)
                return;

            foreach ((KeyGesture gesture, ICommand command) in bindings)
                registered.UnregisterCommand(command, gesture);
            registered = null;
        }
    }
}
```

In `EditorSession`:
- add the properties (after `Placement`):

```csharp
        /// <summary>
        /// Gets the editor's actions as commands.
        /// </summary>
        public EditorCommands Commands { get; }

        /// <summary>
        /// Gets the key gestures bound to <see cref="Commands"/>. Rebind them before or after attaching.
        /// </summary>
        public EditorBindings Bindings { get; }

        /// <summary>
        /// Gets or sets how far the large nudges move, in layout units. Defaults to 10.
        /// </summary>
        public int LargeNudge { get; set; } = 10;
```

- in the constructor: `Commands = new EditorCommands(this); Bindings = new EditorBindings(Commands);`;
- in `Attach`, after `session.EnterEdit();`: `session.Bindings.Register(design.Configuration.Input.Events);` (`DesignSession.Configuration` is public);
- in `Dispose`, before `disposed = true;`: `Bindings.Unregister();`;
- call `Commands.RaiseCanExecuteChanged()` at the end of the `Mode` setter, `SetSelection` and `UpdateBlocked` (after their events), so buttons bound to the commands update.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Editor"`
Expected: PASS, `EditorCommandTests` Total 9, and every other editor test still passes.

- [ ] **Step 5: Full suite, warnings, commit**

Run:
```bash
dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"
```
Expected: 0 errors, 81 warnings. Total **1382** (1293 + 7 + 5 + 6 + 12 + 19 + 18 + 13 + 9), no failures.

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/Editor/EditorCommandTests.cs
git commit -m "Add the editor's commands and rebindable key bindings

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

| Spec section | Where it's implemented |
|---|---|
| 1.1 Grid spans | Task 1 |
| 1.2 `IsMouseOverGUI` | Task 2 |
| 1.3 single-command unregister, 1.4 first-wins dispatch | Task 3 |
| 1.5 `IsKeyboardNavigationEnabled` | Task 2 |
| 2 Lifetime, modes, hit resolution, selection, blocked | Task 4 |
| 2 Commands and bindings | Task 8 |
| 3 Registry, single-slot and list fallbacks, logical containers | Tasks 5, 7 |
| 3 StackPanel, Grid | Task 6 |
| 3 Values, one gesture = one undo step | Tasks 5, 7 |
| 5 Edge cases: root, removed selection, page loaded twice | Tasks 4, 7 |
| 4 `EditorFrame`, demo, performance measurement | 10.3b |

- **Deviations** are listed at the top.
