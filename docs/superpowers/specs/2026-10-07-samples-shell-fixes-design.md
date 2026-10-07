# Samples shell follow-up fixes

> Short design spec, discussed with Ivan on 2026-10-07 after the first look at the samples shell
> (`2026-10-07-samples-shell-design.md`). There is no separate plan: the fixes are implemented test-first from this spec.

## Context

Running the shell showed four problems:

1. On a high-resolution laptop, a fixed 1280x720 window is hard to use. Both hosts should be resizable, which also
   tests how the library handles resizing.
2. A demo's page grows sideways inside the shell's `ScrollViewer`. `Measure()` has no width limit and a `TextBlock`
   reports its full one-line width, so `ScrollViewer.ArrangeContent` (which gives content `max(viewport, desired)` on
   both axes) widens the whole page. In the Selector demo, the dropdowns are then centered in a page wider than the
   window.
3. `ColorPickerButton`'s popup overflows the right edge of the window when the button sits near it. `PositionPopup`
   flips the popup above or below its owner but never fits it horizontally.
4. Found while investigating 1: resizing the viewport doesn't re-lay out the UI. `Canvas.Render` invalidates the root
   elements only when the effective *scale* changes. The public `Canvas.OnResize()` exists but nothing calls it.

## Decisions

| Problem | Decision |
|---|---|
| Resizable windows | Both sample hosts allow user resizing. |
| Resize re-layout | `Canvas.Render` polls the physical viewport size every frame, as `RefreshScale` already polls the scale. |
| Sideways growth | New per-axis `ScrollViewer` API, WinUI-style. The shell disables horizontal scrolling. |
| Popup overflow | Flip, then clamp: right-align the popup with its owner, then clamp it to the canvas surface. |

## Design

### Canvas: re-layout on viewport resize

- `Canvas.Render` remembers the last physical `RenderContext.ViewportSize`. When it changes, it calls `OnResize()`.
- `OnResize()` invalidates the arrange of the root elements **and the overlays**, so popups and dialogs are re-laid out
  against the new `SurfaceSize`. Open popups follow their owners through the existing `OnArrangeUpdated` repositioning.
- Polling, not `IRenderContext.ViewportResize`: the canvas has no disposal point, and an event subscription would keep
  every discarded canvas alive (the same reasoning as `RefreshScale`'s remarks).

### ScrollViewer: `HorizontalScrollMode` / `VerticalScrollMode`

```csharp
public enum ScrollMode { Enabled, Disabled }

public ScrollMode HorizontalScrollMode { get; set; } // default Enabled
public ScrollMode VerticalScrollMode { get; set; }   // default Enabled
```

On a disabled axis:
- `ArrangeContent` lays the content out at the viewport size, the way any non-scrolling parent would.
- `ExtentWidth`/`ExtentHeight` is capped at the viewport, so the offset stays at 0. Everything that scrolls goes through
  the clamped offset setters, so wheel, drag, fling, panning and `BringIntoView` all stop scrolling that axis.
- Changing either property invalidates arrange.
- Virtualizing content (`IVirtualizingScrollInfo`) already lays itself out inside the viewport, so only its extent cap
  applies.

`ScrollMode` is a separate enum from `PanningMode`: panning is about input, and scroll mode is about layout.

The samples shell sets `HorizontalScrollMode = Disabled` on every demo's `ScrollViewer`.

### Popups: fit inside the canvas surface

Popups are canvas overlays. They sit above every other control, in surface space, and a `ScrollViewer` never clips
or contains them. So the horizontal fit is against `Canvas.SurfaceSize`, not against an enclosing scroller:

1. Left-align the popup with its owner (unchanged).
2. If it would cross the right edge of the surface, right-align it with the owner's right edge instead.
3. Clamp the result into `[0, SurfaceSize.Width - popupWidth]`.

Both `ColorPickerButton.PositionPopup` and `Selector.PositionPopup` use the same rule. A `Selector`'s popup is as wide
as the control, so in practice the rule only matters when the control itself is partly off-screen.

### Hosts

- **MonoGame Sample:** `Window.AllowUserResizing = true`. On `Window.ClientSizeChanged`, while windowed, it copies the
  client size into `PreferredBackBufferWidth`/`Height` and calls `ApplyChanges()`, so `RenderContext.ViewportSize`
  (read from the back buffer) follows the window.
- **Stride Sample:** `Window.AllowUserResizing = true`. Stride's presenter resizes the back buffer itself.
- **FNA (stub):** a future FNA host needs the same `ClientSizeChanged` handling as MonoGame.

## Testing

- **Canvas:** after a viewport size change, the next `Render` re-arranges a stretched root to the new surface, and an
  open popup follows its owner.
- **ScrollViewer:** with `HorizontalScrollMode = Disabled`, content wider than the viewport is arranged at the
  viewport's width. `ExtentWidth` equals the viewport width, and `HorizontalOffset` stays 0 after setting it and after
  a horizontal wheel. With `VerticalScrollMode = Disabled`, the same holds vertically. The default stays scrollable on
  both axes.
- **Shell:** a demo page with text wider than the viewport isn't wider than the content area.
- **Popups:** a `ColorPickerButton` near the right edge opens its popup right-aligned with the button and inside the
  surface. A popup wider than the surface is clamped to x = 0.
- **Hosts:** build only. Ivan smoke-tests resizing on both engines.

## Performance

- One `Size` comparison per frame in `Canvas.Render`. A re-layout happens only on frames where the size changed.
- `ScrollMode` adds one enum check to the extent getters and `ArrangeContent`.
- The popup fit is a few integer comparisons per reposition.
