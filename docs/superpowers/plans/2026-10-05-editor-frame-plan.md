# Phase 10.3b: The Visual Editor Frame Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `EditorFrame`, the visible half of the design-time editor: two canvas overlays (a full-surface capture layer that draws every adorner and turns pointer gestures into `EditorSession` gestures, and a small toolbar), plus `EditorDemo` in both sample hosts.

**Architecture:** The frame is a thin shell over 10.3a's headless `EditorSession`. The capture layer claims drags and taps in Edit mode and forwards them to `BeginMove`/`BeginResize`/`Select`. Each frame it builds an `AdornerScene` (hover, selection and handles, drop indicator, ghost) from pure geometry helpers, then draws the scene. The toolbar shows the mode, the selection and the blocked reason.

**Tech Stack:** C# / .NET 10, xUnit v2, `IcyUI` core, `IcyUI.Design`.

**Spec:** `docs/superpowers/specs/2026-10-05-editor-frame-design.md` (section 4, the frame rows of Testing, Performance, and the manual check). Requires `docs/superpowers/plans/2026-10-05-editor-session-plan.md` (10.3a) to be complete.

## Global Constraints

- Core changes stay engine-neutral: nothing in `IcyUI.MonoGame`, `IcyUI.Stride` or `IcyUI.FNA` changes. The sample hosts change only to register `EditorDemo`.
- **Every public API gets complete XML documentation** (`<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`, `<see cref>`/`<see langword>`).
- Match the surrounding style: copyright header, block-scoped namespaces, StyleCop member order.
- **Warnings:** the baseline is **81** (`dotnet build "sources/IcyUI.sln" --no-incremental`). It must not grow.
- **Tests:** the baseline is **1382** (after 10.3a), all passing. Always read the Total.
- Every new demo is registered in **both** hosts' `SampleGame.cs` (Stride) / sample list (MonoGame) and linked into both host projects; `MonoGame Sample` has `Nullable` disabled, so shared demo files start with `#nullable enable`.
- Commit after every task on `platform-independent`, ending the message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Ivan runs the smoke test.** Never claim one.

## Review Focus

1. **A page popup opened after the frame attached** (a ComboBox dropdown) would sit above the capture layer and take clicks. Expect the frame back on top within one frame, and the popup's content still selectable. Test: Task 3, `TheFrame_ReturnsOnTopOfALaterPopup`.
2. **A drag cancelled by the gesture recognizer** (a second finger, focus loss). Expect a resize rolled back and a move never written, not a committed half-gesture. Test: Task 1, `ACancelledDrag_CallsOnDragCanceled`; Task 4, `ACancelledDrag_RollsTheResizeBack`.
3. **Clicking the toolbar in Edit mode.** Expect the toggle button to work and the click not to select or deselect anything behind the toolbar. Test: Task 4, `AToolbarClick_DoesntChangeTheSelection`.
4. **A canvas with pan and zoom, and a non-1 `EffectiveScale`.** Expect adorners exactly over their elements, and handles found under the pointer. Test: Task 2, `SurfaceBounds_FollowTheCanvasTransform`, `HandleAt_UsesSurfaceUnits`.
5. **Switching demos (or hiding the page) with the editor attached in Edit mode.** Expect the demo to detach its frame, so other demos get their input back. Test: Task 5, `HidingTheDemo_DetachesTheEditor`.

## Deviations from the spec (decided while planning)

1. **One more small core change:** `UIElement.OnDragCanceled(Point)`. Today the canvas reports a cancelled gesture as `OnDragEnded`, so the frame couldn't tell a cancel from a drop. The new virtual defaults to calling `OnDragEnded`, so every existing control behaves as before.
2. **Taps reach the frame through `ITouchEvents.Tap`**, not `UIElement.OnTap()` (which carries no position). The frame acts only when the canvas's own hit test lands on its capture layer, so toolbar clicks never select.
3. **"Stay on top" runs through the dispatcher.** The capture layer notices during its render that it isn't the last overlay and queues the re-add to the end of the frame, since the overlay list can't change while the canvas iterates it.
4. **`EditorDemo` attaches the frame on demand** ("Edit this page" / "Stop editing" button) and detaches it when the demo is hidden, because the hosts switch demos on the same canvas. It shares `DesignDemo`'s `DesignSession` (one per configuration) through a new internal `DesignDemo.SessionFor`.

---

### Task 1: `UIElement.OnDragCanceled`

**Files:**
- Modify: `sources/IcyUI/UI/UIElement.cs`, `sources/IcyUI/UI/Canvas.cs` (`OnGestureDragCanceled`), `sources/IcyUI.Tests/UI/AxisElement.cs`
- Test: `sources/IcyUI.Tests/UI/CanvasDragCancelTests.cs`

**Interfaces:**
- Produces: `protected internal virtual void UIElement.OnDragCanceled(Point screenPoint)`, called instead of `OnDragEnded` when the gesture recognizer cancels a drag; the base implementation calls `OnDragEnded(screenPoint)`. Task 4 overrides it.

- [ ] **Step 1: Write the failing tests**

Add a cancel-aware test element to `sources/IcyUI.Tests/UI/AxisElement.cs`, inside the namespace:

```csharp
    /// <summary>
    /// An <see cref="AxisElement"/> that tells a cancel from an end.
    /// </summary>
    internal sealed class CancelAwareElement(DragAxes axes) : UIElement
    {
        public List<string> Log { get; } = [];

        protected override DragAxes GetDragAxes(in DragClaimContext context) => axes;

        protected internal override void OnDragStarted(Point screenPoint) => Log.Add("start");

        protected internal override void OnDragEnded(Point screenPoint) => Log.Add("end");

        protected internal override void OnDragCanceled(Point screenPoint) => Log.Add("cancel");
    }
```

`sources/IcyUI.Tests/UI/CanvasDragCancelTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasDragCancelTests
    {
        [Fact]
        public void ACancelledDrag_CallsOnDragCanceled()
        {
            (Canvas canvas, FakeInputSystem input) = Create();
            var element = Place(new CancelAwareElement(DragAxes.Both));
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(new Point(10, 10), new Point(30, 10)));

            Assert.Equal(["start", "cancel"], element.Log);
        }

        [Fact]
        public void TheDefaultCancel_StillEndsTheDrag()
        {
            (Canvas canvas, FakeInputSystem input) = Create();
            var element = Place(new AxisElement(DragAxes.Both));
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(new Point(10, 10), new Point(30, 10)));

            Assert.Equal("end", element.Log[^1]);
        }

        private static T Place<T>(T element)
            where T : UIElement
        {
            element.Width = 100;
            element.Height = 100;
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;
            return element;
        }

        private static DragInfo Drag(Point start, Point position) =>
            new(PointerKind.MouseLeft, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        private static (Canvas Canvas, FakeInputSystem Input) Create()
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

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasDragCancelTests"`
Expected: the build FAILS (`OnDragCanceled` has no virtual to override).

- [ ] **Step 3: Implement**

In `UIElement.cs`, next to `OnDragEnded`:

```csharp
        /// <summary>
        /// Called on the element that owns a drag when the gesture recognizer cancels it (a second pointer, lost focus)
        /// instead of completing it.
        /// </summary>
        /// <param name="screenPoint">The last pointer position, in screen space.</param>
        /// <remarks>
        /// The base implementation calls <see cref="OnDragEnded(Point)"/>, so an element that doesn't care about the
        /// difference releases its drag state the same way. Override it to undo whatever the drag did, rather than commit it.
        /// </remarks>
        protected internal virtual void OnDragCanceled(Point screenPoint) => OnDragEnded(screenPoint);
```

In `Canvas.OnGestureDragCanceled`, replace `LiveDragOwner?.OnDragEnded(e.Data.Position);` with `LiveDragOwner?.OnDragCanceled(e.Data.Position);` and update its comment: "Let the owner undo its drag (by default, a normal end that drops any capture) - but never complete a drag-drop: ...".

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.UI"`
Expected: PASS, `CanvasDragCancelTests` Total 2, and every existing drag test (`CanvasDragOwnershipTests`, the ScrollViewer and Slider tests) still passes.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1384, no failures), then the warning count (81).

```bash
git add sources/IcyUI/UI sources/IcyUI.Tests/UI
git commit -m "Tell drag owners when a drag is cancelled

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Adorner geometry

**Files:**
- Create: `sources/IcyUI.Design/Editor/AdornerGeometry.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/AdornerGeometryTests.cs`

**Interfaces:**
- Consumes: `UIElement.PointToSurface` (fixed for overlays in 10.3a Task 2), `ResizeHandle` (10.3a Task 5).
- Produces (internal, `Icy.Design.Editor`): `static class AdornerGeometry` with `RectangleF SurfaceBounds(UIElement element)`, `RectangleF ToSurface(UIElement container, RectangleF local)`, `RectangleF ScreenToSurface(Rectangle screen, float scale)`, `Vector2 ScreenToSurface(Point screen, float scale)`, `IReadOnlyList<(ResizeHandle Handle, RectangleF Area)> Handles(RectangleF bounds, float size)`, `ResizeHandle HandleAt(RectangleF bounds, float size, Vector2 surfacePoint)`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Editor/AdornerGeometryTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor;
using Icy.Design.Editor.Placement;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class AdornerGeometryTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="10,20,0,0">
              <Border x:Name="box" Width="100" Height="40"/>
            </StackPanel>
            """;

        [Fact]
        public void SurfaceBounds_MatchTheLayoutWithoutTransforms()
        {
            using var host = new EditorTestHost(Page);

            Assert.Equal(new RectangleF(10, 20, 100, 40), AdornerGeometry.SurfaceBounds(host.Named<Border>("box")));
        }

        [Fact]
        public void SurfaceBounds_FollowTheCanvasTransform()
        {
            using var host = new EditorTestHost(Page);
            host.Canvas.Offset = new Vector2(30, 5);
            host.Canvas.Scale = new Vector2(2, 2);
            host.Render();

            RectangleF bounds = AdornerGeometry.SurfaceBounds(host.Named<Border>("box"));

            Assert.Equal(200, bounds.Width);
            Assert.Equal(80, bounds.Height);
        }

        [Fact]
        public void ToSurface_MapsAContainersLocalRectangle()
        {
            using var host = new EditorTestHost(Page);

            RectangleF mapped = AdornerGeometry.ToSurface(host.Named<StackPanel>("root"), new RectangleF(0, 40, 100, 0));

            Assert.Equal(new RectangleF(10, 60, 100, 0), mapped);
        }

        [Fact]
        public void ScreenToSurface_DividesByTheScale()
        {
            Assert.Equal(new RectangleF(10, 20, 50, 25), AdornerGeometry.ScreenToSurface(new Rectangle(20, 40, 100, 50), 2f));
        }

        [Fact]
        public void Handles_AreEightSquaresOnTheOutline()
        {
            IReadOnlyList<(ResizeHandle Handle, RectangleF Area)> handles = AdornerGeometry.Handles(new RectangleF(0, 0, 100, 40), 7);

            Assert.Equal(8, handles.Count);
            Assert.Contains(handles, x => x.Handle == ResizeHandle.BottomRight && x.Area == new RectangleF(96.5f, 36.5f, 7, 7));
            Assert.Contains(handles, x => x.Handle == ResizeHandle.Top && x.Area == new RectangleF(46.5f, -3.5f, 7, 7));
        }

        [Theory]
        [InlineData(100, 40, ResizeHandle.BottomRight)]
        [InlineData(0, 20, ResizeHandle.Left)]
        [InlineData(50, 20, ResizeHandle.None)]
        public void HandleAt_UsesSurfaceUnits(float x, float y, ResizeHandle expected)
        {
            Assert.Equal(expected, AdornerGeometry.HandleAt(new RectangleF(0, 0, 100, 40), 7, new Vector2(x, y)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~AdornerGeometryTests"`
Expected: the build FAILS (`AdornerGeometry` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editor/AdornerGeometry.cs`:

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
    /// Where adorners go, in surface units (the space canvas overlays are laid out and drawn in).
    /// </summary>
    internal static class AdornerGeometry
    {
        private static readonly (ResizeHandle Handle, float X, float Y)[] HandleAnchors =
        [
            (ResizeHandle.TopLeft, 0, 0),
            (ResizeHandle.Top, 0.5f, 0),
            (ResizeHandle.TopRight, 1, 0),
            (ResizeHandle.Right, 1, 0.5f),
            (ResizeHandle.BottomRight, 1, 1),
            (ResizeHandle.Bottom, 0.5f, 1),
            (ResizeHandle.BottomLeft, 0, 1),
            (ResizeHandle.Left, 0, 0.5f),
        ];

        /// <summary>
        /// Gets an element's outline in surface units: the axis-aligned bounds of its four corners after every transform.
        /// </summary>
        public static RectangleF SurfaceBounds(UIElement element)
        {
            Rectangle bounds = element.ActualBounds;
            return Bounds(
                element.PointToSurface(Vector2.Zero),
                element.PointToSurface(new Vector2(bounds.Width, 0)),
                element.PointToSurface(new Vector2(0, bounds.Height)),
                element.PointToSurface(new Vector2(bounds.Width, bounds.Height)));
        }

        /// <summary>
        /// Maps a rectangle in a container's local space (a placement indicator) into surface units.
        /// </summary>
        public static RectangleF ToSurface(UIElement container, RectangleF local) => Bounds(
            container.PointToSurface(new Vector2(local.Left, local.Top)),
            container.PointToSurface(new Vector2(local.Right, local.Top)),
            container.PointToSurface(new Vector2(local.Left, local.Bottom)),
            container.PointToSurface(new Vector2(local.Right, local.Bottom)));

        /// <summary>
        /// Converts a rectangle in screen pixels into surface units.
        /// </summary>
        public static RectangleF ScreenToSurface(Rectangle screen, float scale) =>
            new(screen.X / scale, screen.Y / scale, screen.Width / scale, screen.Height / scale);

        /// <summary>
        /// Converts a point in screen pixels into surface units.
        /// </summary>
        public static Vector2 ScreenToSurface(Point screen, float scale) => new(screen.X / scale, screen.Y / scale);

        /// <summary>
        /// Gets the eight resize handles of an outline: squares of <paramref name="size"/> centered on its corners and
        /// edge midpoints.
        /// </summary>
        public static IReadOnlyList<(ResizeHandle Handle, RectangleF Area)> Handles(RectangleF bounds, float size)
        {
            var handles = new (ResizeHandle, RectangleF)[HandleAnchors.Length];
            for (int i = 0; i < HandleAnchors.Length; i++)
            {
                (ResizeHandle handle, float x, float y) = HandleAnchors[i];
                float centerX = bounds.X + (bounds.Width * x);
                float centerY = bounds.Y + (bounds.Height * y);
                handles[i] = (handle, new RectangleF(centerX - (size / 2), centerY - (size / 2), size, size));
            }

            return handles;
        }

        /// <summary>
        /// Gets the handle under a surface point, or <see cref="ResizeHandle.None"/>.
        /// </summary>
        public static ResizeHandle HandleAt(RectangleF bounds, float size, Vector2 surfacePoint)
        {
            foreach ((ResizeHandle handle, RectangleF area) in Handles(bounds, size))
            {
                if (surfacePoint.X >= area.Left && surfacePoint.X <= area.Right && surfacePoint.Y >= area.Top && surfacePoint.Y <= area.Bottom)
                    return handle;
            }

            return ResizeHandle.None;
        }

        private static RectangleF Bounds(Point a, Point b, Point c, Point d)
        {
            int left = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
            int top = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
            int right = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
            int bottom = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
            return RectangleF.FromLTRB(left, top, right, bottom);
        }
    }
}
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~AdornerGeometryTests"`
Expected: PASS, Total 8 (5 facts + 3 theory rows).

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1392, no failures), then the warning count (81).

```bash
git add sources/IcyUI.Design/Editor/AdornerGeometry.cs sources/IcyUI.Tests/Design/Editor/AdornerGeometryTests.cs
git commit -m "Add the editor's adorner geometry

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `EditorFrame`: layers, toolbar, modes, staying on top

**Files:**
- Create: `sources/IcyUI.Design/Editor/EditorFrame.cs`, `sources/IcyUI.Design/Editor/EditorToolbarPlacement.cs`, `sources/IcyUI.Design/Editor/EditorCaptureLayer.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/EditorFrameTests.cs`

**Interfaces:**
- Consumes: `EditorSession` (10.3a), `EditorSession.OwnLayers`, `Canvas.AddOverlay`/`RemoveOverlay`/`Overlays`, `Dispatcher.GetCurrentThreadDispatcher().Invoke`.
- Produces:
  - `public enum EditorToolbarPlacement { TopLeft, TopRight, BottomLeft, BottomRight }`;
  - `public sealed class EditorFrame : IDisposable` with `static EditorFrame Attach(Canvas canvas, DesignSession design)`, `EditorSession Session`, `EditorToolbarPlacement ToolbarPlacement`, `Color SelectionColor`, `Color HoverColor`, `Color IndicatorColor`, `Color GhostColor`, `float HandleSize`, `void Dispose()`;
  - internal: `EditorCaptureLayer CaptureLayer`, `UIElement Toolbar`, `string ToolbarLabel`, `string? ToolbarStatus`, `void Render(IRenderContext)` (Task 4 fills in drawing), `void BeginDrag/UpdateDrag/EndDrag/CancelDrag(Point)` (Task 4), `AdornerScene BuildScene()` (Task 4).
  - `internal sealed class EditorCaptureLayer(EditorFrame frame) : UIElement`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Editor/EditorFrameTests.cs`:

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
    public class EditorFrameTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="100,100,0,0">
              <Border x:Name="box" Width="100" Height="40"/>
              <Button x:Name="ok" Width="100" Height="30">OK</Button>
            </StackPanel>
            """;

        [Fact]
        public void Attach_AddsTheTwoLayersOnTop()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);

            Assert.Same(frame.CaptureLayer, host.Canvas.Overlays[^2]);
            Assert.Same(frame.Toolbar, host.Canvas.Overlays[^1]);
            Assert.Contains(frame.CaptureLayer, frame.Session.OwnLayers);
            Assert.Contains(frame.Toolbar, frame.Session.OwnLayers);
        }

        [Fact]
        public void EditMode_CapturesThePointerEverywhere()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Named<Button>("ok"), 5, 5));
            host.Render();

            Assert.Same(frame.CaptureLayer, host.Canvas.HitTest(host.At(host.Named<Button>("ok"), 5, 5)));
            Assert.True(host.Canvas.IsMouseOverGUI);
        }

        [Fact]
        public void InteractMode_LetsTheClickReachThePage()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var ok = host.Named<Button>("ok");
            bool clicked = false;
            ok.Click += (_, _) => clicked = true;

            frame.Session.Mode = EditorMode.Interact;
            host.Input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(host.At(ok, 50, 15), 1));

            Assert.True(clicked);
        }

        [Fact]
        public void TheToolbar_ShowsTheModeTheSelectionAndTheBlockedReason()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);

            frame.Session.Select(host.Named<Border>("box"));
            Assert.Equal("Edit · Border \"box\"", frame.ToolbarLabel);

            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);
            Assert.Equal("The markup has errors; fix them first.", frame.ToolbarStatus);

            frame.Session.Mode = EditorMode.Interact;
            Assert.StartsWith("Interact", frame.ToolbarLabel, StringComparison.Ordinal);
        }

        [Fact]
        public void TheToolbar_SaysSoWhenNothingOnTheCanvasIsTracked()
        {
            using var host = new EditorTestHost(Page);
            var empty = new Canvas(host.Configuration) { IsInputEnabled = true, IsVisible = true };
            empty.Add(new Border { Width = 50, Height = 50 });

            using EditorFrame frame = EditorFrame.Attach(empty, host.Design);

            Assert.Equal("Edit · No tracked pages on this canvas", frame.ToolbarLabel);
        }

        [Fact]
        public void TheFrame_ReturnsOnTopOfALaterPopup()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var popup = new Border { Width = 50, Height = 50 };
            host.Canvas.AddOverlay(popup);

            host.Render();

            Assert.Same(frame.Toolbar, host.Canvas.Overlays[^1]);
            Assert.Same(frame.CaptureLayer, host.Canvas.Overlays[^2]);
            Assert.Same(popup, host.Canvas.Overlays[^3]);
        }

        [Fact]
        public void Dispose_RemovesTheLayersAndTheBindings()
        {
            using var host = new EditorTestHost(Page);
            EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            host.Session.Dispose();

            frame.Dispose();

            Assert.DoesNotContain(frame.CaptureLayer, host.Canvas.Overlays);
            Assert.DoesNotContain(frame.Toolbar, host.Canvas.Overlays);
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
            Assert.False(host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete)));
        }

        [Fact]
        public void ToolbarPlacement_MovesTheToolbar()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);

            frame.ToolbarPlacement = EditorToolbarPlacement.BottomRight;

            Assert.Equal(HorizontalAlignment.Right, frame.Toolbar.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Bottom, frame.Toolbar.VerticalAlignment);
        }
    }
}
```

`EditorTestHost` attaches its own `EditorSession`. The frame tests attach a second one through `EditorFrame.Attach`; `Dispose_RemovesTheLayersAndTheBindings` disposes the host's session first, so the Delete gesture has no handler left after the frame is gone. The other tests leave the host session attached; it's harmless, since only the frame's session is exercised.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorFrameTests"`
Expected: the build FAILS (`EditorFrame` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editor/EditorToolbarPlacement.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor
{
    /// <summary>
    /// The canvas corner an <see cref="EditorFrame"/>'s toolbar sits in.
    /// </summary>
    public enum EditorToolbarPlacement
    {
        /// <summary>The top-left corner.</summary>
        TopLeft,

        /// <summary>The top-right corner.</summary>
        TopRight,

        /// <summary>The bottom-left corner.</summary>
        BottomLeft,

        /// <summary>The bottom-right corner.</summary>
        BottomRight,
    }
}
```

`sources/IcyUI.Design/Editor/EditorCaptureLayer.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The editor's full-surface overlay: in Edit mode it takes every pointer gesture over the canvas and hands it to its
    /// <see cref="EditorFrame"/>, and it draws the adorners.
    /// </summary>
    internal sealed class EditorCaptureLayer(EditorFrame frame) : UIElement
    {
        // The drag hooks are protected internal in core; from another assembly they're overridden as protected.
        protected override DragAxes GetDragAxes(in DragClaimContext context) =>
            frame.Session.Mode == EditorMode.Edit ? DragAxes.Both : DragAxes.None;

        protected override void OnDragStarted(Point screenPoint) => frame.BeginDrag(screenPoint);

        protected override void OnDragPerforming(Point screenPoint) => frame.UpdateDrag(screenPoint);

        protected override void OnDragEnded(Point screenPoint) => frame.EndDrag(screenPoint);

        protected override void OnDragCanceled(Point screenPoint) => frame.CancelDrag();

        protected override void OnRender(IRenderContext context) => frame.Render(context);
    }
}
```

`sources/IcyUI.Design/Editor/EditorFrame.cs` (drag and render bodies are completed in Task 4; here they are the minimal versions shown):

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;
using Icy.Data.Markup;
using Icy.Design.Syntax;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The design-time editor overlay for a live page: click to select, drag to move, drag the handles to resize, and use
    /// the editor's key bindings, with every gesture recorded as an undoable markup edit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The frame adds two overlays to the canvas: a full-surface capture layer that takes the pointer in
    /// <see cref="EditorMode.Edit"/> and draws the adorners, and a small toolbar with the mode toggle, the selection, and
    /// why editing is blocked when it is. In <see cref="EditorMode.Interact"/> the capture layer lets the pointer through,
    /// so the page and the game work normally while the selection stays visible.
    /// </para>
    /// <para>
    /// The logic lives in <see cref="Session"/>; the frame only shows it and feeds it pointer gestures. Detaching keeps
    /// every edit; save the documents through <see cref="DesignDocument.Save"/>.
    /// </para>
    /// <para>Typical in-game use:</para>
    /// <code>
    /// EditorFrame editor = EditorFrame.Attach(canvas, designSession);
    /// // ... edit, playtest in Interact mode, edit more ...
    /// foreach (DesignDocument document in designSession.Documents)
    ///     document.Save();
    /// editor.Dispose();
    /// </code>
    /// </remarks>
    public sealed class EditorFrame : IDisposable
    {
        private readonly Canvas canvas;
        private readonly DesignSession design;
        private readonly TextBlock modeLabel = new() { Margin = new Thickness(8, 0, 0, 0) };
        private readonly TextBlock statusLabel = new() { Foreground = Color.Salmon, Margin = new Thickness(8, 0, 0, 0) };
        private EditorToolbarPlacement toolbarPlacement;
        private string? lastFailure;
        private bool disposed;

        private EditorFrame(Canvas canvas, DesignSession design)
        {
            this.canvas = canvas;
            this.design = design;
            Session = EditorSession.Attach(design, canvas);
            CaptureLayer = new EditorCaptureLayer(this);
            Toolbar = CreateToolbar();
            ToolbarPlacement = EditorToolbarPlacement.TopLeft;
        }

        /// <summary>
        /// Gets the session the frame shows; share it with other tools to share the selection.
        /// </summary>
        public EditorSession Session { get; }

        /// <summary>
        /// Gets or sets the corner the toolbar sits in. Defaults to <see cref="EditorToolbarPlacement.TopLeft"/>.
        /// </summary>
        public EditorToolbarPlacement ToolbarPlacement
        {
            get => toolbarPlacement;
            set
            {
                toolbarPlacement = value;
                bool left = value is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.BottomLeft;
                bool top = value is EditorToolbarPlacement.TopLeft or EditorToolbarPlacement.TopRight;
                Toolbar.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                Toolbar.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            }
        }

        /// <summary>Gets or sets the color of the selection outline and handles.</summary>
        public Color SelectionColor { get; set; } = Color.DeepSkyBlue;

        /// <summary>Gets or sets the color of the hover outline.</summary>
        public Color HoverColor { get; set; } = Color.FromArgb(160, Color.LightSkyBlue);

        /// <summary>Gets or sets the color of the drop indicator.</summary>
        public Color IndicatorColor { get; set; } = Color.Orange;

        /// <summary>Gets or sets the color of the dragged element's ghost outline.</summary>
        public Color GhostColor { get; set; } = Color.FromArgb(200, Color.White);

        /// <summary>Gets or sets the side of the square resize handles, in surface units. Defaults to 7.</summary>
        public float HandleSize { get; set; } = 7;

        internal EditorCaptureLayer CaptureLayer { get; }

        internal Border Toolbar { get; }

        internal string ToolbarLabel => modeLabel.Text ?? string.Empty;

        internal string? ToolbarStatus => string.IsNullOrEmpty(statusLabel.Text) ? null : statusLabel.Text;

        /// <summary>
        /// Attaches an editor to <paramref name="canvas"/>, in <see cref="EditorMode.Edit"/>.
        /// </summary>
        /// <param name="canvas">The canvas whose pages to edit.</param>
        /// <param name="design">The design session tracking those pages.</param>
        /// <returns>The frame; dispose it to detach.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public static EditorFrame Attach(Canvas canvas, DesignSession design)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            ArgumentNullException.ThrowIfNull(design);

            var frame = new EditorFrame(canvas, design);
            frame.Start();
            return frame;
        }

        /// <summary>
        /// Detaches the editor: removes its overlays and key bindings, and gives the canvas its focus and navigation back.
        /// Every edit stays.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            design.Configuration.Input.Events.Touch.Tap -= OnTap;
            Session.ModeChanged -= OnSessionChanged;
            Session.SelectionChanged -= OnSessionChanged;
            Session.BlockedChanged -= OnSessionChanged;
            canvas.RemoveOverlay(CaptureLayer);
            canvas.RemoveOverlay(Toolbar);
            Session.Dispose();
        }

        internal void BeginDrag(Point screenPoint)
        {
        }

        internal void UpdateDrag(Point screenPoint)
        {
        }

        internal void EndDrag(Point screenPoint)
        {
        }

        internal void CancelDrag()
        {
        }

        internal void Render(IRenderContext context)
        {
            if (Session.Mode == EditorMode.Edit && !IsOnTop())
                Dispatcher.GetCurrentThreadDispatcher().Invoke(BringToTop);
        }

        private static string Describe(EditorSelection selection)
        {
            ElementSyntax? element = EditorSession.Syntax(selection);
            if (element == null)
                return selection.Instance.GetType().Name;

            string? name = Editing.TreeMatcher.GetDirective(selection.Document.Syntax, element, MarkupDirectives.Name);
            return name != null ? $"{element.Name} \"{name}\"" : element.Name;
        }

        private Border CreateToolbar()
        {
            var toggle = new Button
            {
                Content = new TextBlock { Text = "Edit / Interact" },
                Padding = new Thickness(8, 4),
                Command = Session.Commands.ToggleMode,
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(toggle);
            row.Children.Add(modeLabel);
            row.Children.Add(statusLabel);
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 32, 32, 38)),
                Padding = new Thickness(6),
                Margin = new Thickness(8),
                Child = row,
            };
        }

        private void Start()
        {
            canvas.AddOverlay(CaptureLayer);
            canvas.AddOverlay(Toolbar);
            Session.OwnLayers.Add(CaptureLayer);
            Session.OwnLayers.Add(Toolbar);
            Session.ModeChanged += OnSessionChanged;
            Session.SelectionChanged += OnSessionChanged;
            Session.BlockedChanged += OnSessionChanged;
            design.Configuration.Input.Events.Touch.Tap += OnTap;
            UpdateToolbar();
        }

        private void OnSessionChanged(object? sender, EventArgs e) => UpdateToolbar();

        private void OnTap(object? sender, GenericEventArgs<TouchInfo> e)
        {
        }

        private void UpdateToolbar()
        {
            CaptureLayer.IsHitTestVisible = Session.Mode == EditorMode.Edit;
            string mode = Session.Mode == EditorMode.Edit ? "Edit" : "Interact";
            string what = Session.Selection is { } selection
                ? Describe(selection)
                : HasTrackedContent() ? "Click an element to select it" : "No tracked pages on this canvas";
            modeLabel.Text = $"{mode} · {what}";
            statusLabel.Text = Session.BlockedReason ?? lastFailure ?? string.Empty;
        }

        private bool HasTrackedContent()
        {
            foreach (DesignDocument document in design.Documents)
            {
                if (document.Syntax.Root is { } root && document.GetNodeId(root) is NodeId id
                    && document.GetObjects(id).OfType<UIElement>().Any(x => ReferenceEquals(x.Canvas, canvas)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOnTop()
        {
            IReadOnlyList<UIElement> overlays = canvas.Overlays;
            return overlays.Count >= 2 && ReferenceEquals(overlays[^1], Toolbar) && ReferenceEquals(overlays[^2], CaptureLayer);
        }

        private void BringToTop()
        {
            if (disposed || IsOnTop())
                return;

            canvas.RemoveOverlay(CaptureLayer);
            canvas.RemoveOverlay(Toolbar);
            canvas.AddOverlay(CaptureLayer);
            canvas.AddOverlay(Toolbar);
        }
    }
}
```

Namespace notes for this file: `Dispatcher` is `Icy.Data.Markup.Dispatcher`; `GenericEventArgs<T>` is in `Icy.Data`; `MarkupDirectives` is in `Icy.Markup`; `TreeMatcher` is `Icy.Design.Editing.TreeMatcher`, internal to `IcyUI.Design`. `SolidColorBrush` lives in `sources/IcyUI/Rendering/Brushes/SolidColorBrush.cs`; use the namespace declared at its top.

`TheFrame_ReturnsOnTopOfALaterPopup` relies on `canvas.Render()` drawing the capture layer (queuing `BringToTop`) and then running `Dispatcher.Update()` at the end of the same frame.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorFrameTests"`
Expected: PASS, Total 8.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1400, no failures), then the warning count (81).

```bash
git add sources/IcyUI.Design/Editor sources/IcyUI.Tests/Design/Editor/EditorFrameTests.cs
git commit -m "Add the editor frame's overlays and toolbar

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Gestures and adorners

**Files:**
- Create: `sources/IcyUI.Design/Editor/AdornerScene.cs`
- Modify: `sources/IcyUI.Design/Editor/EditorFrame.cs` (`OnTap`, the four drag methods, `BuildScene`, `Render`)
- Test: `sources/IcyUI.Tests/Design/Editor/EditorFrameGestureTests.cs`

**Interfaces:**
- Consumes: Task 2's geometry; 10.3a's `BeginMove`/`BeginResize`/`MoveGesture`/`ResizeGesture`.
- Produces: `internal sealed record AdornerScene(RectangleF? Hover, RectangleF? Selection, IReadOnlyList<RectangleF> Handles, bool Dimmed, RectangleF? Indicator, bool IndicatorIsLine, RectangleF? Ghost)`; `internal AdornerScene EditorFrame.BuildScene()`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Editor/EditorFrameGestureTests.cs`:

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
    public class EditorFrameGestureTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="100,100,0,0">
              <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Border x:Name="b" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Button x:Name="ok" Width="100" Height="30">OK</Button>
            </StackPanel>
            """;

        [Fact]
        public void ATap_SelectsTheElement_AndATapOnNothingClearsIt()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var ok = host.Named<Button>("ok");

            host.Input.Events.Touch.RaiseTap(new TouchInfo(host.At(ok, 50, 15), 1));
            Assert.Same(ok, frame.Session.Selection!.Instance);

            host.Input.Events.Touch.RaiseTap(new TouchInfo(new Point(700, 500), 1));
            Assert.Null(frame.Session.Selection);
        }

        [Fact]
        public void AToolbarClick_DoesntChangeTheSelection()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Named<Border>("a"));

            host.Input.Events.Touch.RaiseTap(new TouchInfo(frame.Toolbar.PointToScreen(new Vector2(20, 10)), 1));

            Assert.Same(host.Named<Border>("a"), frame.Session.Selection!.Instance);
        }

        [Fact]
        public void ABodyDrag_MovesTheElement()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var a = host.Named<Border>("a");
            Point from = host.At(a, 50, 10);
            Point to = host.At(host.Named<Button>("ok"), 50, 25);

            host.Input.Events.Gestures.RaiseDragStarted(Drag(from, from));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(from, to));
            Assert.NotNull(frame.BuildScene().Indicator);
            Assert.NotNull(frame.BuildScene().Ghost);
            host.Input.Events.Gestures.RaiseDragCompleted(Drag(from, to));

            Assert.Same(a, host.Named<StackPanel>("root").Children[^1]);
            Assert.Same(a, frame.Session.Selection!.Instance);
        }

        [Fact]
        public void AHandleDrag_ResizesTheElement()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var a = host.Named<Border>("a");
            frame.Session.Select(a);
            Point handle = host.At(a, 100, 10);
            Point to = handle with { X = handle.X + 30 };

            host.Input.Events.Gestures.RaiseDragStarted(Drag(handle, handle));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(handle, to));
            host.Input.Events.Gestures.RaiseDragCompleted(Drag(handle, to));

            Assert.Equal(130f, a.Width);
            Assert.True(host.Document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(100f, a.Width);
        }

        [Fact]
        public void ACancelledDrag_RollsTheResizeBack()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var a = host.Named<Border>("a");
            frame.Session.Select(a);
            Point handle = host.At(a, 100, 10);
            Point to = handle with { X = handle.X + 30 };

            host.Input.Events.Gestures.RaiseDragStarted(Drag(handle, handle));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(handle, to));
            host.Input.Events.Gestures.RaiseDragCanceled(Drag(handle, to));

            Assert.Equal(100f, a.Width);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void TheScene_ShowsTheSelectionAndItsHandles()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Named<Border>("a"));

            AdornerScene scene = frame.BuildScene();

            Assert.Equal(new RectangleF(100, 100, 100, 20), scene.Selection);
            Assert.Equal(8, scene.Handles.Count);
            Assert.False(scene.Dimmed);
        }

        [Fact]
        public void InteractMode_DimsTheSelectionAndHidesTheHandles()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Named<Border>("a"));
            frame.Session.Mode = EditorMode.Interact;

            AdornerScene scene = frame.BuildScene();

            Assert.True(scene.Dimmed);
            Assert.Empty(scene.Handles);
            Assert.Null(scene.Hover);
        }

        [Fact]
        public void TheScene_ShowsTheHoverTarget()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Named<Border>("b"), 5, 5));

            Assert.Equal(new RectangleF(100, 120, 100, 20), frame.BuildScene().Hover);
        }

        [Fact]
        public void Rendering_DrawsTheAdorners()
        {
            using var host = new EditorTestHost(Page);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var render = (Icy.Tests.Rendering.FakeRenderContext)host.Configuration.RenderContext;
            host.Render();
            int before = render.DrawCalls.Count;
            render.DrawCalls.Clear();

            frame.Session.Select(host.Named<Border>("a"));
            host.Render();

            // An outline is four lines; each handle is a fill and four lines.
            Assert.True(render.DrawCalls.Count >= before + 4 + (8 * 5), $"{render.DrawCalls.Count} draw calls, {before} without a selection.");
        }

        private static DragInfo Drag(Point start, Point position) =>
            new(PointerKind.MouseLeft, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);
    }
}
```

`Rendering_DrawsTheAdorners` reads the draw calls through `IcyConfiguration.RenderContext` (public), which `EditorTestHost` builds as a `FakeRenderContext`.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorFrameGestureTests"`
Expected: the build FAILS (`AdornerScene`, `BuildScene` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editor/AdornerScene.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Design.Editor
{
    /// <summary>
    /// What the editor draws this frame, in surface units.
    /// </summary>
    /// <param name="Hover">The outline of the element a click would select, in Edit mode.</param>
    /// <param name="Selection">The selected element's outline.</param>
    /// <param name="Handles">The resize handles; empty in Interact mode and while blocked.</param>
    /// <param name="Dimmed">Whether the selection is drawn dimmed (Interact mode).</param>
    /// <param name="Indicator">The drop target's insertion line or area while moving.</param>
    /// <param name="IndicatorIsLine">Whether <paramref name="Indicator"/> is a line.</param>
    /// <param name="Ghost">The dragged element's outline at the pointer while moving.</param>
    internal sealed record AdornerScene(
        RectangleF? Hover,
        RectangleF? Selection,
        IReadOnlyList<RectangleF> Handles,
        bool Dimmed,
        RectangleF? Indicator,
        bool IndicatorIsLine,
        RectangleF? Ghost);
}
```

In `EditorFrame`, add the fields `private MoveGesture? move;` and `private ResizeGesture? resize;`, `using System.Numerics;` and `using Icy.Design.Editor.Placement;`, and replace the placeholder members:

```csharp
        internal void BeginDrag(Point screenPoint)
        {
            if (Session.Mode != EditorMode.Edit)
                return;

            lastFailure = null;
            if (Session.Selection is { } selection && !Session.IsBlocked)
            {
                RectangleF bounds = AdornerGeometry.SurfaceBounds(selection.Instance);
                ResizeHandle handle = AdornerGeometry.HandleAt(bounds, HandleSize, AdornerGeometry.ScreenToSurface(screenPoint, canvas.EffectiveScale));
                if (handle != ResizeHandle.None)
                {
                    resize = Session.BeginResize(handle, screenPoint);
                    return;
                }
            }

            if (Session.ResolveSelectable(Session.HitTest(screenPoint)) is not { } target)
                return;
            if (!ReferenceEquals(Session.Selection?.Instance, target) && !Session.Select(target))
                return;

            move = Session.BeginMove(screenPoint);
        }

        internal void UpdateDrag(Point screenPoint)
        {
            resize?.Update(screenPoint);
            move?.Update(screenPoint);
        }

        internal void EndDrag(Point screenPoint)
        {
            EditResult? result = null;
            if (resize != null)
            {
                result = resize.Complete();
                resize = null;
            }

            if (move != null)
            {
                move.Update(screenPoint);
                result = move.Complete();
                move = null;
            }

            if (result is { Succeeded: false })
            {
                lastFailure = result.Error?.Message;
                UpdateToolbar();
            }
        }

        internal void CancelDrag()
        {
            resize?.Cancel();
            move?.Cancel();
            resize = null;
            move = null;
        }

        internal AdornerScene BuildScene()
        {
            bool editing = Session.Mode == EditorMode.Edit;
            RectangleF? hover = null;
            if (editing && move == null && resize == null)
            {
                Point mouse = design.Configuration.Input.Mouse.MouseInfo.Position;
                if (Session.ResolveSelectable(Session.HitTest(mouse)) is { } hovered && !ReferenceEquals(hovered, Session.Selection?.Instance))
                    hover = AdornerGeometry.SurfaceBounds(hovered);
            }

            RectangleF? selected = null;
            IReadOnlyList<RectangleF> handles = [];
            if (Session.Selection is { } selection && selection.Instance.Canvas == canvas)
            {
                RectangleF bounds = AdornerGeometry.SurfaceBounds(selection.Instance);
                selected = bounds;
                if (editing && !Session.IsBlocked && move == null)
                    handles = [.. AdornerGeometry.Handles(bounds, HandleSize).Select(x => x.Area)];
            }

            RectangleF? indicator = null;
            RectangleF? ghost = null;
            bool line = false;
            if (move != null)
            {
                ghost = AdornerGeometry.ScreenToSurface(move.GhostBounds, canvas.EffectiveScale);
                if (move.Target is { } target && move.TargetContainer is { } container)
                {
                    indicator = AdornerGeometry.ToSurface(container, target.Indicator);
                    line = target.IndicatorIsLine;
                }
            }

            return new AdornerScene(hover, selected, handles, !editing, indicator, line, ghost);
        }

        internal void Render(IRenderContext context)
        {
            if (Session.Mode == EditorMode.Edit && !IsOnTop())
                Dispatcher.GetCurrentThreadDispatcher().Invoke(BringToTop);

            AdornerScene scene = BuildScene();
            if (scene.Hover is { } hover)
                Outline(context, hover, HoverColor, 1);
            if (scene.Selection is { } selection)
                Outline(context, selection, scene.Dimmed ? Color.FromArgb(110, SelectionColor) : SelectionColor, 1.5f);
            foreach (RectangleF handle in scene.Handles)
            {
                context.FillRectangle(new Vector2(handle.X, handle.Y), new Vector2(handle.Width, handle.Height), Color.White);
                Outline(context, handle, SelectionColor, 1);
            }

            if (scene.Ghost is { } ghost)
                Outline(context, ghost, GhostColor, 1);
            if (scene.Indicator is { } indicator)
            {
                if (scene.IndicatorIsLine)
                    context.DrawLine(indicator.Left, indicator.Top, indicator.Right, indicator.Bottom, IndicatorColor, 3);
                else
                    Outline(context, indicator, IndicatorColor, 2);
            }
        }

        private static void Outline(IRenderContext context, RectangleF area, Color color, float thickness) =>
            context.DrawRectangle(new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), color, thickness);
```

and `OnTap`:

```csharp
        private void OnTap(object? sender, GenericEventArgs<TouchInfo> e)
        {
            if (disposed || Session.Mode != EditorMode.Edit)
                return;

            // Only taps the capture layer receives: a tap on the toolbar belongs to its buttons.
            Point point = e.Data.LastTouch;
            if (!ReferenceEquals(canvas.HitTest(point), CaptureLayer))
                return;

            if (Session.ResolveSelectable(Session.HitTest(point)) is { } target)
                Session.Select(target);
            else
                Session.Clear();
        }
```

The capture layer is an overlay at the surface origin, so its local space is surface space and adorners draw at their surface coordinates. `DrawRectangle`, `FillRectangle` and `DrawLine` are `Icy.Rendering.ShapesExtensions` extension methods.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Editor"`
Expected: PASS, `EditorFrameGestureTests` Total 9, and every other editor test still passes.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1409, no failures), then the warning count (81).

```bash
git add sources/IcyUI.Design/Editor sources/IcyUI.Tests/Design/Editor
git commit -m "Drive the editor session from pointer gestures and draw its adorners

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `EditorDemo` in both hosts, and the frame's overhead

**Files:**
- Create: `sources/Shared Samples/EditorDemo.cs`, `sources/MonoGame Sample/Samples/EditorSample.cs`
- Modify: `sources/Shared Samples/DesignDemo.cs` (`SessionFor`), `sources/Stride Sample/SampleGame.cs`, `sources/MonoGame Sample/SampleGame.cs`, `sources/Stride Sample/Stride Sample.csproj`, `sources/MonoGame Sample/MonoGame Sample.csproj`, `sources/IcyUI.Tests/IcyUI.Tests.csproj`
- Test: `sources/IcyUI.Tests/Samples/EditorDemoTests.cs`, `sources/IcyUI.Tests/Design/Editor/EditorFramePerformanceTests.cs`

**Interfaces:**
- Consumes: `EditorFrame`, `DesignDocument.SaveAs`.
- Produces: `public static UIElement EditorDemo.Build(IcyConfiguration configuration, string fontFamily)`; `internal static DesignSession DesignDemo.SessionFor(IcyConfiguration configuration)`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Samples/EditorDemoTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class EditorDemoTests
    {
        [Fact]
        public void TheEditButton_AttachesAndDetachesTheFrame()
        {
            (Canvas canvas, UIElement root) = Build();
            Button edit = FindButton(root, "Edit this page");

            edit.Command!.Execute(null);
            int withEditor = canvas.Overlays.Count;
            FindButton(root, "Stop editing").Command!.Execute(null);

            Assert.Equal(2, withEditor);
            Assert.Empty(canvas.Overlays);
        }

        [Fact]
        public void HidingTheDemo_DetachesTheEditor()
        {
            (Canvas canvas, UIElement root) = Build();
            FindButton(root, "Edit this page").Command!.Execute(null);

            root.IsVisible = false;

            Assert.Empty(canvas.Overlays);
            Assert.True(canvas.IsKeyboardNavigationEnabled);
        }

        [Fact]
        public void ThePage_IsTrackedByTheSharedSession()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var root = (StackPanel)EditorDemo.Build(configuration, "Airfool");

            Assert.NotNull(DesignDemo.SessionFor(configuration).FindDocument(root.Children[0], out _));
        }

        private static (Canvas Canvas, UIElement Root) Build()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            UIElement root = EditorDemo.Build(configuration, "Airfool");
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(root);
            canvas.Render();
            return (canvas, root);
        }

        private static Button FindButton(UIElement root, string text) =>
            root.EnumerateVisualSubtree().OfType<Button>().First(x => x.Content is TextBlock { Text: var label } && label == text);
    }
}
```

`sources/IcyUI.Tests/Design/Editor/EditorFramePerformanceTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Text;
using Icy.Design.Editor;
using Icy.Input.Devices;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design.Editor
{
    public class EditorFramePerformanceTests(ITestOutputHelper output)
    {
        [Fact]
        public void EditModeOverhead_OnA500ElementPage_IsMeasured()
        {
            var markup = new StringBuilder("<StackPanel x:Name=\"root\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">");
            for (int i = 0; i < 500; i++)
                markup.Append("<Border Width=\"4\" Height=\"1\"/>");
            markup.Append("</StackPanel>");
            using var host = new EditorTestHost(markup.ToString());
            host.Session.Dispose();

            double baseline = Measure(host);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Root);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Root, 2, 2));
            double editing = Measure(host);

            output.WriteLine($"Canvas.Render on 500 elements: {baseline:0.000} ms detached, {editing:0.000} ms in Edit mode, overhead {editing - baseline:0.000} ms (budget 0.2 ms).");
        }

        private static double Measure(EditorTestHost host)
        {
            for (int i = 0; i < 30; i++)
                host.Render();

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 60; i++)
                host.Render();
            return watch.Elapsed.TotalMilliseconds / 60;
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorDemoTests|FullyQualifiedName~EditorFramePerformanceTests"`
Expected: the build FAILS (`EditorDemo`, `DesignDemo.SessionFor` not found; `EditorDemo.cs` isn't linked yet).

- [ ] **Step 3: Implement**

In `DesignDemo.cs`, add after the `Sessions` field:

```csharp
        /// <summary>
        /// Gets the design session the demos share for <paramref name="configuration"/>; a configuration has a single
        /// load-observer slot, so every demo uses the same one.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <returns>The session, attached on first use.</returns>
        internal static DesignSession SessionFor(IcyConfiguration configuration) => Sessions.GetValue(configuration, DesignSession.Attach);
```

and use it in `Build` (`DesignSession session = SessionFor(configuration);`).

`sources/Shared Samples/EditorDemo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.IO;
using Icy.Configuration;
using Icy.Design;
using Icy.Design.Editor;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A page to edit with <see cref="EditorFrame"/>: a stack, a grid with spans, a free panel for margin moves, a text
    /// box and a combo box. The manual check for the editor overlay (Phase 10.3).
    /// </summary>
    public static class EditorDemo
    {
        /// <summary>
        /// The page the demo edits.
        /// </summary>
        public const string Markup =
            """
            <StackPanel x:Name="page" Orientation="Vertical" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TextBlock x:Name="title" FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,8">Edit me: select, drag, resize</TextBlock>
              <StackPanel x:Name="stack" Orientation="Horizontal" Margin="0,0,0,8">
                <Button Padding="12,6" Margin="0,0,8,0">One</Button>
                <Button Padding="12,6" Margin="0,0,8,0">Two</Button>
                <Button Padding="12,6">Three</Button>
              </StackPanel>
              <Grid x:Name="grid" Width="360" Height="140" Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="120"/>
                  <ColumnDefinition Width="120"/>
                  <ColumnDefinition Width="120"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                  <RowDefinition Height="70"/>
                  <RowDefinition Height="70"/>
                </Grid.RowDefinitions>
                <Border Background="LightCoral" Grid.ColumnSpan="2"/>
                <Border Background="LightSkyBlue" Grid.Column="2" Grid.RowSpan="2"/>
                <Border Background="LightGreen" Grid.Row="1"/>
              </Grid>
              <Panel x:Name="free" Width="360" Height="100" Margin="0,0,0,8">
                <Border Background="Gold" Width="60" Height="30" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="10,10,0,0"/>
                <Border Background="Plum" Width="60" Height="30" HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,10,10"/>
              </Panel>
              <TextBox x:Name="text" Width="200" Margin="0,0,0,8"/>
              <ComboBox x:Name="combo" Width="160"/>
            </StackPanel>
            """;

        /// <summary>
        /// Builds the demo: the tracked page, an "Edit this page" toggle, a Save button and a status line.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The demo's root element.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;

            DesignSession session = DesignDemo.SessionFor(configuration);
            UIElement page = new MarkupLoader(configuration).Load(Markup, nameof(EditorDemo));
            DesignDocument document = session.FindDocument(page, out _)!;
            ((ComboBox)MarkupNameScope.GetScope(page)!.Find("combo")!).ItemsSource = new[] { "One", "Two", "Three" };

            var status = new TextBlock { Text = "Press \"Edit this page\", then click, drag and resize. Ctrl+Shift+E toggles Interact mode.", Margin = new Thickness(0, 6, 0, 0) };
            var editLabel = new TextBlock { Text = "Edit this page" };
            var edit = new Button { Content = editLabel, Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 8, 0) };
            var save = new Button { Content = new TextBlock { Text = "Save" }, Padding = new Thickness(12, 6) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            toolbar.Children.Add(edit);
            toolbar.Children.Add(save);

            var root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20),
            };
            root.Children.Add(page);
            root.Children.Add(toolbar);
            root.Children.Add(status);

            EditorFrame? frame = null;
            void Detach()
            {
                frame?.Dispose();
                frame = null;
                editLabel.Text = "Edit this page";
            }

            edit.Command = new DemoCommand(() =>
            {
                if (frame != null)
                {
                    Detach();
                    return;
                }

                if (root.Canvas is not { } canvas)
                    return;

                frame = EditorFrame.Attach(canvas, session);
                editLabel.Text = "Stop editing";
            });
            save.Command = new DemoCommand(() =>
            {
                string path = Path.Combine(Path.GetTempPath(), "IcyEditorDemo.xml");
                document.SaveAs(path);
                status.Text = $"Saved to {path}";
            });

            // The hosts switch demos on one canvas: an editor left in Edit mode would keep the other demos' input.
            root.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UIElement.IsVisible) && !root.IsVisible)
                    Detach();
            };

            return root;
        }

        private sealed class DemoCommand(Action execute) : System.Windows.Input.ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute();
        }
    }
}
```

The test project links demos as `<Compile Include="..\Shared Samples\DesignDemo.cs" Link="Samples\DesignDemo.cs" />`; add the same line for `EditorDemo.cs` to `IcyUI.Tests.csproj`, `MonoGame Sample.csproj` (`Link="Samples\EditorDemo.cs"`) and `Stride Sample.csproj` (`Link="EditorDemo.cs"`). `UIElement.IsVisible` is set through `SetProperty`, so `PropertyChanged` reports it. `<Panel>` resolves from markup like `<Border>` (both are public classes in `Icy.UI`).

**Stride host** (`sources/Stride Sample/SampleGame.cs`): add `private UIElement? editorRoot;`, build it right after `designRoot` (`editorRoot = EditorDemo.Build(configuration, "Airfool");`), `canvas.Add(editorRoot);` after `canvas.Add(designRoot);`, change `% 18` to `% 19`, add `editorRoot!.IsVisible = selectedDemo == 18;` to `UpdateSelectedDemo`, and add `<see cref="EditorDemo"/>` to the class summary's demo list.

**MonoGame host:** `sources/MonoGame Sample/Samples/EditorSample.cs`, modeled on `DesignSample.cs`:

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
    /// Runs <see cref="EditorDemo"/> - a page edited with the design-time editor overlay - as its own selectable sample.
    /// </summary>
    internal class EditorSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public EditorSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "Editor Demo")
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

            demoRoot = EditorDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
```

Register it in `MonoGame Sample/SampleGame.cs` after `new DesignSample(...)`: `new EditorSample(this, uiConfiguration, canvas)`.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorDemoTests|FullyQualifiedName~EditorFramePerformanceTests" --logger "console;verbosity=detailed"`
Expected: PASS, Total 4. Copy the measurement line into the task report.

- [ ] **Step 5: Build everything, run the whole suite, commit**

Run:
```bash
dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"
```
Expected: 0 errors, 81 warnings (both sample hosts compile). Total **1413** (1382 + 2 + 8 + 8 + 9 + 4), no failures.

```bash
git add "sources/Shared Samples" "sources/MonoGame Sample" "sources/Stride Sample" sources/IcyUI.Tests
git commit -m "Add EditorDemo to both sample hosts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Hand over for review and Ivan's smoke test**

Run the whole-branch review of 10.3a and 10.3b together. Then give Ivan this checklist for both engines; don't claim any of it yourself:

1. Open **Editor Demo** and press **Edit this page**. The toolbar appears top-left, reading "Edit · Click an element to select it".
2. Hover: a thin outline follows the element under the mouse. Click a button: it's selected with eight handles, and pressing it doesn't fire it.
3. Drag "One" past "Three": an insertion line follows, and the button lands there on release.
4. Drag the coral grid cell's right handle into the third column past a quarter: it spans three columns. Drag the gold box in the free panel: it moves by its margin.
5. Arrow keys nudge by 1, Shift+arrows by 10; Delete removes; Ctrl+Z undoes each gesture in one step.
6. Ctrl+Shift+E (or the toolbar button) switches to Interact: open the combo box, switch back to Edit, and select an item in the open dropdown.
7. Click into the text box in Interact mode, switch to Edit, and type: nothing reaches the text box.
8. Save: open the shown file; its diff against the original markup contains only what you changed.
9. Switch demos with PageDown while editing: the editor detaches and the next demo responds to input.
