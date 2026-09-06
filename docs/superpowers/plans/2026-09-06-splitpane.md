# SplitPane (Tier-2 Phase 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `SplitPane`, a two-pane resizable layout control with a draggable divider, to `IcyUI`.

**Architecture:** `SplitPane : Control`. `Chrome` stays the default `Border` (background/border decoration) whose single `Child` is the divider - a two-level `Border` (an outer, wider hit-test band; an inner, thin visible line), positioned via `Width`/`Height` + `Margin` exactly like `Slider` positions its thumb. `First`/`Second` are two plain `UIElement?` properties managed *outside* `Chrome`, as extra elements in an overridden `GetVisualChildren`/`OnRender`/`ArrangeContent` - the same "chrome plus extra managed children" shape `ItemsControl` already uses for its realized containers, needed here because `Control`'s hard `(Border)Chrome` casts (`Background`/`BorderBrush`/etc.) mean the single `Chrome.Child` slot can't hold three independent visuals.

**Tech Stack:** C# / .NET (see `sources/IcyUI.sln`), xUnit for tests, IcyUI's own markup/styling system for templating and theming.

**Spec:** `docs/superpowers/specs/2026-09-06-splitpane-design.md`

## Global Constraints

- Divider drag uses the **single-target** `UIElement.OnDragStarted`/`OnDragPerforming`/`OnDragEnded` pattern (`Slider`'s own mechanism) - **not** the Phase 1 `IDragSource`/`IDropTarget` cross-container framework.
- Exactly two panes (`First`/`Second`) + one `Orientation` per instance. No native N-pane mode - 3+ regions come from nesting `SplitPane`s.
- `SplitterPosition` is a ratio (0-1) of available space, clamped to `[0,1]` by its own setter only - the `MinFirstSize`/`MinSecondSize` pixel clamp is a rendering/drag-time computation, not something the property setter enforces (see spec §3, §5).
- All public APIs get complete XML doc comments (standard notation - lists, `<see>`/`<paramref>`/`langword`) per project convention.
- No `IcyUI.MonoGame`/`IcyUI.Stride`/`IcyUI.FNA` changes are needed beyond the sample wiring in Task 5 - this is pure core-`IcyUI` layout/input, confirmed in the spec's Context section.

---

## Task 1: `SplitPane` skeleton - composition, `First`/`Second`, default divider

**Files:**
- Create: `sources/IcyUI/UI/Controls/SplitPane.cs`
- Test: `sources/IcyUI.Tests/Controls/SplitPaneTests.cs`

**Interfaces:**
- Consumes: `Icy.UI.Controls.Control` (`Chrome`, `ContentBounds`, `GetTemplateChild<T>`, `OnApplyTemplate`), `Icy.UI.Border` (`Child`, `Background`), `Icy.UI.Orientation` (existing enum, `Horizontal`/`Vertical`), `Icy.UI.UIElement` (`Parent`, `Canvas`, `Arrange`, `PointToLocal`, `InvalidateMeasure`/`InvalidateArrange`).
- Produces: `SplitPane` class with `Orientation`, `First`, `Second` properties and private fields `divider` (outer hit-band `Border`) / `dividerVisual` (inner visible-line `Border`) that Tasks 2-4 build on. `First`/`Second` setters wire `Parent`/`Canvas` directly (mirrors `Border.Child`'s setter) and call `InvalidateMeasure()`.

- [ ] **Step 1: Write the failing test for composition/wiring**

```csharp
// sources/IcyUI.Tests/Controls/SplitPaneTests.cs
using System.Linq;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SplitPaneTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var pane = new SplitPane();

            Assert.Equal(Orientation.Horizontal, pane.Orientation);
            Assert.Equal(0.5f, pane.SplitterPosition);
            Assert.Equal(0f, pane.MinFirstSize);
            Assert.Equal(0f, pane.MinSecondSize);
            Assert.Equal(6f, pane.DividerSize);
            Assert.Null(pane.First);
            Assert.Null(pane.Second);
        }

        [Fact]
        public void First_Set_WiresParentAndCanvas()
        {
            var pane = new SplitPane();
            var child = new Border();

            pane.First = child;

            Assert.Same(pane, child.Parent);
        }

        [Fact]
        public void Second_Set_WiresParentAndCanvas()
        {
            var pane = new SplitPane();
            var child = new Border();

            pane.Second = child;

            Assert.Same(pane, child.Parent);
        }

        [Fact]
        public void First_Replaced_UnparentsTheOldValue()
        {
            var pane = new SplitPane();
            var oldChild = new Border();
            var newChild = new Border();
            pane.First = oldChild;

            pane.First = newChild;

            Assert.Null(oldChild.Parent);
            Assert.Same(pane, newChild.Parent);
        }

        [Fact]
        public void GetVisualChildren_YieldsChromeThenFirstThenSecond()
        {
            var pane = new SplitPane();
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            var subtree = pane.EnumerateVisualSubtree().ToList();

            Assert.Contains(first, subtree);
            Assert.Contains(second, subtree);
            // The default divider (Chrome's Child) is present too, distinct from First/Second.
            Assert.True(subtree.Count(e => e is Border) >= 4); // Chrome + divider + inner line + First + Second
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: FAIL (`SplitPane` doesn't exist yet - compile error).

- [ ] **Step 3: Implement `SplitPane`'s skeleton**

```csharp
// sources/IcyUI/UI/Controls/SplitPane.cs
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Splits a region into two resizable areas (<see cref="First"/>/<see cref="Second"/>) separated by a
    /// draggable divider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Divider-grab is via <see cref="UIElement.OnDragStarted(Point)"/>/<see cref="UIElement.OnDragPerforming(Point)"/> -
    /// the same single-target "hold and move the pointer" mechanism <see cref="Slider"/>'s thumb uses, not the
    /// cross-container <see cref="Icy.Input.DragDrop.IDragSource"/>/<see cref="Icy.Input.DragDrop.IDropTarget"/>
    /// framework - a resize gesture has no drop target elsewhere.
    /// </para>
    /// <para>
    /// Exactly two panes and one <see cref="Orientation"/> per instance - a 3+ region layout is built by nesting
    /// <see cref="SplitPane"/>s, not a native N-pane mode.
    /// </para>
    /// <para>
    /// Template-safe via a named template part: a <see cref="Control.Template"/> that declares an element named
    /// <c>PART_Divider</c> (of any type assignable to <see cref="UI.Border"/>) has drag logic and
    /// <see cref="DividerBrush"/> wired to it instead of the built-in default divider - see <see cref="OnApplyTemplate"/>.
    /// </para>
    /// </remarks>
    public class SplitPane : Control
    {
        private const int DefaultDividerSize = 6;
        private const int DividerLineThickness = 2;

        private readonly Border defaultDivider;
        private readonly Border defaultDividerLine;
        private Border divider;
        private Border dividerVisual;
        private UIElement? first;
        private UIElement? second;
        private Orientation orientation = Orientation.Horizontal;
        private float splitterPosition = 0.5f;
        private float minFirstSize;
        private float minSecondSize;
        private float dividerSize = DefaultDividerSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitPane"/> class.
        /// </summary>
        public SplitPane()
        {
            defaultDividerLine = new Border
            {
                Background = new SolidColorBrush(Color.White),
            };
            defaultDivider = new Border
            {
                Background = new SolidColorBrush(Color.Transparent),
                Child = defaultDividerLine,
            };
            divider = defaultDivider;
            dividerVisual = defaultDividerLine;

            // Safe here, at construction: Chrome is always the default Border until/unless Template is set later,
            // in which case OnApplyTemplate re-wires (or re-attaches) divider/dividerVisual appropriately.
            ((Border)Chrome).Child = divider;
            ApplyDividerOrientation();
        }

        /// <summary>
        /// Gets or sets the axis <see cref="First"/>/<see cref="Second"/> are split along.
        /// </summary>
        /// <remarks>
        /// Matches <see cref="StackPanel.Orientation"/>'s own semantics: <see cref="Orientation.Horizontal"/>
        /// places <see cref="First"/>/<see cref="Second"/> side-by-side (a vertical-line divider);
        /// <see cref="Orientation.Vertical"/> stacks them (a horizontal-line divider).
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(Orientation.Horizontal)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public Orientation Orientation
        {
            get => orientation;
            set
            {
                if (SetProperty(ref orientation, value))
                {
                    ApplyDividerOrientation();
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the element displayed before the divider (left, when <see cref="Orientation"/> is
        /// <see cref="Orientation.Horizontal"/>; top, when <see cref="Orientation.Vertical"/>).
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? First
        {
            get => first;
            set
            {
                if (first == value)
                    return;
                if (first != null)
                {
                    first.Parent = null;
                    first.Canvas = null;
                }

                first = value;
                if (first != null)
                {
                    first.Parent = this;
                    first.Canvas = Canvas;
                }

                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets the element displayed after the divider (right, when <see cref="Orientation"/> is
        /// <see cref="Orientation.Horizontal"/>; bottom, when <see cref="Orientation.Vertical"/>).
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public UIElement? Second
        {
            get => second;
            set
            {
                if (second == value)
                    return;
                if (second != null)
                {
                    second.Parent = null;
                    second.Canvas = null;
                }

                second = value;
                if (second != null)
                {
                    second.Parent = this;
                    second.Canvas = Canvas;
                }

                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets the fraction (0-1) of available space, along <see cref="Orientation"/>'s axis, allocated
        /// to <see cref="First"/>.
        /// </summary>
        /// <remarks>
        /// Clamped to <c>[0,1]</c> only - <see cref="MinFirstSize"/>/<see cref="MinSecondSize"/> are enforced
        /// separately, at arrange/drag time, since they depend on the control's current size (see
        /// <see cref="ArrangeContent"/>).
        /// </remarks>
        [Category("Layout")]
        [DefaultValue(0.5f)]
        [RegisterReference]
        [AffectsArrange]
        public float SplitterPosition
        {
            get => splitterPosition;
            set
            {
                if (SetProperty(ref splitterPosition, float.Clamp(value, 0, 1)))
                {
                    InvalidateArrange();
                    SplitterPositionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets the minimum size, in pixels along <see cref="Orientation"/>'s axis, <see cref="First"/>
        /// keeps regardless of <see cref="SplitterPosition"/>.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(0f)]
        [RegisterReference]
        [AffectsArrange]
        public float MinFirstSize
        {
            get => minFirstSize;
            set
            {
                if (SetProperty(ref minFirstSize, value))
                    InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the minimum size, in pixels along <see cref="Orientation"/>'s axis, <see cref="Second"/>
        /// keeps regardless of <see cref="SplitterPosition"/>.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(0f)]
        [RegisterReference]
        [AffectsArrange]
        public float MinSecondSize
        {
            get => minSecondSize;
            set
            {
                if (SetProperty(ref minSecondSize, value))
                    InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the divider's hit-test band width, in pixels along <see cref="Orientation"/>'s axis - the
        /// grabbable area is this wide even though the visible line drawn inside it is thinner (see
        /// <see cref="DividerBrush"/>).
        /// </summary>
        [Category("Layout")]
        [DefaultValue((float)DefaultDividerSize)]
        [RegisterReference]
        [AffectsMeasure]
        [AffectsArrange]
        public float DividerSize
        {
            get => dividerSize;
            set
            {
                if (SetProperty(ref dividerSize, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the brush used to paint the divider's visible line - the currently-live one (the default
        /// built-in line, or a <see cref="Control.Template"/>'s own <c>PART_Divider</c> once
        /// <see cref="OnApplyTemplate"/> has repointed it).
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public IBrush DividerBrush
        {
            get => dividerVisual.Background;
            set => dividerVisual.Background = value;
        }

        /// <summary>
        /// Occurs when <see cref="SplitterPosition"/> changes.
        /// </summary>
        public event EventHandler? SplitterPositionChanged;

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            yield return Chrome;
            if (First != null)
                yield return First;
            if (Second != null)
                yield return Second;
        }

        /// <inheritdoc/>
        protected override void OnRender(Icy.Rendering.IRenderContext context)
        {
            Chrome.Draw(context);
            First?.Draw(context);
            Second?.Draw(context);
        }

        private void ApplyDividerOrientation()
        {
            bool horizontal = Orientation == Orientation.Horizontal;

            divider.HorizontalAlignment = horizontal ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            divider.VerticalAlignment = horizontal ? VerticalAlignment.Stretch : VerticalAlignment.Top;
            divider.Width = horizontal ? DividerSize : float.NaN;
            divider.Height = horizontal ? float.NaN : DividerSize;

            if (dividerVisual == defaultDividerLine)
            {
                dividerVisual.HorizontalAlignment = horizontal ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
                dividerVisual.VerticalAlignment = horizontal ? VerticalAlignment.Stretch : VerticalAlignment.Center;
                dividerVisual.Width = horizontal ? DividerLineThickness : float.NaN;
                dividerVisual.Height = horizontal ? float.NaN : DividerLineThickness;
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: PASS (all 5 facts green). `Defaults_MatchSpec`/`GetVisualChildren_...` pass because `ArrangeContent`/`MeasureContent` aren't overridden yet - `Control`'s base implementation (`Chrome.Arrange`/`Chrome.Measure`) is still in effect, which is fine for this task; Task 2 replaces it.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/SplitPane.cs sources/IcyUI.Tests/Controls/SplitPaneTests.cs
git commit -m "Add SplitPane skeleton: composition, First/Second, default divider"
```

---

## Task 2: Layout algorithm - `MeasureContent`/`ArrangeContent`

**Files:**
- Modify: `sources/IcyUI/UI/Controls/SplitPane.cs`
- Modify: `sources/IcyUI.Tests/Controls/SplitPaneTests.cs`

**Interfaces:**
- Consumes: Task 1's `divider`/`dividerVisual` fields, `Orientation`/`SplitterPosition`/`MinFirstSize`/`MinSecondSize`/`DividerSize`/`First`/`Second`; `Control.ContentBounds` (`ActualBounds - BorderThickness - Padding`); `UIElement.Arrange(Rectangle)`, `UIElement.Measure()`.
- Produces: `ArrangeContent`/`MeasureContent` overrides. Later tasks (drag, templating) read the same divider-position math via a new private `ResolveDividerOffset(Rectangle content)` helper this task introduces.

- [ ] **Step 1: Write the failing tests for arrange math**

```csharp
// Add to SplitPaneTests.cs
using System.Drawing;

// ... inside the SplitPaneTests class:

[Fact]
public void ArrangeContent_Horizontal_SplitsProportionally()
{
    var pane = new SplitPane { Width = 200, Height = 100, SplitterPosition = 0.5f };
    var first = new Border();
    var second = new Border();
    pane.First = first;
    pane.Second = second;

    pane.Arrange(new Rectangle(0, 0, 200, 100));

    // available = 200, roaming = 200 - DividerSize(6) = 194; firstWidth = 194 * 0.5 = 97.
    // Second starts after First AND the divider band: 97 + DividerSize(6) = 103.
    Assert.Equal(97, first.ActualBounds.Width);
    Assert.Equal(100, first.ActualBounds.Height);
    Assert.Equal(103, second.ActualBounds.X);
    Assert.Equal(200 - 97 - 6, second.ActualBounds.Width);
}

[Fact]
public void ArrangeContent_Vertical_SplitsAlongHeight()
{
    var pane = new SplitPane { Width = 100, Height = 200, Orientation = Orientation.Vertical, SplitterPosition = 0.25f };
    var first = new Border();
    var second = new Border();
    pane.First = first;
    pane.Second = second;

    pane.Arrange(new Rectangle(0, 0, 100, 200));

    // roaming = 200 - 6 = 194; firstHeight = 194 * 0.25 = 48 (truncated).
    Assert.Equal(48, first.ActualBounds.Height);
    Assert.Equal(100, first.ActualBounds.Width);
    Assert.Equal(48 + 6, second.ActualBounds.Y);
}

[Fact]
public void ArrangeContent_RespectsMinFirstAndMinSecondSize()
{
    var pane = new SplitPane
    {
        Width = 200,
        Height = 100,
        SplitterPosition = 0.05f, // would put First far below MinFirstSize without clamping
        MinFirstSize = 50,
        MinSecondSize = 50,
    };
    var first = new Border();
    var second = new Border();
    pane.First = first;
    pane.Second = second;

    pane.Arrange(new Rectangle(0, 0, 200, 100));

    Assert.True(first.ActualBounds.Width >= 50, $"First was {first.ActualBounds.Width}, expected >= 50");
    Assert.True(second.ActualBounds.Width >= 50, $"Second was {second.ActualBounds.Width}, expected >= 50");
}

[Fact]
public void ArrangeContent_UndersizedContainer_DegradesWithoutThrowing()
{
    // MinFirstSize + MinSecondSize + DividerSize (50+50+6=106) exceeds the 80px available - must not throw,
    // and must still produce a stable (if imperfect) split.
    var pane = new SplitPane { Width = 80, Height = 100, MinFirstSize = 50, MinSecondSize = 50 };
    pane.First = new Border();
    pane.Second = new Border();

    var exception = Record.Exception(() => pane.Arrange(new Rectangle(0, 0, 80, 100)));

    Assert.Null(exception);
}

[Fact]
public void MeasureContent_SumsFirstAndSecondPlusDividerSize()
{
    var pane = new SplitPane { DividerSize = 6 };
    pane.First = new Border { Width = 80, Height = 40 };
    pane.Second = new Border { Width = 60, Height = 30 };

    Size measured = pane.Measure();

    Assert.Equal(80 + 60 + 6, measured.Width);
    Assert.Equal(40, measured.Height);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: FAIL - `Control`'s default `ArrangeContent`/`MeasureContent` (which just delegate to `Chrome`) don't touch `First`/`Second` at all, so their `ActualBounds` stay at the default `Rectangle.Empty`.

- [ ] **Step 3: Implement `MeasureContent`/`ArrangeContent`**

```csharp
// Add to SplitPane, after ApplyDividerOrientation:

/// <inheritdoc/>
protected override void ArrangeContent()
{
    Rectangle content = ContentBounds;
    bool horizontal = Orientation == Orientation.Horizontal;
    float available = horizontal ? content.Width : content.Height;
    int dividerOffset = ResolveDividerOffset(available);

    if (horizontal)
    {
        First?.Arrange(new Rectangle(content.X, content.Y, dividerOffset, content.Height));
        divider.Margin = new Thickness(dividerOffset, 0, 0, 0);
        Second?.Arrange(new Rectangle(
            content.X + dividerOffset + (int)DividerSize, content.Y,
            Math.Max(0, content.Width - dividerOffset - (int)DividerSize), content.Height));
    }
    else
    {
        First?.Arrange(new Rectangle(content.X, content.Y, content.Width, dividerOffset));
        divider.Margin = new Thickness(0, dividerOffset, 0, 0);
        Second?.Arrange(new Rectangle(
            content.X, content.Y + dividerOffset + (int)DividerSize,
            content.Width, Math.Max(0, content.Height - dividerOffset - (int)DividerSize)));
    }

    Chrome.Arrange(ActualBounds);
}

/// <inheritdoc/>
protected override Size MeasureContent()
{
    Size firstSize = First?.Measure() ?? Size.Empty;
    Size secondSize = Second?.Measure() ?? Size.Empty;
    bool horizontal = Orientation == Orientation.Horizontal;

    return horizontal
        ? new Size((int)(firstSize.Width + secondSize.Width + DividerSize), Math.Max(firstSize.Height, secondSize.Height))
        : new Size(Math.Max(firstSize.Width, secondSize.Width), (int)(firstSize.Height + secondSize.Height + DividerSize));
}

/// <summary>
/// Computes the divider's pixel offset from the start of <paramref name="available"/>, applying
/// <see cref="SplitterPosition"/> and clamping to <see cref="MinFirstSize"/>/<see cref="MinSecondSize"/>.
/// </summary>
/// <param name="available">The full content extent along <see cref="Orientation"/>'s axis.</param>
/// <returns>
/// The pixel offset where <see cref="First"/> ends and the divider begins. Degrades gracefully (splits the
/// midpoint of whatever range remains) when <see cref="MinFirstSize"/> + <see cref="MinSecondSize"/> +
/// <see cref="DividerSize"/> exceeds <paramref name="available"/>, rather than throwing.
/// </returns>
private int ResolveDividerOffset(float available)
{
    float roaming = Math.Max(0, available - DividerSize);
    float rawPos = SplitterPosition * roaming;
    float minPos = MinFirstSize;
    float maxPos = roaming - MinSecondSize;

    float pos = maxPos >= minPos ? float.Clamp(rawPos, minPos, maxPos) : (minPos + maxPos) / 2f;
    return (int)Math.Max(0, pos);
}
```

Add `using System;` and `using System.Collections.Generic;` to the top of `SplitPane.cs` if not already present (needed for `Math`/`IEnumerable<T>`).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: PASS (all facts, including the previous task's).

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/SplitPane.cs sources/IcyUI.Tests/Controls/SplitPaneTests.cs
git commit -m "Implement SplitPane's Measure/Arrange layout algorithm"
```

---

## Task 3: Divider drag

**Files:**
- Modify: `sources/IcyUI/UI/Controls/SplitPane.cs`
- Modify: `sources/IcyUI.Tests/Controls/SplitPaneTests.cs`

**Interfaces:**
- Consumes: `UIElement.OnDragStarted(Point)`/`OnDragPerforming(Point)`/`OnDragEnded(Point)`, `UIElement.PointToLocal(Point)`, `Control.Padding`/`Control.BorderThickness`, Task 2's `ResolveDividerOffset` (reused for the inverse: turning a drag point into a ratio), `Icy.Tests.Input.FakeInputSystem` (existing test double, same one `SliderTests` uses).
- Produces: dragging the divider (hit-tested the same way `Slider`'s thumb is - `Canvas` locks the whole gesture to whatever was hit at drag-start) sets `SplitterPosition`.

- [ ] **Step 1: Write the failing drag tests**

```csharp
// Add to SplitPaneTests.cs
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;

// ... inside SplitPaneTests:

private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
{
    var input = new FakeInputSystem();
    var renderContext = new FakeRenderContext();
    var assets = new AssetConfiguration(AssetContext.ApplicationContext);
    var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
    return (new Canvas(config), input);
}

[Fact]
public void Drag_UpdatesSplitterPositionBasedOnPointerPosition()
{
    var (canvas, input) = CreateCanvas();
    canvas.IsInputEnabled = true;
    canvas.IsVisible = true;
    var pane = new SplitPane
    {
        Width = 200,
        Height = 100,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        First = new Border(),
        Second = new Border(),
    };
    canvas.Add(pane);
    canvas.Render();

    // Divider sits at roaming(194)*0.5=97..103 after the first render (SplitterPosition starts at 0.5).
    input.Events.Drag.RaiseDragStarted(new Point(100, 50));
    input.Events.Drag.RaiseDragPerforming(new Point(150, 50));

    // roaming = 194; ratio = 150/194 (PointToLocal aligns with content origin here since Padding/BorderThickness are 0).
    Assert.True(pane.SplitterPosition > 0.5f, $"Expected SplitterPosition to increase past 0.5, got {pane.SplitterPosition}");
}

[Fact]
public void Drag_BeyondPaneEdge_ClampsToOne()
{
    var (canvas, input) = CreateCanvas();
    canvas.IsInputEnabled = true;
    canvas.IsVisible = true;
    var pane = new SplitPane
    {
        Width = 200,
        Height = 100,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        First = new Border(),
        Second = new Border(),
    };
    canvas.Add(pane);
    canvas.Render();

    input.Events.Drag.RaiseDragStarted(new Point(100, 50));
    input.Events.Drag.RaiseDragPerforming(new Point(1000, 50));

    Assert.Equal(1f, pane.SplitterPosition);
}

[Fact]
public void Drag_RespectsMinSecondSize()
{
    var (canvas, input) = CreateCanvas();
    canvas.IsInputEnabled = true;
    canvas.IsVisible = true;
    var pane = new SplitPane
    {
        Width = 200,
        Height = 100,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        MinSecondSize = 50,
        First = new Border(),
        Second = new Border(),
    };
    canvas.Add(pane);
    canvas.Render();

    input.Events.Drag.RaiseDragStarted(new Point(100, 50));
    input.Events.Drag.RaiseDragPerforming(new Point(1000, 50));

    pane.Arrange(new Rectangle(0, 0, 200, 100));
    Assert.True(pane.Second!.ActualBounds.Width >= 50, $"Second was {pane.Second.ActualBounds.Width}, expected >= 50");
}

[Fact]
public void SplitterPositionChanged_FiresOnce_PerActualChange()
{
    var (canvas, input) = CreateCanvas();
    canvas.IsInputEnabled = true;
    canvas.IsVisible = true;
    var pane = new SplitPane
    {
        Width = 200,
        Height = 100,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        First = new Border(),
        Second = new Border(),
    };
    canvas.Add(pane);
    canvas.Render();
    // Drag-start alone already moves SplitterPosition off its 0.5 default (100/194 != 0.5) and would raise the
    // event once, unobserved - attach the handler only after that settles, so exactly one more move (drag-
    // performing) isolates exactly one raise, rather than conflating both changes into an ambiguous count.
    input.Events.Drag.RaiseDragStarted(new Point(100, 50));
    canvas.Render();
    int raiseCount = 0;
    pane.SplitterPositionChanged += (_, _) => raiseCount++;

    input.Events.Drag.RaiseDragPerforming(new Point(150, 50));

    Assert.Equal(1, raiseCount);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: FAIL - `SplitPane` doesn't override the drag hooks yet, so `SplitterPosition` never moves from its `0.5f` default.

- [ ] **Step 3: Implement the drag overrides**

```csharp
// Add to SplitPane:
using System.Numerics;

private bool isDragging;

/// <inheritdoc/>
protected internal override void OnDragEnded(Point screenPoint)
{
    base.OnDragEnded(screenPoint);
    isDragging = false;
}

/// <inheritdoc/>
protected internal override void OnDragPerforming(Point screenPoint)
{
    base.OnDragPerforming(screenPoint);
    if (isDragging)
        UpdatePositionFromPoint(screenPoint);
}

/// <inheritdoc/>
protected internal override void OnDragStarted(Point screenPoint)
{
    base.OnDragStarted(screenPoint);
    isDragging = true;
    UpdatePositionFromPoint(screenPoint);
}

private void UpdatePositionFromPoint(Point screenPoint)
{
    Vector2 local = PointToLocal(screenPoint);
    bool horizontal = Orientation == Orientation.Horizontal;
    float inset = horizontal ? Padding.Left + BorderThickness.Left : Padding.Top + BorderThickness.Top;
    float localAxis = horizontal ? local.X : local.Y;
    Rectangle content = ContentBounds;
    float available = horizontal ? content.Width : content.Height;
    float roaming = Math.Max(1, available - DividerSize);

    float minRatio = float.Clamp(MinFirstSize / roaming, 0, 1);
    float maxRatio = float.Clamp(1 - (MinSecondSize / roaming), 0, 1);
    if (maxRatio < minRatio)
        (minRatio, maxRatio) = ((minRatio + maxRatio) / 2f, (minRatio + maxRatio) / 2f);

    float rawRatio = (localAxis - inset) / roaming;
    SplitterPosition = float.Clamp(rawRatio, minRatio, maxRatio);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: PASS (all facts so far).

- [ ] **Step 5: Run the full test suite (regression check)**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS - no other suite touches `SplitPane`, this is a pure regression guard.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/SplitPane.cs sources/IcyUI.Tests/Controls/SplitPaneTests.cs
git commit -m "Add SplitPane divider drag (single-target, mirrors Slider)"
```

---

## Task 4: Templating (`PART_Divider`) + `DefaultTheme.xml` entry

**Files:**
- Modify: `sources/IcyUI/UI/Controls/SplitPane.cs`
- Modify: `sources/IcyUI/Resources/Themes/DefaultTheme.xml`
- Modify: `sources/IcyUI.Tests/Controls/SplitPaneTests.cs`

**Interfaces:**
- Consumes: `Control.OnApplyTemplate`, `Control.GetTemplateChild<T>(string)`, `Control.Template` (existing swap machinery - snapshot/restore of `Background`/`BorderBrush`/`BorderThickness`/`Padding` already works unmodified since `SplitPane` doesn't override `CaptureTemplateState`/`RestoreTemplateState`: `First`/`Second` are never chrome-owned, so nothing about them needs preserving across a template swap).
- Produces: `OnApplyTemplate` override; `IcyDefaultSplitPaneTemplate`/`SplitPane` style entries in `DefaultTheme.xml`.

- [ ] **Step 1: Write the failing template tests**

```csharp
// Add to SplitPaneTests.cs
using Icy.Markup;
using Icy.UI.Styles;

// ... inside SplitPaneTests:

private static ControlTemplate LoadTemplate(string markup)
{
    var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
    var loader = new MarkupLoader(configuration);
    return (ControlTemplate)loader.LoadObject(markup);
}

[Fact]
public void Template_WithPartDivider_ArrangePositionsTheTemplatesOwnDivider()
{
    var template = LoadTemplate(
        """
        <ControlTemplate TargetType="SplitPane">
          <Border>
            <Border x:Name="PART_Divider" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
          </Border>
        </ControlTemplate>
        """);
    var pane = new SplitPane { Template = template, Width = 200, Height = 100, First = new Border(), Second = new Border() };

    pane.Arrange(new Rectangle(0, 0, 200, 100));

    var templatedDivider = pane.EnumerateVisualSubtree().OfType<Border>().Single(b => b.Width == 6);
    Assert.Equal(97, templatedDivider.Margin.Left); // roaming(194)*0.5 = 97, same math as Task 2's arrange test.
}

[Fact]
public void Template_WithPartDivider_DragUpdatesSplitterPositionAndTheTemplatesOwnDivider()
{
    var template = LoadTemplate(
        """
        <ControlTemplate TargetType="SplitPane">
          <Border>
            <Border x:Name="PART_Divider" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
          </Border>
        </ControlTemplate>
        """);
    var (canvas, input) = CreateCanvas();
    canvas.IsInputEnabled = true;
    canvas.IsVisible = true;
    var pane = new SplitPane
    {
        Template = template,
        Width = 200,
        Height = 100,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        First = new Border(),
        Second = new Border(),
    };
    canvas.Add(pane);
    canvas.Render();

    input.Events.Drag.RaiseDragStarted(new Point(100, 50));
    input.Events.Drag.RaiseDragPerforming(new Point(150, 50));
    canvas.Render();

    Assert.True(pane.SplitterPosition > 0.5f);
    var templatedDivider = pane.EnumerateVisualSubtree().OfType<Border>().Single(b => b.Width == 6);
    Assert.True(templatedDivider.Margin.Left > 97);
}

[Fact]
public void Template_WithoutPartDivider_StillArrangesWithoutThrowing()
{
    var template = LoadTemplate("""<ControlTemplate TargetType="SplitPane"><Border/></ControlTemplate>""");
    var pane = new SplitPane { Template = template, Width = 200, Height = 100, First = new Border(), Second = new Border() };

    var exception = Record.Exception(() => pane.Arrange(new Rectangle(0, 0, 200, 100)));

    Assert.Null(exception);
}

[Fact]
public void Template_ClearedAfterBeingSet_RestoresDefaultDividerBehavior()
{
    var template = LoadTemplate(
        """
        <ControlTemplate TargetType="SplitPane">
          <Border>
            <Border x:Name="PART_Divider" Width="6" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
          </Border>
        </ControlTemplate>
        """);
    var pane = new SplitPane { Template = template, Width = 200, Height = 100, First = new Border(), Second = new Border() };
    pane.Arrange(new Rectangle(0, 0, 200, 100));

    pane.Template = null;
    pane.Arrange(new Rectangle(0, 0, 200, 100));

    // Back on the default divider - DividerBrush must route to a real element actually in the visual tree
    // (the default inner line), not the orphaned template's PART_Divider.
    var newBrush = new Icy.Rendering.Brushes.SolidColorBrush(Color.Red);
    pane.DividerBrush = newBrush;

    Assert.Same(newBrush, pane.DividerBrush);
    Assert.Contains(pane.EnumerateVisualSubtree().OfType<Border>(), b => ReferenceEquals(b.Background, newBrush));
}

[Fact]
public void DividerBrush_DefaultsToWhiteAndIsSettable()
{
    var pane = new SplitPane();

    var defaultBrush = Assert.IsType<Icy.Rendering.Brushes.SolidColorBrush>(pane.DividerBrush);
    Assert.Equal(Color.White.ToArgb(), defaultBrush.Color.ToArgb());

    var newBrush = new Icy.Rendering.Brushes.SolidColorBrush(Color.Red);
    pane.DividerBrush = newBrush;

    Assert.Same(newBrush, pane.DividerBrush);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: FAIL - no `OnApplyTemplate` override yet, so a `PART_Divider` in a template is never actually wired to drag/position logic (the built-in `divider`/`dividerVisual` fields stay authoritative regardless of `Template`).

- [ ] **Step 3: Implement `OnApplyTemplate`**

```csharp
// Add to SplitPane:

/// <inheritdoc/>
/// <remarks>
/// Repoints <c>divider</c>/<c>dividerVisual</c> - the elements <see cref="ArrangeContent"/>/
/// <see cref="UpdatePositionFromPoint"/> already manipulate by reference - to whichever <see cref="UI.Border"/>
/// is actually live right now: <see cref="Control.Template"/>'s own <c>PART_Divider</c> when one is set,
/// falling back to the built-in default divider otherwise (mirrors <see cref="Slider.OnApplyTemplate"/> exactly).
/// A template's <c>PART_Divider</c> with no nested <see cref="UI.Border.Child"/> of its own gets
/// <see cref="DividerBrush"/> routed directly onto it instead of a nested line - graceful degradation, matching
/// how a template with no <c>PART_Divider</c> at all still applies (just with drag/position wired to nothing
/// the template actually shows, exactly as an untemplated-part <see cref="Slider"/> already behaves).
/// </remarks>
protected override void OnApplyTemplate()
{
    base.OnApplyTemplate();

    if (Template != null && GetTemplateChild<Border>("PART_Divider") is { } part)
    {
        divider = part;
        dividerVisual = part.Child as Border ?? part;
        ApplyDividerOrientation();
        return;
    }

    divider = defaultDivider;
    dividerVisual = defaultDividerLine;
    if (Template == null)
        ((Border)Chrome).Child = divider;
    ApplyDividerOrientation();
}
```

Guard `ApplyDividerOrientation`'s `dividerVisual == defaultDividerLine` check already written in Task 1 so it only auto-sizes the inner line when it's actually the built-in one - a template-supplied `PART_Divider`'s own child (or itself, in the no-nested-child fallback) keeps whatever `Width`/`Height`/alignment the template author gave it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SplitPaneTests"`
Expected: PASS (all facts).

- [ ] **Step 5: Add the `DefaultTheme.xml` entry**

Insert after the `ProgressBar` entry (`sources/IcyUI/Resources/Themes/DefaultTheme.xml`, right before the `TextBox, ScrollViewer, Window` comment):

```xml
  <!-- SplitPane -->
  <ControlTemplate x:Key="IcyDefaultSplitPaneTemplate" TargetType="SplitPane">
    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
      <Border x:Name="PART_Divider" Background="Transparent">
        <Border Background="{TemplateBinding DividerBrush}"/>
      </Border>
    </Border>
  </ControlTemplate>

  <Style TargetType="SplitPane" Template="{StaticResource IcyDefaultSplitPaneTemplate}"
         Background="Transparent" BorderBrush="#FF56566A" BorderThickness="0" DividerBrush="#FF56566A">
    <Style.StateGroups>
      <VisualStateGroup Name="CommonStates">
        <VisualState Name="Hovered" State="Hovered" DividerBrush="#FF6A6A80"/>
        <VisualState Name="Pressed" State="Pressed" DividerBrush="#FF3C78D8"/>
      </VisualStateGroup>
    </Style.StateGroups>
  </Style>

```

Note: this theme `PART_Divider`'s nested `Border` has no explicit `Width`/`Margin`/alignment - `SplitPane.ApplyDividerOrientation`'s fallback (`dividerVisual = part.Child as Border ?? part`) only auto-sizes the *default*, code-constructed inner line (per the guard in Task 1/Step 3), not a template-supplied one, so the nested `Border` here would stretch to fill `PART_Divider`'s full band unless sized. Give it the same centered-thin-line treatment explicitly in the markup instead: `HorizontalAlignment="Center" Width="2"` (Horizontal-orientation look; since `DefaultTheme.xml` has no per-orientation branching, this theme entry targets the common `Orientation.Horizontal` case - a `Vertical` `SplitPane` using this default template gets a full-height-looking but width-2 vertical line still centered in the band, which is an acceptable simplification flagged here for a future iteration, not a blocking defect for this phase).

Update the nested `Border` to:
```xml
        <Border Background="{TemplateBinding DividerBrush}" HorizontalAlignment="Center" Width="2"/>
```

- [ ] **Step 6: Verify the theme still loads cleanly**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ThemeConfigurationTests"`
Expected: PASS - this is the existing Phase 0 suite that loads the whole `DefaultTheme.xml` document; a malformed new entry would fail it.

- [ ] **Step 7: Run the full test suite (regression check)**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add sources/IcyUI/UI/Controls/SplitPane.cs sources/IcyUI/Resources/Themes/DefaultTheme.xml sources/IcyUI.Tests/Controls/SplitPaneTests.cs
git commit -m "Add SplitPane PART_Divider templating and default theme entry"
```

---

## Task 5: Sample - `SplitPaneDemo` + MonoGame/Stride wiring

**Files:**
- Create: `sources/Shared Samples/SplitPaneDemo.cs`
- Create: `sources/MonoGame Sample/Samples/SplitPaneSample.cs`
- Modify: `sources/MonoGame Sample/SampleGame.cs`
- Modify: `sources/Stride Sample/Stride Sample.csproj`
- Modify: `sources/Stride Sample/SampleGame.cs`

**Interfaces:**
- Consumes: `Icy.Markup.MarkupLoader`, `Icy.Configuration.IcyConfiguration`, the existing `SampleBase`/`SamplesRunner` (MonoGame) and manual PageUp/PageDown demo-cycling (Stride) infrastructure - same shape `ItemsControlDemo`/`ItemsControlSample` already use.
- Produces: a runnable, visually-inspectable `SplitPaneDemo.Build(IcyConfiguration, string fontFamily) : UIElement`.

- [ ] **Step 1: Write `SplitPaneDemo.cs`**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.SplitPane"/> (Phase 3 of the Tier-2 roadmap):
    /// two nested <see cref="Icy.UI.Controls.SplitPane"/>s forming a 3-pane IDE-like layout, matching the design
    /// spec's own nesting mockup (<c>docs/superpowers/specs/2026-09-06-splitpane-design.md</c>).
    /// </summary>
    public static class SplitPaneDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ItemsControlDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">SplitPane (Phase 3)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Drag either divider to resize. The outer split is horizontal (tree vs. editor+output); the inner split is vertical (editor over output).</TextBlock>

                <SplitPane Orientation="Horizontal" Width="600" Height="360" MinFirstSize="80" MinSecondSize="160">
                  <SplitPane.First>
                    <Border Background="#FF2E2E38" BorderBrush="#FF3A3A44" BorderThickness="0,0,1,0" Padding="10">
                      <TextBlock FontSize="14" Foreground="WhiteSmoke">Tree</TextBlock>
                    </Border>
                  </SplitPane.First>
                  <SplitPane.Second>
                    <SplitPane Orientation="Vertical" MinFirstSize="60" MinSecondSize="60">
                      <SplitPane.First>
                        <Border Background="#FF2E2E38" BorderBrush="#FF3A3A44" BorderThickness="0,0,0,1" Padding="10">
                          <TextBlock FontSize="14" Foreground="WhiteSmoke">Editor</TextBlock>
                        </Border>
                      </SplitPane.First>
                      <SplitPane.Second>
                        <Border Background="#FF2E2E38" Padding="10">
                          <TextBlock FontSize="14" Foreground="WhiteSmoke">Output</TextBlock>
                        </Border>
                      </SplitPane.Second>
                    </SplitPane>
                  </SplitPane.Second>
                </SplitPane>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration the loader resolves types, converters, and properties through. Its
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> is set to <paramref name="fontFamily"/>
        /// here, which is what gives the document's text a font.
        /// </param>
        /// <param name="fontFamily">
        /// The font family every text element resolves. Import it beforehand (see <c>FontSystem.ImportFont</c>) for
        /// text to actually render.
        /// </param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the markup language.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            return loader.Load(Markup, nameof(SplitPaneDemo));
        }
    }
}
```

- [ ] **Step 2: Write `SplitPaneSample.cs` (MonoGame)**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="SplitPaneDemo"/> - two nested, interactively-resizable <c>SplitPane</c>s forming a 3-pane
    /// layout - as its own selectable sample (PgUp/PgDown to switch to it, like the other samples).
    /// </summary>
    internal class SplitPaneSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public SplitPaneSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "SplitPane Demo")
        {
            VisibleChanged += (_, _) =>
            {
                if (demoRoot != null)
                    demoRoot.IsVisible = Visible;
            };
        }

        protected override void LoadContent()
        {
            UIConfiguration.Fonts.ImportFont(UIConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            Canvas.IsInputEnabled = true;

            demoRoot = SplitPaneDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
```

- [ ] **Step 3: Register the MonoGame sample**

Modify `sources/MonoGame Sample/SampleGame.cs`: add `new SplitPaneSample(this, uiConfiguration, canvas)` to the `samplesRunner` list right after `new ItemsControlSample(this, uiConfiguration, canvas)`.

- [ ] **Step 4: Wire the Stride sample**

Modify `sources/Stride Sample/Stride Sample.csproj`: add, after the `ItemsControlDemo.cs` link:
```xml
    <Compile Include="..\Shared Samples\SplitPaneDemo.cs" Link="SplitPaneDemo.cs" />
```

Modify `sources/Stride Sample/SampleGame.cs`:
- Add a field: `private UIElement? splitPaneRoot;`
- After `itemsControlRoot = ItemsControlDemo.Build(configuration, "Airfool");`, add:
  ```csharp
  splitPaneRoot = SplitPaneDemo.Build(configuration, "Airfool");
  ```
- After `canvas.Add(itemsControlRoot);`, add:
  ```csharp
  canvas.Add(splitPaneRoot);
  ```
- Change `selectedDemo = (selectedDemo + 1) % 7;` to `% 8`.
- In `UpdateSelectedDemo`, add:
  ```csharp
  splitPaneRoot!.IsVisible = selectedDemo == 7;
  ```
- Update the class's XML doc `<summary>` to also mention `SplitPaneDemo` (matching how it currently lists every demo by name).

- [ ] **Step 5: Build everything**

Run: `dotnet build "sources/IcyUI.sln"`
Expected: builds clean, zero new warnings.

- [ ] **Step 6: Commit**

```bash
git add "sources/Shared Samples/SplitPaneDemo.cs" "sources/MonoGame Sample/Samples/SplitPaneSample.cs" "sources/MonoGame Sample/SampleGame.cs" "sources/Stride Sample/Stride Sample.csproj" "sources/Stride Sample/SampleGame.cs"
git commit -m "Add SplitPane sample (Phase 3): nested resizable 3-pane demo"
```

- [ ] **Step 7: Manual smoke test (both engines)**

Per [[feedback_smoke_test_notification]] - **notify Ivan before running this** and let him check visually himself (or run it with him watching), rather than driving the window via automated input injection. Confirm: both dividers grab and resize interactively; nested `SplitPane`s resize proportionally when the outer split moves; nothing clips/overlaps; PgUp/PgDown (MonoGame) and PageUp/PageDown/Shift+Tab (Stride) reach the new sample.

---

## Self-Review Notes

- **Spec coverage:** API shape (Task 1), layout algorithm (Task 2), divider hit-test/visual split (Task 1's two-level `Border`), drag (Task 3), templating + theme (Task 4), sample (Task 5) - every section of the spec's "Detailed design" has a task. The spec's "Open items for implementation planning" (divider cosmetic defaults, tie-breaking under an undersized container, no keyboard support) are resolved inline above: `ResolveDividerOffset` picks the range-midpoint tie-break; keyboard support is confirmed out of scope, consistent with `Slider`.
- **Placeholder scan:** no TBD/TODO; every step has runnable code.
- **Type consistency:** `divider`/`dividerVisual` (Task 1) are read by `ArrangeContent`/`ResolveDividerOffset` (Task 2), `UpdatePositionFromPoint` (Task 3), and `OnApplyTemplate` (Task 4) with the same names and `Border` type throughout. `SplitterPosition`/`MinFirstSize`/`MinSecondSize`/`DividerSize`/`DividerBrush` property names match the spec exactly and are used identically across all four tasks.
