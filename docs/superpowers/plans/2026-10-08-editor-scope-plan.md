# Editor Scope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restrict the design-time editor to one `UIElement` subtree and let the wheel and the middle button scroll the
page while editing, then use it in `EditorDemo` and the samples shell.

**Architecture:** One core seam (`UIElement.PassesUnclaimedInput`) lets an overlay hand unclaimed wheel scrolls and drags
to whatever lies beneath it. `EditorSession` owns the scope and the region (the scope's ancestor-clipped surface bounds)
and gates hit-testing and selection with it; `EditorFrame` fits its capture layer, toolbar and adorners to that region.
The samples attach scoped editors.

**Tech Stack:** C# / .NET 10, xUnit (v2), `IcyUI`, `IcyUI.Design`, `Shared Samples`.

**Spec:** `docs/superpowers/specs/2026-10-08-editor-scope-design.md`

## Global Constraints

- Every public API gets complete XML documentation (`<see cref>`, `<see langword>`, `<para>`, `<list>`).
- Match surrounding style: block-scoped `namespace X { }`, the file copyright header, StyleCop rules in `IcyUI` and
  `IcyUI.Design`.
- Core (`IcyUI`) must not reference engine types. No engine project changes in this plan.
- Don't pass NaN-able layout limits to `float.Clamp`/`Math.Min`/`Math.Max`. (Integer `Math.Clamp` on surface pixels
  is fine.)
- Build: `dotnet build "sources/IcyUI.sln"`. Test: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. `dotnet test`
  prints "Passed!" even when the test host crashes: always check the **Total** count.
- No new build warnings in `IcyUI` or `IcyUI.Design` compared with the baseline recorded in Task 1, Step 1.
- Never claim a manual smoke test. Ivan runs them.

## Spec deviation

- **Scope off the canvas:** the spec says the capture layer and the toolbar hide with `IsVisible = false`. A hidden
  overlay is never drawn, so the frame, which polls the region from the capture layer's `OnRender`, would never see the
  scope come back. Instead the capture layer stays visible but stops hit-testing and draws nothing; only the toolbar
  hides. The observable behavior is the same.
- **`EditorTestHost`:** the existing frame tests attach a second session on the host's canvas. The one-session rule
  forbids that, so the host gains an `attachSession: false` option (Task 2).

## Review Focus

1. **A scope inside a hidden ancestor** (a collapsed panel, `IsVisible = false` on a parent): a reasonable person
   expects no capture at all, not a phantom capture area where the content used to be. Pinned in Task 2 (region empty)
   and Task 3 (pointer not captured).
2. **Toggling Interact → Edit while the region is empty:** `UpdateToolbar` today sets
   `CaptureLayer.IsHitTestVisible = Edit` unconditionally, which would turn capture back on. Pinned in Task 3.
3. **A scaled canvas** (`EffectiveScale = 2`): the region is in surface units, pointer positions are in screen pixels.
   A wrong conversion selects the wrong element or nothing. Pinned in Task 2.
4. **A failed `Attach`** (scope not on the canvas, or a second session): it must leave no footprint: navigation still
   enabled, nothing registered in `FindAttached`. Pinned in Task 2.
5. **Adorners drawn after the capture layer moved:** the layer now has an offset, so drawing in surface coordinates
   without shifting would draw every outline displaced by the region's origin. Pinned in Task 3 through the recorded
   draw calls.

---

### Task 1: Core fall-through for unclaimed scrolls and drags

**Files:**
- Modify: `sources/IcyUI/UI/UIElement.cs` (new property next to `IsHitTestVisible`, around line 368-384; new field next
  to `isHitTestVisible`, line 68)
- Modify: `sources/IcyUI/UI/Canvas.cs` (`OnGestureDragStarted` line 831, `OnScroll` lines 962-972, new private helpers
  next to `ResolveDragOwner` line 771)
- Test: `sources/IcyUI.Tests/UI/CanvasUnclaimedInputTests.cs` (new)

**Interfaces:**
- Produces: `public bool UIElement.PassesUnclaimedInput { get; set; }`, default `false`. Task 3 sets it on the capture
  layer.

- [ ] **Step 1: Record the warning baseline**

Run: `dotnet build "sources/IcyUI.sln" 2>&1 | grep -c "warning"`. Write the number down; every later build compares
against it. (Ignore `NU1900` from the unreachable work feed.)

- [ ] **Step 2: Write the failing tests**

Create `sources/IcyUI.Tests/UI/CanvasUnclaimedInputTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasUnclaimedInputTests
    {
        private static (Canvas Canvas, FakeInputSystem Input, ScrollViewer Viewer) Create()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var viewer = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = new Border { Width = 100, Height = 500 },
            };
            canvas.Add(viewer);
            canvas.Render();
            return (canvas, input, viewer);
        }

        private static Border Cover(Canvas canvas, bool passes)
        {
            var overlay = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                PassesUnclaimedInput = passes,
            };
            canvas.AddOverlay(overlay);
            canvas.Render();
            return overlay;
        }

        private static void Hover(Canvas canvas, FakeInputSystem input, Point point)
        {
            input.Mouse.MouseInfo = new MouseInfo(point);
            canvas.Render();
        }

        private static DragInfo Drag(PointerKind kind, Point start, Point position) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        [Fact]
        public void TheWheel_OverAFallThroughOverlay_ScrollsTheViewerBeneath()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: true);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, viewer.VerticalOffset);
        }

        [Fact]
        public void TheWheel_OverAnOrdinaryOverlay_IsBlocked()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: false);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void TheWheel_FallsThroughTwoStackedOverlays()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: true);
            Cover(canvas, passes: true);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, viewer.VerticalOffset);
        }

        [Fact]
        public void TheWheel_ConsumedInsideTheOverlay_DoesNotFallThrough()
        {
            var (canvas, input, viewer) = Create();
            var inner = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = new Border { Width = 100, Height = 500 },
                PassesUnclaimedInput = true,
            };
            canvas.AddOverlay(inner);
            Hover(canvas, input, new Point(50, 50));

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, inner.VerticalOffset);
            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void AMiddleDrag_OverAFallThroughOverlay_PansTheViewerBeneath()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: true);

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 60)));
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 40)));
            input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 40)));

            Assert.Equal(40, viewer.VerticalOffset);
        }

        [Fact]
        public void AMiddleDrag_OverAnOrdinaryOverlay_IsBlocked()
        {
            var (canvas, input, viewer) = Create();
            Cover(canvas, passes: false);

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 60)));
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 40)));

            Assert.Equal(0, viewer.VerticalOffset);
        }

        [Fact]
        public void ADragTheOverlayClaims_StaysWithTheOverlay()
        {
            var (canvas, input, viewer) = Create();
            var overlay = new AxisElement(DragAxes.Both)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                PassesUnclaimedInput = true,
            };
            canvas.AddOverlay(overlay);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, new Point(50, 80), new Point(50, 60)));

            Assert.Equal(new[] { "start 50,80", "move 50,60" }, overlay.Log);
            Assert.Equal(0, viewer.VerticalOffset);
        }
    }
}
```

`AxisElement` (in `UI/AxisElement.cs`) claims its fixed axes for every pointer kind and logs the calls.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasUnclaimedInputTests"`
Expected: build error, `'Border' does not contain a definition for 'PassesUnclaimedInput'`.

- [ ] **Step 4: Add the property**

In `UIElement.cs`, next to `private bool isHitTestVisible = true;` (line 68):

```csharp
        private bool passesUnclaimedInput;
```

After the `IsHitTestVisible` property:

```csharp
        /// <summary>
        /// Gets or sets a value indicating whether a wheel scroll or a drag that nothing in this overlay takes falls
        /// through to the elements beneath it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only consulted on the root of a <see cref="UI.Canvas.Overlays">canvas overlay</see>. When a wheel scroll
        /// reaches no element of the overlay that consumes it (see <see cref="OnScroll"/>), or a starting drag finds no
        /// element of the overlay that claims it (see <see cref="GetDragAxes"/>), the canvas hit-tests again below the
        /// overlay and routes the input there.
        /// </para>
        /// <para>
        /// Hover, taps, clicks, drag-and-drop sources and focus never fall through. <see langword="false"/> by default,
        /// so an ordinary overlay, such as a modal dialog's backdrop, keeps blocking the page beneath it. Tools use it
        /// for a layer that takes some gestures but should let the page scroll, like the design-time editor's capture
        /// layer.
        /// </para>
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool PassesUnclaimedInput
        {
            get => passesUnclaimedInput;
            set => SetProperty(ref passesUnclaimedInput, value);
        }
```

If `<see cref="GetDragAxes"/>` raises CS1574 (overload resolution in crefs), use
`<see cref="GetDragAxes(in DragClaimContext)"/>` as the existing `DragAxes.cs` docs do.

- [ ] **Step 5: Route unclaimed input below fall-through overlays**

In `Canvas.cs`, add next to `ResolveDragOwner`:

```csharp
        /// <summary>
        /// Resolves a drag's owner from <paramref name="hit"/>, and when nothing there claims it and the chain belongs to
        /// an overlay that <see cref="UIElement.PassesUnclaimedInput">passes unclaimed input</see>, from what lies
        /// beneath that overlay.
        /// </summary>
        private UIElement? ResolveDragOwnerFallingThrough(UIElement? hit, in DragInfo drag)
        {
            HashSet<UIElement>? passed = null;
            while (true)
            {
                if (ResolveDragOwner(hit, drag) is { } owner)
                    return owner;
                if (FallThroughOverlay(hit) is not { } overlay)
                    return null;

                passed ??= [];
                passed.Add(overlay);
                HashSet<UIElement> excluded = passed;
                hit = HitTest(drag.Start, x => !excluded.Contains(x));
            }
        }

        /// <summary>
        /// Gets the overlay <paramref name="hit"/> belongs to when that overlay passes unclaimed input on; otherwise
        /// <see langword="null"/>.
        /// </summary>
        private UIElement? FallThroughOverlay(UIElement? hit)
        {
            UIElement? root = hit;
            while (root?.Parent is { } parent)
                root = parent;

            return root is { PassesUnclaimedInput: true } && overlayElements.Contains(root) ? root : null;
        }
```

In `OnGestureDragStarted`, replace `dragOwner = ResolveDragOwner(hit, drag);` with:

```csharp
            dragOwner = ResolveDragOwnerFallingThrough(hit, drag);
```

Replace the body of `OnScroll` with:

```csharp
        private void OnScroll(object? sender, GenericEventArgs<Icy.Input.Events.ScrollInfo> e)
        {
            // Stop at the first element that actually consumes the scroll (see UIElement.OnScroll's remarks) -
            // otherwise a scrollable region nested inside another scrollable region also scrolled every ancestor
            // around it, since every one of them received the same wheel/swipe event.
            UIElement? hit = hoveredElement;
            HashSet<UIElement>? passed = null;
            while (true)
            {
                foreach (UIElement element in SelfAndAncestors(hit))
                {
                    if (element.OnScroll(e.Data))
                        return;
                }

                // Nothing took it: an overlay that passes unclaimed input on lets the elements beneath it have a go.
                if (FallThroughOverlay(hit) is not { } overlay)
                    return;

                passed ??= [];
                passed.Add(overlay);
                HashSet<UIElement> excluded = passed;
                hit = HitTest(Configuration.Input.Mouse.MouseInfo.Position, x => !excluded.Contains(x));
            }
        }
```

`hoveredElement` is computed from the same mouse position in `UpdateHover`, so the re-hit is consistent with it.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasUnclaimedInputTests|FullyQualifiedName~CanvasDragOwnershipTests|FullyQualifiedName~ScrollViewer"`
Expected: all pass. If `AMiddleDrag_OverAFallThroughOverlay_PansTheViewerBeneath` reports 20 rather than 40, the
viewer applies only the moves after the start; set the expectation to the start-to-last-move distance it reports
**only if** `ScrollViewerPanningTests` shows the same convention, and note it in the commit.

- [ ] **Step 7: Run the full suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. Expected: all pass; check Total.

- [ ] **Step 8: Commit**

```bash
git add sources/IcyUI/UI/UIElement.cs sources/IcyUI/UI/Canvas.cs sources/IcyUI.Tests/UI/CanvasUnclaimedInputTests.cs
git commit -m "Let an overlay pass unclaimed scrolls and drags to the elements beneath it"
```

---

### Task 2: Scope, region and one session per canvas in `EditorSession`

**Files:**
- Modify: `sources/IcyUI.Design/Editor/EditorSession.cs` (ctor line 38, `Attach` line 161-177, `HitTest` line 179-185,
  `Select` lines 210-238, `Dispose` line 345)
- Modify: `sources/IcyUI.Tests/Design/Editor/EditorTestHost.cs`
- Modify: `sources/IcyUI.Tests/Design/Editor/EditorFrameTests.cs`, `EditorFrameGestureTests.cs`,
  `EditorFramePerformanceTests.cs` (host construction only)
- Test: `sources/IcyUI.Tests/Design/Editor/EditorSessionScopeTests.cs` (new)

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces:
  - `public static EditorSession Attach(DesignSession design, Canvas canvas, UIElement? scope)`
  - `public UIElement? Scope { get; }`
  - `public static EditorSession? FindAttached(Canvas canvas)`
  - `internal Rectangle Region()`: surface units; the whole surface without a scope; `Rectangle.Empty` when the
    scope is off the canvas, hidden, or fully clipped.
  - `internal bool IsInScope(UIElement element)`
  - `EditorTestHost(string markup, bool attachSession = true)`; `host.Session` throws when built without one.

- [ ] **Step 1: Make the test host able to skip its session**

Replace the constructor, the `Session` property and `Dispose` in `EditorTestHost.cs`:

```csharp
        private readonly EditorSession? session;

        public EditorTestHost(string markup, bool attachSession = true)
        {
            Input = new FakeInputSystem();
            Configuration = new IcyConfiguration(Input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            Configuration.Types.Markup.RegisterShortName<PickyPanel>();
            Configuration.Types.Markup.RegisterShortName<ClrPanel>();
            Design = DesignSession.Attach(Configuration);
            Root = new MarkupLoader(Configuration).Load(markup.ReplaceLineEndings("\n"), "page.xml");
            Document = Design.FindDocument(Root, out _)!;
            Canvas = new Canvas(Configuration) { IsInputEnabled = true, IsVisible = true };
            Canvas.Add(Root);
            Canvas.Render();
            if (attachSession)
                session = EditorSession.Attach(Design, Canvas);
        }

        // ...

        /// <summary>
        /// Gets the session the host attached. Hosts built with <c>attachSession: false</c> have none: the test attaches
        /// its own session or frame, since a canvas takes one session at a time.
        /// </summary>
        public EditorSession Session => session ?? throw new InvalidOperationException("This host was built without a session.");

        // ...

        public void Dispose()
        {
            session?.Dispose();
            Design.Dispose();
        }
```

Add `public FakeRenderContext RenderContext => (FakeRenderContext)Configuration.RenderContext;` for Task 3.

- [ ] **Step 2: Move the frame tests onto session-less hosts**

In `EditorFrameTests.cs`, `EditorFrameGestureTests.cs` and `EditorFramePerformanceTests.cs`, every
`new EditorTestHost(Page)` / `new EditorTestHost(markup.ToString())` that is followed by `EditorFrame.Attach` becomes
`new EditorTestHost(Page, attachSession: false)` (resp. `markup.ToString(), attachSession: false`). Then:

- `EditorFrameTests.Dispose_RemovesTheLayersAndTheBindings`: delete the comment
  `// Detach in reverse order of attaching: ...` and the line `host.Session.Dispose();`.
- `EditorFramePerformanceTests`: delete the line `host.Session.Dispose();`.
- `EditorFrameTests.TheToolbar_SaysSoWhenNothingOnTheCanvasIsTracked` attaches to a second canvas; it can keep the
  default host.

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Editor"`
Expected: all pass (no behavior changed yet).

- [ ] **Step 3: Write the failing tests**

Create `sources/IcyUI.Tests/Design/Editor/EditorSessionScopeTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design.Editor;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorSessionScopeTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="100,100,0,0">
              <Border x:Name="outside" Width="100" Height="20" HorizontalAlignment="Left"/>
              <ScrollViewer x:Name="viewer" Width="200" Height="100" HorizontalAlignment="Left">
                <StackPanel x:Name="scope">
                  <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
                  <Border x:Name="tall" Width="100" Height="400" HorizontalAlignment="Left"/>
                </StackPanel>
              </ScrollViewer>
            </StackPanel>
            """;

        private static EditorSession AttachScoped(EditorTestHost host) =>
            EditorSession.Attach(host.Design, host.Canvas, host.Named<StackPanel>("scope"));

        [Fact]
        public void TheRegion_IsTheScopeClippedByItsScrollViewer()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);
            Rectangle viewer = Rectangle.Round(AdornerGeometry.SurfaceBounds(host.Named<ScrollViewer>("viewer")));

            Rectangle region = session.Region();

            Assert.Equal(viewer.Top, region.Top);
            Assert.Equal(viewer.Bottom, region.Bottom);
            Assert.True(region.Width > 0);
            Assert.True(region.Left >= viewer.Left && region.Right <= viewer.Right);
        }

        [Fact]
        public void WithoutAScope_TheRegionIsTheWholeSurface()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = EditorSession.Attach(host.Design, host.Canvas);

            Assert.Null(session.Scope);
            Assert.Equal(new Rectangle(Point.Empty, host.Canvas.SurfaceSize), session.Region());
        }

        [Fact]
        public void HitTest_SeesOnlyTheRegion()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);
            var tall = host.Named<Border>("tall");

            Assert.Same(host.Named<Border>("a"), session.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
            Assert.Null(session.HitTest(host.At(host.Named<Border>("outside"), 5, 5)));

            // Below the viewer: inside the scope's bounds, but clipped away.
            Assert.Null(session.HitTest(host.At(tall, 5, 300)));
        }

        [Fact]
        public void HitTest_OnAScaledCanvas_StillMatchesThePointer()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            host.Configuration.Scaling.UserScale = 2f;
            host.Render();
            using EditorSession session = AttachScoped(host);

            Assert.Same(host.Named<Border>("a"), session.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
            Assert.Null(session.HitTest(host.At(host.Named<Border>("outside"), 5, 5)));
        }

        [Fact]
        public void TheRegion_IsEmpty_WhileAnAncestorIsHidden()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);

            host.Named<ScrollViewer>("viewer").IsVisible = false;
            host.Render();

            Assert.True(session.Region().IsEmpty);
            Assert.Null(session.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
        }

        [Fact]
        public void Select_RefusesElementsOutsideTheScope_AndAcceptsOverlays()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);
            UIElement popup = new MarkupLoader(host.Configuration).Load("<Border Width=\"50\" Height=\"50\"/>", "popup.xml");
            host.Canvas.AddOverlay(popup);

            Assert.False(session.Select(host.Named<Border>("outside")));
            Assert.Null(session.Selection);
            Assert.True(session.Select(host.Named<Border>("a")));
            Assert.True(session.Select(popup));
        }

        [Fact]
        public void SelectByNode_RefusesAnElementOutsideTheScope()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorSession session = AttachScoped(host);

            Assert.Throws<ArgumentException>(() => session.Select(host.Document, host.IdOf(host.Named<Border>("outside"))));
        }

        [Fact]
        public void ASecondSession_OnTheSameCanvas_Throws_UntilTheFirstIsDisposed()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            EditorSession first = EditorSession.Attach(host.Design, host.Canvas);

            Assert.Throws<InvalidOperationException>(() => EditorSession.Attach(host.Design, host.Canvas));
            Assert.Same(first, EditorSession.FindAttached(host.Canvas));
            Assert.False(host.Canvas.IsKeyboardNavigationEnabled);

            first.Dispose();
            Assert.Null(EditorSession.FindAttached(host.Canvas));
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);

            using EditorSession second = EditorSession.Attach(host.Design, host.Canvas);
            Assert.Same(second, EditorSession.FindAttached(host.Canvas));
        }

        [Fact]
        public void AScopeNotOnTheCanvas_Throws_AndLeavesNoFootprint()
        {
            using var host = new EditorTestHost(Page, attachSession: false);

            Assert.Throws<ArgumentException>(() => EditorSession.Attach(host.Design, host.Canvas, new Border()));

            Assert.Null(EditorSession.FindAttached(host.Canvas));
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorSessionScopeTests"`
Expected: build errors: no `Attach` overload with three arguments, no `Scope`, `Region`, `FindAttached`.

- [ ] **Step 5: Implement the scope in `EditorSession`**

Add `using System.Runtime.CompilerServices;` and `using System.Numerics;` to the usings.

Add a static field and change the constructor:

```csharp
        private static readonly ConditionalWeakTable<Canvas, EditorSession> AttachedSessions = new();

        private EditorSession(DesignSession design, Canvas canvas, UIElement? scope)
        {
            Design = design;
            Canvas = canvas;
            Scope = scope;
            Commands = new EditorCommands(this);
            Bindings = new EditorBindings(Commands);
        }
```

Add after the `Canvas` property:

```csharp
        /// <summary>
        /// Gets the subtree this editor is limited to, or <see langword="null"/> when it edits the whole canvas.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The editor sees only the scope's visible area: its bounds, clipped by every ancestor that
        /// <see cref="UIElement.ClipToBounds">clips</see> and by the canvas surface. <see cref="HitTest"/> finds nothing
        /// outside that area, and <see cref="Select(UIElement)"/> refuses elements that are neither in the scope nor in a
        /// canvas overlay (the page's popups and dialogs).
        /// </para>
        /// <para>The keyboard is not scoped: in <see cref="EditorMode.Edit"/> the editor's key bindings stay canvas-wide.</para>
        /// <para>The scope is fixed for the session's lifetime; dispose the session and attach a new one to change it.</para>
        /// </remarks>
        public UIElement? Scope { get; }
```

Replace `Attach` with the two overloads, and add `FindAttached`:

```csharp
        /// <summary>
        /// Attaches an editor to the whole of <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="design">The design session tracking the canvas's pages.</param>
        /// <param name="canvas">The canvas to edit.</param>
        /// <returns>The session; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="design"/> or <paramref name="canvas"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Another session is attached to <paramref name="canvas"/>.</exception>
        public static EditorSession Attach(DesignSession design, Canvas canvas) => Attach(design, canvas, null);

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, limited to <paramref name="scope"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="design">The design session tracking the canvas's pages.</param>
        /// <param name="canvas">The canvas to edit.</param>
        /// <param name="scope">The subtree to edit (see <see cref="Scope"/>), or <see langword="null"/> for the whole canvas.</param>
        /// <returns>The session; dispose it to detach.</returns>
        /// <remarks>
        /// A canvas takes one session at a time: two would both bind the editor's keys and fight over the focus and
        /// navigation state. Check <see cref="FindAttached"/> first when another tool might hold the canvas.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="design"/> or <paramref name="canvas"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="scope"/> is not on <paramref name="canvas"/>.</exception>
        /// <exception cref="InvalidOperationException">Another session is attached to <paramref name="canvas"/>.</exception>
        public static EditorSession Attach(DesignSession design, Canvas canvas, UIElement? scope)
        {
            ArgumentNullException.ThrowIfNull(design);
            ArgumentNullException.ThrowIfNull(canvas);
            if (scope != null && !ReferenceEquals(scope.Canvas, canvas))
                throw new ArgumentException("The scope must be on the canvas the editor attaches to.", nameof(scope));

            var session = new EditorSession(design, canvas, scope);
            if (!AttachedSessions.TryAdd(canvas, session))
                throw new InvalidOperationException("Another editor session is attached to this canvas; dispose it first.");

            session.EnterEdit();
            session.Bindings.Register(design.Configuration.Input.Events);
            return session;
        }

        /// <summary>
        /// Finds the editor session attached to <paramref name="canvas"/>.
        /// </summary>
        /// <param name="canvas">The canvas.</param>
        /// <returns>The attached session, or <see langword="null"/> when there's none.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="canvas"/> is <see langword="null"/>.</exception>
        public static EditorSession? FindAttached(Canvas canvas)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            return AttachedSessions.TryGetValue(canvas, out EditorSession? session) ? session : null;
        }
```

Replace `HitTest` (keep its XML docs, adding the region sentence):

```csharp
        /// <summary>
        /// Finds the element under a screen point the way the editor sees the canvas: past the editor's own layers, but
        /// including the page's popups and dialogs, and only inside the <see cref="Scope"/>'s visible area.
        /// </summary>
        /// <param name="screenPoint">A point in screen space, where pointer positions arrive.</param>
        /// <returns>The topmost hit element, or <see langword="null"/>.</returns>
        public UIElement? HitTest(Point screenPoint)
        {
            if (Scope != null)
            {
                Vector2 surface = AdornerGeometry.ScreenToSurface(screenPoint, Canvas.EffectiveScale);
                if (!Region().Contains((int)MathF.Floor(surface.X), (int)MathF.Floor(surface.Y)))
                    return null;
            }

            return Canvas.HitTest(screenPoint, overlay => !OwnLayers.Contains(overlay));
        }
```

In `Select(UIElement instance)`, after `ThrowIfDisposed();`:

```csharp
            if (!IsInScope(instance))
                return false;
```

and add to its `<returns>`: "or when it's outside the <see cref="Scope"/> and not in an overlay".

In `Select(DesignDocument document, NodeId node)`, change the instance lookup to prefer an in-scope copy:

```csharp
            UIElement instance = document.GetObjects(node).OfType<UIElement>().FirstOrDefault(x => ReferenceEquals(x.Canvas, Canvas) && IsInScope(x))
                ?? throw new ArgumentException($"Element {node} has no live copy in this editor's scope.", nameof(node));
```

and update its `ArgumentException` doc to "The element has no live copy in the editor's scope on this canvas, or can't
be edited."

Add the internal helpers next to `Syntax`:

```csharp
        /// <summary>
        /// Gets the <see cref="Scope"/>'s visible area in surface units: its bounds clipped by every clipping ancestor and
        /// by the canvas surface. Without a scope, the whole surface; empty while the scope is off the canvas or hidden.
        /// </summary>
        internal Rectangle Region()
        {
            var surface = new Rectangle(Point.Empty, Canvas.SurfaceSize);
            if (Scope is not { } scope)
                return surface;
            if (!ReferenceEquals(scope.Canvas, Canvas))
                return Rectangle.Empty;

            for (UIElement? current = scope; current != null; current = current.Parent)
            {
                if (!current.IsVisible)
                    return Rectangle.Empty;
            }

            Rectangle region = Rectangle.Intersect(surface, Rectangle.Round(AdornerGeometry.SurfaceBounds(scope)));
            for (UIElement? ancestor = scope.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (ancestor.ClipToBounds)
                    region = Rectangle.Intersect(region, Rectangle.Round(AdornerGeometry.SurfaceBounds(ancestor)));
            }

            return region.Width > 0 && region.Height > 0 ? region : Rectangle.Empty;
        }

        /// <summary>
        /// Gets whether the editor may select <paramref name="element"/>: it's in the <see cref="Scope"/>, or in one of the
        /// canvas's overlays (the page's popups and dialogs), or there's no scope.
        /// </summary>
        internal bool IsInScope(UIElement element)
        {
            if (Scope == null)
                return true;

            UIElement root = element;
            for (UIElement? current = element; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, Scope))
                    return true;
                root = current;
            }

            return Canvas.Overlays.Contains(root);
        }
```

At the end of `Dispose` (before `disposed = true;`):

```csharp
            if (AttachedSessions.TryGetValue(Canvas, out EditorSession? attached) && ReferenceEquals(attached, this))
                AttachedSessions.Remove(Canvas);
```

Update the class remarks with one more `<para>`: "A canvas takes one session at a time; see <see cref="FindAttached"/>."

- [ ] **Step 6: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design"`
Expected: all pass, including the new `EditorSessionScopeTests`. If `TheRegion_IsTheScopeClippedByItsScrollViewer`
fails on `region.Left`/`Right`, print the three rectangles before changing anything: the assertion is deliberately
loose about the scrollbar's width, not about the top and bottom clip.

- [ ] **Step 7: Run the full suite, then commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. Expected: all pass; check Total.

```bash
git add sources/IcyUI.Design/Editor/EditorSession.cs sources/IcyUI.Tests/Design/Editor
git commit -m "Limit an editor session to a scope, and allow one session per canvas"
```

---

### Task 3: Fit `EditorFrame` to the scope

**Files:**
- Modify: `sources/IcyUI.Design/Editor/EditorFrame.cs`
- Modify: `sources/IcyUI.Design/Editor/EditorCaptureLayer.cs` (class summary only)
- Test: `sources/IcyUI.Tests/Design/Editor/EditorFrameScopeTests.cs` (new)
- Modify: `sources/IcyUI.Tests/Design/Editor/EditorFramePerformanceTests.cs` (scoped measurement)

**Interfaces:**
- Consumes: `UIElement.PassesUnclaimedInput` (Task 1); `EditorSession.Attach(design, canvas, scope)`, `Scope`,
  `Region()` (Task 2); `EditorTestHost(markup, attachSession: false)` and `host.RenderContext` (Task 2).
- Produces: `public static EditorFrame Attach(Canvas canvas, DesignSession design, UIElement? scope)`.

- [ ] **Step 1: Write the failing tests**

Create `sources/IcyUI.Tests/Design/Editor/EditorFrameScopeTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorFrameScopeTests
    {
        // A sidebar button left of a scrolling content area, like the samples shell.
        private const string Page =
            """
            <StackPanel x:Name="root" Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="side" Width="150" Height="300">Side</Button>
              <ScrollViewer x:Name="viewer" Width="400" Height="300">
                <StackPanel x:Name="content">
                  <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
                  <Border x:Name="tall" Width="100" Height="1000" HorizontalAlignment="Left"/>
                </StackPanel>
              </ScrollViewer>
            </StackPanel>
            """;

        private static EditorFrame AttachScoped(EditorTestHost host)
        {
            EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design, host.Named<ScrollViewer>("viewer"));
            Settle(host);
            return frame;
        }

        // The frame follows the region from its render: one frame to see it, one to arrange the layer, one more for the
        // toolbar's measured size.
        private static void Settle(EditorTestHost host)
        {
            for (int i = 0; i < 3; i++)
                host.Render();
        }

        private static Rectangle Surface(UIElement element) => Rectangle.Round(AdornerGeometry.SurfaceBounds(element));

        private static DragInfo Drag(PointerKind kind, Point start, Point position) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        [Fact]
        public void TheCaptureLayer_CoversExactlyTheRegion()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);

            Assert.Equal(frame.Session.Region(), Surface(frame.CaptureLayer));
            Assert.Equal(Surface(host.Named<ScrollViewer>("viewer")), frame.Session.Region());
        }

        [Fact]
        public void TheCaptureLayer_FollowsAViewportResize()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);

            host.RenderContext.ViewportSize = new Size(300, 200);
            Settle(host);

            Assert.Equal(new Rectangle(150, 0, 150, 200), frame.Session.Region());
            Assert.Equal(frame.Session.Region(), Surface(frame.CaptureLayer));
        }

        [Fact]
        public void TheToolbar_SitsInTheRegionsCorner()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            Rectangle region = frame.Session.Region();

            Rectangle topLeft = Surface(frame.Toolbar);
            Assert.Equal(region.Left + 8, topLeft.Left);
            Assert.Equal(region.Top + 8, topLeft.Top);

            frame.ToolbarPlacement = EditorToolbarPlacement.BottomRight;
            Settle(host);
            Rectangle bottomRight = Surface(frame.Toolbar);
            Assert.Equal(region.Right - 8, bottomRight.Right);
            Assert.Equal(region.Bottom - 8, bottomRight.Bottom);
        }

        [Fact]
        public void TheToolbar_StaysOnTheSurface_WhenTheRegionIsSmallerThanIt()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);

            host.RenderContext.ViewportSize = new Size(170, 200);
            frame.ToolbarPlacement = EditorToolbarPlacement.TopRight;
            Settle(host);

            Rectangle toolbar = Surface(frame.Toolbar);
            Assert.True(toolbar.Left >= 0);
            Assert.True(toolbar.Right <= 170);
        }

        [Fact]
        public void InEditMode_AClickOutsideTheRegion_ReachesThePage()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var side = host.Named<Button>("side");
            bool clicked = false;
            side.Click += (_, _) => clicked = true;

            host.Input.Events.Touch.RaiseTap(new TouchInfo(host.At(side, 20, 20), 1));

            Assert.True(clicked);
            Assert.Null(frame.Session.Selection);
        }

        [Fact]
        public void InEditMode_AClickInsideTheRegion_Selects()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var a = host.Named<Border>("a");

            host.Input.Events.Touch.RaiseTap(new TouchInfo(host.At(a, 5, 5), 1));

            Assert.Same(a, frame.Session.Selection!.Instance);
        }

        [Fact]
        public void InEditMode_TheWheel_ScrollsThePage()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Named<Border>("a"), 5, 5));
            host.Render();

            host.Input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, host.Named<ScrollViewer>("viewer").VerticalOffset);
        }

        [Fact]
        public void InEditMode_AMiddleDrag_PansThePage()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            Point from = host.At(host.Named<Border>("tall"), 5, 100);
            Point to = from with { Y = from.Y - 40 };
            string before = host.Document.Text;

            host.Input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseMiddle, from, from with { Y = from.Y - 20 }));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.MouseMiddle, from, to));
            host.Input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.MouseMiddle, from, to));

            Assert.True(host.Named<ScrollViewer>("viewer").VerticalOffset > 0);
            Assert.Equal(before, host.Document.Text);
        }

        [Fact]
        public void AHiddenScope_StopsCapturing_AndComesBack()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var viewer = host.Named<ScrollViewer>("viewer");

            viewer.IsVisible = false;
            Settle(host);
            Assert.False(frame.Toolbar.IsVisible);
            Assert.NotSame(frame.CaptureLayer, host.Canvas.HitTest(host.At(host.Named<Button>("side"), 200, 20)));

            // Toggling the mode while hidden must not turn the capture back on.
            frame.Session.Mode = EditorMode.Interact;
            frame.Session.Mode = EditorMode.Edit;
            Assert.False(frame.CaptureLayer.IsHitTestVisible);

            viewer.IsVisible = true;
            Settle(host);
            Assert.True(frame.Toolbar.IsVisible);
            Assert.Same(frame.CaptureLayer, host.Canvas.HitTest(host.At(host.Named<Border>("a"), 5, 5)));
        }

        [Fact]
        public void TheSelectionOutline_IsDrawnWhereTheElementIs()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = AttachScoped(host);
            var a = host.Named<Border>("a");
            frame.SelectionColor = Color.Magenta;
            frame.Session.Select(a);
            Settle(host);

            host.RenderContext.DrawCalls.Clear();
            host.Render();

            Rectangle top = host.RenderContext.DrawCalls
                .Where(c => c.Options.Color == Color.Magenta)
                .Select(c => c.TransformAtDrawTime.Apply(c.Options.Destination))
                .First();
            Assert.Equal(Surface(a).Location, top.Location);
        }
    }
}
```

`host.At(side, 200, 20)` in `AHiddenScope_...` is a point right of the sidebar button, over where the viewer was.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorFrameScopeTests"`
Expected: build error: no `EditorFrame.Attach` overload with three arguments.

- [ ] **Step 3: Implement the scoped frame**

In `EditorFrame.cs`:

1. Fields: add

```csharp
        private const int ToolbarInset = 8;
        private (Rectangle Region, Size Toolbar, EditorToolbarPlacement Placement)? applied;
        private bool regionEmpty;
```

2. Constructor:

```csharp
        private EditorFrame(Canvas canvas, DesignSession design, UIElement? scope)
        {
            this.canvas = canvas;
            this.design = design;
            Session = EditorSession.Attach(design, canvas, scope);

            // The layer takes left and touch drags in Edit mode; the wheel and middle-button pans reach the page beneath.
            CaptureLayer = new EditorCaptureLayer(this) { PassesUnclaimedInput = true };
            Toolbar = CreateToolbar();
            ToolbarPlacement = EditorToolbarPlacement.TopLeft;
        }
```

3. `Attach` overloads (replace the existing one):

```csharp
        /// <summary>
        /// Attaches an editor to the whole of <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="canvas">The canvas whose pages to edit.</param>
        /// <param name="design">The design session tracking those pages.</param>
        /// <returns>The frame; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Another editor session is attached to <paramref name="canvas"/>.</exception>
        public static EditorFrame Attach(Canvas canvas, DesignSession design) => Attach(canvas, design, null);

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, limited to <paramref name="scope"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="canvas">The canvas whose pages to edit.</param>
        /// <param name="design">The design session tracking those pages.</param>
        /// <param name="scope">
        /// The subtree to edit (see <see cref="EditorSession.Scope"/>), or <see langword="null"/> for the whole canvas.
        /// The capture layer, the toolbar and the adorners keep to the scope's visible area, so the rest of the canvas
        /// stays usable with the pointer.
        /// </param>
        /// <returns>The frame; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="canvas"/> or <paramref name="design"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="scope"/> is not on <paramref name="canvas"/>.</exception>
        /// <exception cref="InvalidOperationException">Another editor session is attached to <paramref name="canvas"/>.</exception>
        public static EditorFrame Attach(Canvas canvas, DesignSession design, UIElement? scope)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(design);

            var frame = new EditorFrame(canvas, design, scope);
            frame.Start();
            return frame;
        }
```

4. `Render`: sync the region first, skip drawing while it's empty, and draw in the layer's own space:

```csharp
        internal void Render(IRenderContext context)
        {
            if (Session.Mode == EditorMode.Edit && !IsOnTop())
                Dispatcher.GetCurrentThreadDispatcher().Invoke(BringToTop);

            SyncRegion();
            if (regionEmpty)
                return;

            // The scene is in surface units; the layer sits at the region's origin and clips to it.
            Point origin = CaptureLayer.PointToSurface(Vector2.Zero);
            AdornerScene scene = BuildScene();
            if (scene.Hover is { } hover)
                Outline(context, Shift(hover, origin), HoverColor, 1);
            if (scene.Selection is { } selection)
                Outline(context, Shift(selection, origin), scene.Dimmed ? Color.FromArgb(110, SelectionColor) : SelectionColor, 1.5f);
            foreach (RectangleF handle in scene.Handles)
            {
                RectangleF area = Shift(handle, origin);
                context.FillRectangle(new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), Color.White);
                Outline(context, area, SelectionColor, 1);
            }

            if (scene.Ghost is { } ghost)
                Outline(context, Shift(ghost, origin), GhostColor, 1);
            if (scene.Indicator is { } indicator)
            {
                RectangleF area = Shift(indicator, origin);
                if (scene.IndicatorIsLine)
                    context.DrawLine(area.Left, area.Top, area.Right, area.Bottom, IndicatorColor, 3);
                else
                    Outline(context, area, IndicatorColor, 2);
            }
        }

        private static RectangleF Shift(RectangleF area, Point origin) =>
            new(area.X - origin.X, area.Y - origin.Y, area.Width, area.Height);
```

5. Region sync and toolbar placement:

```csharp
        /// <summary>
        /// Fits the capture layer and the toolbar to the session's region. Runs every frame from the capture layer's render
        /// and changes layout only when the region, the toolbar's size or its placement changed.
        /// </summary>
        private void SyncRegion()
        {
            Rectangle region = Session.Region();
            bool empty = region.IsEmpty;
            if (empty != regionEmpty)
            {
                regionEmpty = empty;
                UpdateToolbar();
            }

            // Without a scope the layer stretches over the canvas and the toolbar uses alignments, as before scoping.
            if (Session.Scope == null || empty)
                return;

            Size toolbar = Toolbar.ActualBounds.Size;
            if (applied == (region, toolbar, toolbarPlacement))
                return;

            applied = (region, toolbar, toolbarPlacement);
            CaptureLayer.HorizontalAlignment = HorizontalAlignment.Left;
            CaptureLayer.VerticalAlignment = VerticalAlignment.Top;
            CaptureLayer.Margin = new Thickness(region.X, region.Y, 0, 0);
            CaptureLayer.Width = region.Width;
            CaptureLayer.Height = region.Height;
            PlaceToolbar(region, toolbar);
        }

        private void PlaceToolbar(Rectangle region, Size size)
        {
            Size surface = canvas.SurfaceSize;
            bool left = toolbarPlacement is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.BottomLeft;
            bool top = toolbarPlacement is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.TopRight;
            int x = left ? region.Left + ToolbarInset : region.Right - ToolbarInset - size.Width;
            int y = top ? region.Top + ToolbarInset : region.Bottom - ToolbarInset - size.Height;
            Toolbar.HorizontalAlignment = HorizontalAlignment.Left;
            Toolbar.VerticalAlignment = VerticalAlignment.Top;
            Toolbar.Margin = new Thickness(
                Math.Clamp(x, 0, Math.Max(0, surface.Width - size.Width)),
                Math.Clamp(y, 0, Math.Max(0, surface.Height - size.Height)),
                0,
                0);
        }
```

6. `UpdateToolbar`: replace its first line with

```csharp
            CaptureLayer.IsHitTestVisible = Session.Mode == EditorMode.Edit && !regionEmpty;
            Toolbar.IsVisible = !regionEmpty;
```

7. Class remarks: change "a full-surface capture layer" to "a capture layer over the editor's scope (the whole
   surface when there is none)", and add after the existing example:

```csharp
    /// <para>Scoped to one part of the UI, for example a tool's content area next to its own sidebar:</para>
    /// <code>
    /// EditorFrame editor = EditorFrame.Attach(canvas, designSession, contentArea);
    /// </code>
    /// <para>
    /// The sidebar keeps working with the pointer, the wheel and the middle button scroll the content area, and the keyboard
    /// stays with the editor in <see cref="EditorMode.Edit"/>. A canvas takes one editor at a time
    /// (see <see cref="EditorSession.FindAttached"/>).
    /// </para>
```

8. `EditorCaptureLayer.cs` summary: "The editor's overlay over its scope: in Edit mode it takes the left-button and
   touch gestures there and hands them to its <see cref="EditorFrame"/>, lets the wheel and middle-button pans through
   to the page, and draws the adorners."

- [ ] **Step 4: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Editor"`
Expected: all pass. If `TheCaptureLayer_CoversExactlyTheRegion` is off by the toolbar inset or by one pixel, check
whether `ActualBounds` includes the margin before touching the arithmetic: the toolbar placement assumes it doesn't.

- [ ] **Step 5: Measure scoped edit mode**

In `EditorFramePerformanceTests`, add a second fact that repeats the existing measurement with
`EditorFrame.Attach(host.Canvas, host.Design, host.Root)` and logs it with the same format, prefixed
"Scoped:":

```csharp
        [Fact]
        public void ScopedEditModeOverhead_OnA500ElementPage_IsMeasured()
        {
            var markup = new StringBuilder("<StackPanel x:Name=\"root\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">");
            for (int i = 0; i < 500; i++)
                markup.Append("<Border Width=\"4\" Height=\"1\"/>");
            markup.Append("</StackPanel>");
            using var host = new EditorTestHost(markup.ToString(), attachSession: false);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Root, 2, 2));

            for (int i = 0; i < 500; i++)
                host.Render();
            double before = Measure(host);
            EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design, host.Root);
            frame.Session.Select(host.Root);
            double editing = Measure(host);
            frame.Dispose();
            double after = Measure(host);
            double baseline = (before + after) / 2;

            output.WriteLine($"Scoped: Canvas.Render on 500 elements: {baseline:0.000} ms detached ({before:0.000}/{after:0.000}), {editing:0.000} ms in Edit mode, overhead {editing - baseline:0.000} ms (budget 0.2 ms).");
        }
```

Run in Release: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" -c Release --filter "FullyQualifiedName~EditorFramePerformanceTests" --logger "console;verbosity=detailed"`.
Record both lines (unscoped and scoped) in the commit message body. The budget is informational, not asserted.

- [ ] **Step 6: Full suite, warnings, commit**

Run: `dotnet build "sources/IcyUI.sln" 2>&1 | grep -c "warning"` (compare with the Task 1 baseline) and
`dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (check Total).

```bash
git add sources/IcyUI.Design/Editor sources/IcyUI.Tests/Design/Editor
git commit -m "Fit the editor frame to its scope and let the page scroll under it"
```

---

### Task 4: Scope `EditorDemo` to its page

**Files:**
- Modify: `sources/Shared Samples/EditorDemo.cs` (lines 101-114)
- Test: `sources/IcyUI.Tests/Samples/EditorDemoTests.cs`

**Interfaces:**
- Consumes: `EditorFrame.Attach(canvas, design, scope)` (Task 3); `EditorSession.FindAttached`, `Scope` (Task 2).
- Produces: the status text `"The shell's editor is on; press F4 to stop it."`, which Task 5's F4 makes true.

- [ ] **Step 1: Write the failing tests**

In `EditorDemoTests.cs`, change the `Build` helper to also return the input, and add three facts:

```csharp
        private static (Canvas Canvas, UIElement Root, FakeInputSystem Input) BuildWithInput()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            UIElement root = EditorDemo.Build(configuration, "Airfool");
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(root);
            canvas.Render();
            return (canvas, root, input);
        }

        private static void Tap(FakeInputSystem input, UIElement element) =>
            input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(element.PointToScreen(new System.Numerics.Vector2(5, 5)), 1));

        [Fact]
        public void TheEditor_IsScopedToThePage()
        {
            (Canvas canvas, UIElement root, _) = BuildWithInput();

            FindButton(root, "Edit this page").Command!.Execute(null);

            Assert.Same(((StackPanel)root).Children[0], EditorSession.FindAttached(canvas)!.Scope);
        }

        [Fact]
        public void TheDemosButtons_StillWork_InEditMode()
        {
            (Canvas canvas, UIElement root, FakeInputSystem input) = BuildWithInput();
            FindButton(root, "Edit this page").Command!.Execute(null);
            for (int i = 0; i < 3; i++)
                canvas.Render();

            Tap(input, FindButton(root, "Stop editing"));

            Assert.Null(EditorSession.FindAttached(canvas));
            Assert.Empty(canvas.Overlays);
        }

        [Fact]
        public void TheEditButton_Refuses_WhileAnotherEditorHoldsTheCanvas()
        {
            (Canvas canvas, UIElement root, _) = BuildWithInput();
            using EditorSession other = EditorSession.Attach(DesignDemo.SessionFor(canvas.Configuration), canvas);

            FindButton(root, "Edit this page").Command!.Execute(null);

            Assert.Same(other, EditorSession.FindAttached(canvas));
            Assert.Empty(canvas.Overlays);
            Assert.Contains(root.EnumerateVisualSubtree().OfType<TextBlock>(), t => t.Text == "The shell's editor is on; press F4 to stop it.");
        }
```

Add `using Icy.Design.Editor;`. If `Canvas` doesn't expose `Configuration` publicly, keep the configuration from
`BuildWithInput` by returning it too.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorDemoTests"`
Expected: `TheEditor_IsScopedToThePage` fails (`Scope` is null), `TheDemosButtons_StillWork_InEditMode` fails (the tap
hits the capture layer), `TheEditButton_Refuses_...` throws `InvalidOperationException`.

- [ ] **Step 3: Implement**

Replace the `edit.Command` assignment in `EditorDemo.Build`:

```csharp
            edit.Command = new DemoCommand(() =>
            {
                if (frame != null)
                {
                    Detach();
                    return;
                }

                if (root.Canvas is not { } canvas)
                    return;

                // A canvas takes one editor; in the samples shell, that may be the shell's own.
                if (EditorSession.FindAttached(canvas) != null)
                {
                    status.Text = "The shell's editor is on; press F4 to stop it.";
                    return;
                }

                // Scoped to the page, so this demo's own buttons stay clickable while editing.
                frame = EditorFrame.Attach(canvas, session, page);
                editLabel.Text = "Stop editing";
            });
```

Update the `Build` XML summary to mention that the editor is scoped to the page.

- [ ] **Step 4: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorDemoTests|FullyQualifiedName~SampleShellTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add "sources/Shared Samples/EditorDemo.cs" sources/IcyUI.Tests/Samples/EditorDemoTests.cs
git commit -m "Scope the Editor demo to its page so its buttons work while editing"
```

---

### Task 5: The shell's "Edit demo" toggle

**Files:**
- Modify: `sources/Shared Samples/SampleShell.cs` (`Build`, lines 50-143)
- Test: `sources/IcyUI.Tests/Samples/SampleShellTests.cs`

**Interfaces:**
- Consumes: `EditorFrame.Attach(canvas, design, scope)` (Task 3); `EditorSession.FindAttached`, `Scope`, `Selection`,
  `Clear()` (Task 2); `DesignDemo.SessionFor(configuration)`.
- Produces: the sidebar becomes a `Grid` (TreeView in row 0, footer in row 1); the footer button text is
  `"Edit demo (F4)"` / `"Stop editing (F4)"`; F4 toggles.

- [ ] **Step 1: Update the test host accessors**

In `SampleShellTests.ShellHost`, replace `Tree` and add two accessors:

```csharp
            public TreeView Tree => ((SplitPane)Root).First!.EnumerateVisualSubtree().OfType<TreeView>().First();

            public Button EditButton => ((SplitPane)Root).First!.EnumerateVisualSubtree().OfType<Button>()
                .First(b => b.Content is TextBlock { Text: var text }
                    && (text.StartsWith("Edit demo", StringComparison.Ordinal) || text.StartsWith("Stop editing", StringComparison.Ordinal)));

            public void Tap(UIElement element, int x = 5, int y = 5)
            {
                Input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(element.PointToScreen(new System.Numerics.Vector2(x, y)), 1));
                Canvas.Render();
            }
```

`EditButton` is only used after Step 4 adds the footer.

- [ ] **Step 2: Write the failing tests**

Add to `SampleShellTests` (with `using Icy.Design.Editor;`):

```csharp
        internal SampleEntry Tracked(string category, string name) =>
            new(category, name, (configuration, _) =>
            {
                UIElement element = new MarkupLoader(configuration).Load("<Border Width=\"200\" Height=\"100\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\"/>", name + ".xml");
                built[name] = element;
                return element;
            });

        private static void Settle(ShellHost shell)
        {
            for (int i = 0; i < 3; i++)
                shell.Canvas.Render();
        }

        [Fact]
        public void F4_TogglesAnEditorScopedToTheContentArea()
        {
            var shell = Host([Fake("A", "One")]);

            Press(shell, Keys.F4);
            Assert.Same(shell.Content, EditorSession.FindAttached(shell.Canvas)?.Scope);

            Press(shell, Keys.F4);
            Assert.Null(EditorSession.FindAttached(shell.Canvas));
            Assert.Empty(shell.Canvas.Overlays);
        }

        [Fact]
        public void TheFooterButton_TogglesTheEditorToo()
        {
            var shell = Host([Fake("A", "One")]);

            shell.EditButton.Command!.Execute(null);
            Assert.NotNull(EditorSession.FindAttached(shell.Canvas));

            shell.EditButton.Command!.Execute(null);
            Assert.Null(EditorSession.FindAttached(shell.Canvas));
        }

        [Fact]
        public void WhileEditing_TheSidebarSwitchesDemos_AndTheContentSelects()
        {
            var shell = Host([Tracked("A", "One"), Tracked("A", "Two")]);
            Press(shell, Keys.F4);
            Settle(shell);
            EditorSession session = EditorSession.FindAttached(shell.Canvas)!;

            shell.Tap(built["One"]);
            Assert.Same(built["One"], session.Selection?.Instance);

            // Rows: 0 = category "A", 1 = "One", 2 = "Two".
            shell.Tap(shell.Tree.List.Realized[2]);
            Settle(shell);

            Assert.Equal("Two", shell.Tree.SelectedItem!.ToString());
            Assert.Null(session.Selection);
            Assert.Same(session, EditorSession.FindAttached(shell.Canvas));
        }

        [Fact]
        public void F4_Refuses_WhileTheEditorDemoIsEditing()
        {
            SampleEntry editor = SampleCatalog.All.Single(e => e.Name == "Editor");
            var shell = Host([editor]);
            shell.Root.EnumerateVisualSubtree().OfType<Button>().First(b => b.Content is TextBlock { Text: "Edit this page" }).Command!.Execute(null);
            EditorSession demoSession = EditorSession.FindAttached(shell.Canvas)!;

            Press(shell, Keys.F4);

            Assert.Same(demoSession, EditorSession.FindAttached(shell.Canvas));
            Assert.Contains(shell.Root.EnumerateVisualSubtree().OfType<TextBlock>(), t => t.Text == "The Editor demo's editor is on.");
        }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleShellTests"`
Expected: the four new tests fail (`Press` asserts F4 is handled; no editor; no footer button). The existing shell tests
still pass with the new `Tree` accessor.

- [ ] **Step 4: Implement**

In `SampleShell.Build`, add `using Icy.Design.Editor;` at the top of the file, then:

1. Declare the editor before the `tree.SelectionChanged` handler, and clear its selection on a switch:

```csharp
            EditorFrame? editor = null;
            var cache = new Dictionary<SampleEntry, UIElement>();
            tree.SelectionChanged += (_, _) =>
            {
                if (tree.SelectedItem is TreeViewNode { Tag: SampleEntry entry })
                {
                    content.Content = GetOrBuild(entry);

                    // The editor stays on across demos; a selection on the page that left would act on nothing visible.
                    editor?.Session.Clear();
                }
            };
```

2. Build the sidebar with a footer, and use it as the split's first pane:

```csharp
            var editLabel = new TextBlock { Text = "Edit demo (F4)" };
            var editButton = new Button
            {
                Content = editLabel,
                Padding = new Thickness(12, 6),
                Margin = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var editStatus = new TextBlock { Margin = new Thickness(6, 0, 6, 6) };
            var footer = new StackPanel { Orientation = Orientation.Vertical };
            footer.Children.Add(editButton);
            footer.Children.Add(editStatus);
            Grid.SetRow(footer, 1);

            var sidebar = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebar.Children.Add(tree);
            sidebar.Children.Add(footer);

            var root = new SplitPane
            {
                First = sidebar,
                // ... the rest unchanged
            };
```

3. After `root` is built, add the toggle and bind it:

```csharp
            // The shell's own editor works on whichever demo is shown; the sidebar stays usable with the pointer.
            void ToggleEditor()
            {
                if (editor != null)
                {
                    editor.Dispose();
                    editor = null;
                    editLabel.Text = "Edit demo (F4)";
                    editStatus.Text = string.Empty;
                    return;
                }

                if (root.Canvas is not { } canvas)
                    return;
                if (EditorSession.FindAttached(canvas) != null)
                {
                    editStatus.Text = "The Editor demo's editor is on.";
                    return;
                }

                editor = EditorFrame.Attach(canvas, DesignDemo.SessionFor(configuration), content);
                editLabel.Text = "Stop editing (F4)";
                editStatus.Text = string.Empty;
            }

            editButton.Command = new ShellCommand(ToggleEditor);
            configuration.Input.Events.RegisterCommand(new ShellCommand(ToggleEditor), new KeyGesture(Keys.F4));
```

4. Update the class or `Build` XML docs: the sidebar's footer toggles an editor scoped to the content area (F4).

- [ ] **Step 5: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleShellTests|FullyQualifiedName~EditorDemoTests|FullyQualifiedName~SampleCatalogTests"`
Expected: all pass. If `WhileEditing_TheSidebarSwitchesDemos_AndTheContentSelects` fails on the row tap, check that
`Realized[2]` is the "Two" row (`shell.Tree.Rows[2].Item`) before changing the shell.

- [ ] **Step 6: Commit**

```bash
git add "sources/Shared Samples/SampleShell.cs" sources/IcyUI.Tests/Samples/SampleShellTests.cs
git commit -m "Add an Edit demo toggle to the samples shell, scoped to the content area"
```

---

### Task 6: Whole-branch check

**Files:** none new.

- [ ] **Step 1: Build both hosts and the solution**

Run: `dotnet build "sources/IcyUI.sln"`. Expected: 0 errors; the warning count equals the Task 1 baseline (or lists
only warnings in tests/samples that existed before). Both sample hosts compile: they use `SampleShell` and need no
changes.

- [ ] **Step 2: Full test run**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. Expected: all pass; Total is the previous count plus the
new tests (7 core + 9 session + 10 frame + 1 perf + 3 demo + 4 shell = 34).

- [ ] **Step 3: Review against the spec**

Walk the spec's Decisions table and Testing list and tick each against a commit. Confirm the spec deviation section of
this plan is still accurate. Then request the whole-branch review.

- [ ] **Step 4: Hand over for the smoke test**

Report to Ivan, without claiming any manual run:
- Shell: F4 / footer button, click the sidebar while editing, wheel and middle-drag over a long demo while editing,
  toolbar in the content area's top-left corner, switching demos clears the selection.
- Editor demo: "Edit this page" then "Stop editing" and "Save" while in Edit mode; pressing "Edit this page" while
  the shell's editor is on shows the F4 hint.
- Both engines (MonoGame, Stride); the routing change is engine-neutral, so differences point at a host.
