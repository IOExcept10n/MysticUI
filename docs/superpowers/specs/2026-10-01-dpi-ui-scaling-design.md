# DPI-aware UI scaling

> Design spec, brainstormed with Ivan on 2026-09-30/10-01. It came out of the Windows-on-ARM64 work: on a
> 200 % display the MonoGame Sample (DPI-aware manifest) drew the whole UI at half size, while the Stride
> Sample (DPI-unaware) was bitmap-stretched by Windows. IcyUI has no concept of display scale at all.

## Context

IcyUI lays out and renders in raw back-buffer pixels. `Canvas.ContentBounds` is
`IRenderContext.ViewportSize`, overlays and the debug HUD draw with an identity transform in physical
pixels, and dynamic fonts rasterize at their nominal point size. On HiDPI screens a DPI-aware game therefore
gets a tiny UI. A DPI-unaware one gets a blurry, OS-stretched UI.

`Canvas` already has a pan/rotate/scale transform (`Offset`/`Rotation`/`Scale`), but it can't be used as a
DPI factor. Layout still runs against the physical viewport, so `Scale = 2` would lay content out across
the whole back buffer and then draw it doubled, off-screen. Overlays ignore that transform entirely.

**Cross-engine impact:** core `IcyUI` gets the whole feature, including the OS-specific DPI queries.
`IcyUI.MonoGame` and `IcyUI.Stride` get thin connectors that supply the native window, plus pixel snapping in
their `RenderContext.Draw`. `IcyUI.FNA` (still a stub) needs the same connector once it's implemented and
nothing else.

## Decisions

| Decision | Choice |
|---|---|
| Scale sources | All three: **OS DPI** (automatic), a **user UI-scale** setting (options slider), and a **reference resolution** (design at e.g. 1920×1080, fit to the window). Ivan's explicit choice. |
| Composition | `EffectiveScale = base(mode) × UserScale`, where `mode ∈ { None, Dpi, ReferenceResolution }`. DPI and reference-fit are alternative ways to pick the base and are never multiplied together; the user scale always multiplies. |
| Default mode | `Dpi`. A DPI-unaware host reports 1.0, so existing apps are unaffected. DPI-aware hosts get correct sizing automatically. |
| Reference fit policies | `Fit` (min ratio, default), `Fill` (max ratio), `MatchWidth`, `MatchHeight`. No Unity-style blend weight (YAGNI). |
| Settings scope | **Hybrid.** App-wide defaults in a new `IcyConfiguration.Scaling` facet (mode, reference size, fit, user scale). Each `Canvas` may override mode, reference size and fit. `UserScale` is global only, so one options slider drives every canvas. |
| DPI source | `IRenderContext` gains `DisplayScale` (default interface member returning `1f`) and `DisplayScaleChanged`. DPI describes the output surface, which `IRenderContext` already owns (`ViewportSize`/`ViewportResize`). The default member keeps test fakes and the FNA stub compiling. |
| OS-specific code | In **core**, following the `Icy.Input.Clipboard` pattern: interface + internal per-OS implementations + fallback + static selector. Engine projects only provide connectors (native window info). Ivan's explicit direction, to avoid OS × engine combinations. |
| Rendering approach | Logical units at the canvas root (a new "surface" space), with crisp text through device-resolution glyph rasterization and edge-based pixel snapping. Rejected: render-to-texture upscaling (always blurry, extra pass) and scaling every metric at measure time (touches every control). |

## Detailed design

### 1. Public API and configuration

New types in `Icy.UI`:

```csharp
public enum UIScaleMode { None, Dpi, ReferenceResolution }
public enum ReferenceFit { Fit, Fill, MatchWidth, MatchHeight }
```

New `sources/IcyUI/Configuration/ScalingConfiguration.cs`, deriving from `ObservableDispatcherObject`:

| Member | Default | Notes |
|---|---|---|
| `UIScaleMode Mode` | `Dpi` | |
| `Size ReferenceSize` | 1920×1080 | Both dimensions must be > 0. |
| `ReferenceFit ReferenceFit` | `Fit` | |
| `float UserScale` | `1f` | Must be finite and > 0. |

`IcyConfiguration.Scaling` is a new always-non-null facet (like `Theme`), passed through an optional
constructor parameter. It is exposed through `IScalingConfigurationBuilder` /
`IConfigurationBuilder.ConfigureScaling(...)`, mirroring `IThemeConfigurationBuilder`.

`Canvas` gains:

| Member | Notes |
|---|---|
| `UIScaleMode? ScaleMode` | `null` = inherit `Configuration.Scaling.Mode`. |
| `Size? ReferenceSize` | `null` = inherit. |
| `ReferenceFit? ReferenceFit` | `null` = inherit. |
| `float EffectiveScale { get; }` | Read-only; raises `PropertyChanged`. |
| `Size SurfaceSize { get; }` | Physical viewport ÷ `EffectiveScale`, floored to whole units. |

`Canvas.ContentBounds` becomes `new(Point.Empty, SurfaceSize)`. The existing `Offset`/`Rotation`/`Scale`
keep their meaning and are now expressed in surface units.

`IRenderContext` gains:

```csharp
float DisplayScale => 1f;
event EventHandler? DisplayScaleChanged { add { } remove { } }
```

### 2. Canvas pipeline

Three coordinate spaces:

| Space | Units | Used by |
|---|---|---|
| Physical | back-buffer pixels | input devices, `IRenderContext`, scissor rectangles |
| Surface | physical ÷ `EffectiveScale` | overlays, debug HUD, popup placement, `SurfaceSize` |
| Content | surface through the canvas's own `Offset`/`Rotation`/`Scale` | root elements (unchanged) |

- **Transforms.** `surfaceTransform = Scale(EffectiveScale)`. The existing content transform is composed on
  top, so root elements draw with *content × surface*. Overlays and the debug HUD draw with *surface*
  (previously identity). The scissor stays in physical pixels.
- **Layout.** `ContentBounds`, overlay arrangement (`UpdateLayout`), `DebugHudHost` and the viewport clamping
  in `Selector` (`Selector.cs:549`) and `ColorPickerButton` (`ColorPickerButton.cs:191`) all switch from
  `RenderContext.ViewportSize` to `Canvas.SurfaceSize`. These are the only control-level changes.
- **Input.** Pointer positions still arrive in physical pixels. `HitTest` converts physical → surface for
  overlays. `ScreenToCanvasSpace` inverts the full composed transform, so drag, touch and scroll handling
  converts correctly without changes to the input system. The internal `CanvasToScreenSpace`, used to
  anchor popups, returns **surface** coordinates, matching where overlays live. Rename both internals to
  say "surface" where that removes ambiguity.
- **Recomputing.** The canvas computes `EffectiveScale` once per update from its current inputs: physical
  viewport size, `RenderContext.DisplayScale`, `Configuration.Scaling` and its own overrides. When the value
  differs from the cached one, it calls `InvalidateTransform()`, gives every root element and overlay
  `InvalidateMeasure()`, and raises `PropertyChanged` for `EffectiveScale`/`SurfaceSize`. Setting an override
  triggers the same path immediately.

  The canvas deliberately **subscribes to no events**, matching how it handles viewport size today (it has
  no `ViewportResize` subscription and isn't `IDisposable`). Subscribing to the long-lived configuration or
  render context from a canvas with no disposal point would leak every discarded canvas.
  `DisplayScaleChanged` exists for application code, not for `Canvas`.
- **Scale formula.**
  - `None` → 1.
  - `Dpi` → `RenderContext.DisplayScale`.
  - `ReferenceResolution` → `rw = physical.Width / ref.Width`, `rh = physical.Height / ref.Height`. Then
    `Fit` = `min(rw, rh)`, `Fill` = `max(rw, rh)`, `MatchWidth` = `rw`, `MatchHeight` = `rh`.
  - The result is multiplied by `UserScale`, and a non-finite or ≤ 0 result falls back to 1.

### 3. Crisp text and pixel snapping

**Device-resolution glyphs (`SpriteFont.DrawString`, core).**
- Read the uniform scale `s` from `context.Transform`. If `|s − 1| ≥ 0.01` and the font is a
  `DynamicSpriteFont`, obtain a *device font* for the same `FontInfo` with `Size × q(s)`, where `q` rounds to
  the nearest 0.25. It comes through the owning `FontSystem` and shares its atlas and rasterizer cache.
  Quantizing keeps animated scales (`RenderScale` tweens, `Expander`) to a handful of cached sizes.
- **Pen positions** (advance, kerning, line height, baseline) still come from the **logical** font. Only
  the glyph bitmap, its size and its bearing come from the device font, and each quad is drawn at
  `1/q(s)` of its device size. Drawn text width therefore equals measured text width exactly. `TextBlock`
  and `TextBox` measure with the logical font, and hinting differences between sizes must not clip or
  overflow text.
- `DynamicSpriteFont` needs a way to reach its `FontSystem` (or an equivalent resolver) to get the device
  font. Today `FontSystem` creates it (`FontSystem.cs:266`) but the font doesn't keep a back-reference.
- Static bitmap fonts (`StaticSpriteFont`, `.fnt`) keep today's behavior and are simply scaled.

**Pixel snapping (`IcyUI.MonoGame` and `IcyUI.Stride` `RenderContext.Draw`).**
- Both engines already apply `Transform` per quad (position transformed, scale multiplied), not through a
  sprite-batch matrix, so the final physical rectangle is known inside `Draw`.
- When the combined rotation is 0, round the quad's **edges** (left/top and right/bottom) to whole
  physical pixels, instead of rounding position and size separately. Elements that share an edge in
  logical units then share it in physical pixels, so no seams appear at 125 %/150 %.
- Rotated quads are not snapped.
- The rounding lives in a pure core helper (e.g. `Transform2D`/`RenderExtensions`) so it is unit-testable
  and identical for both engines.
- This is a behavior change: sub-pixel motion becomes 1-pixel steps. At scale 1, positions are already
  integral almost everywhere.

### 4. Platform DPI detection (core) and engine connectors

New namespace `Icy.Rendering.Display` in core, mirroring `Icy.Input.Clipboard`:

| Type | Visibility | Role |
|---|---|---|
| `NativeWindowInfo` | public `readonly record struct (nint Handle, Size WindowSize, Size DrawableSize)` | The only data engines supply. |
| `IDisplayScaleProvider` | public | `float GetScale(in NativeWindowInfo window)`. |
| `WindowsDisplayScaleProvider` | internal | `[LibraryImport("user32.dll")] GetDpiForWindow(hwnd) / 96f`, or `GetDpiForSystem() / 96f` when `Handle` is 0. This respects the host process's DPI awareness: an unaware host gets 96 → 1.0, so the OS stretch and IcyUI scaling never stack. |
| `OsxDisplayScaleProvider`, `LinuxDisplayScaleProvider` | internal | `DrawableSize / WindowSize` ratio (Retina-style), else 1.0. |
| `FixedDisplayScaleProvider` | public | Always 1.0 (or a given constant); the `VirtualClipboard` equivalent and a test double. |
| `DisplayScales` | public static | `GetPlatformProvider()`, `TryGetPlatformProvider(out …)`, `GetProvider()` (falls back to `FixedDisplayScaleProvider`). |
| `DisplayScaleTracker` | public | Takes an `IDisplayScaleProvider` and a `Func<NativeWindowInfo>`. `Poll()` recomputes and raises `Changed` only when the value differs; exposes `Scale`. Invalid results (≤ 0, non-finite) are treated as 1.0. |

**Engine connectors** (the only engine-side DPI code):
- `IcyUI.MonoGame`: build `NativeWindowInfo` from the game window. On Windows the HWND comes from the SDL
  window (DesktopGL's `Window.Handle` is an `SDL_Window*`), through `SDL_GetWindowWMInfo` in the `SDL2.dll`
  that ships with DesktopGL. The render context forwards `DisplayScale`/`DisplayScaleChanged` to a
  `DisplayScaleTracker`, and `MonoGameIcyRenderer.Update` calls `Poll()` once per update.
- `IcyUI.Stride`: the same, with the handle from `Game.Window.NativeWindow` and `Poll()` in
  `IcyUIGameSystem.Update`.
- Polling once per update is one cheap call. It avoids competing with the engines for their SDL event
  queues, which both consume internally.

**Sample hosts:**
- `Stride Sample` gets the same PerMonitorV2 `app.manifest` as `MonoGame Sample`, so both behave
  identically on HiDPI displays.
- New shared `sources/Shared Samples/ScalingDemo.cs`, registered in both hosts (plus a
  `MonoGame Sample/Samples/ScalingSample.cs` wrapper, following the existing pattern). It contains:
  - a mode selector (`ComboBox`)
  - a fit selector
  - a `UserScale` `Slider` bound to `Configuration.Scaling.UserScale`
  - a live readout of `DisplayScale`, `EffectiveScale` and `SurfaceSize`
  - a `ComboBox` and a `ColorPickerButton` to exercise popup placement under scaling

## Testing

Unit tests (core, no engine):
- `EffectiveScale` for every mode × fit, with `UserScale`, including a viewport whose aspect ratio differs
  from the reference. Invalid inputs fall back to 1.
- Canvas overrides versus configuration inheritance. Changes to `Configuration.Scaling` or the fake render
  context's `DisplayScale` are picked up on the next update, and a discarded canvas is not kept alive by
  the configuration (no event subscriptions).
- `SurfaceSize` and root-element arrangement at 1.5× and 2×.
- Hit-testing and `ScreenToCanvasSpace` round trips at 2×, both with and without the canvas's own
  `Offset`/`Scale`.
- Overlays arranged against `SurfaceSize` and hit at the correct physical point.
- `DisplayScaleTracker` raises `Changed` only on real changes (fake provider). `DisplayScales.GetProvider()`
  never returns `null`.
- Device-font text: drawn advance equals measured advance at 1.5×; the scale quantization boundaries.
- The edge-snapping helper: shared edges stay shared at 1.25×/1.5×; rotated input is not snapped.

Build: `dotnet build sources/IcyUI.sln` with no new warnings. `IcyUI.Stride` stays at zero.

Manual (Ivan runs it): `ScalingDemo` in both engines on the 200 % laptop and on the 100 % desktop; drag a
window between monitors with different DPI where available.

## Critical files

- `sources/IcyUI/UI/UIScaleMode.cs`, `ReferenceFit.cs` (new)
- `sources/IcyUI/Configuration/ScalingConfiguration.cs`, `IScalingConfigurationBuilder.cs` (new);
  `IcyConfiguration.cs`, `IcyConfigurationBuilder.cs`, `IConfigurationBuilder.cs`, `BuildingExtensions.cs`
  (extended)
- `sources/IcyUI/Rendering/IRenderContext.cs` (default members)
- `sources/IcyUI/Rendering/Display/*` (new namespace)
- `sources/IcyUI/UI/Canvas.cs` (surface space, scale, recompute)
- `sources/IcyUI/Diagnostics/DebugHudHost.cs`, `UI/Controls/Selector.cs`, `UI/Controls/ColorPickerButton.cs`
  (`SurfaceSize`)
- `sources/IcyUI/Rendering/Fonts/SpriteFont.cs`, `DynamicSpriteFont.cs`, `FontSystem.cs` (device fonts)
- `sources/IcyUI.MonoGame/Rendering/RenderContext.cs`, `Configuration/MonoGameIcyRenderer.cs`
- `sources/IcyUI.Stride/Rendering/RenderContext.cs`, `Configuration/IcyUIGameSystem.cs`
- `sources/Stride Sample/app.manifest` (new), `Stride Sample.csproj`
- `sources/Shared Samples/ScalingDemo.cs` (new) and its registration in both hosts
- `sources/IcyUI.Tests/...` (new test files per area)

## Open items for implementation planning (not blocking spec approval)

- **Spike first:** confirm the native-handle path on both engines. That's `SDL_GetWindowWMInfo` against
  MonoGame's bundled SDL2 (struct layout per SDL version) and Stride 4.3's `NativeWindow` type on Windows. If
  either is impractical, that connector passes `Handle = 0` and `WindowsDisplayScaleProvider` falls back to
  `GetDpiForSystem()`. Per-monitor changes then aren't detected for that engine; record this as a known
  limitation.
- Exactly how `DynamicSpriteFont` reaches `FontSystem` (a back-reference or a resolver delegate) is decided
  in the plan.
- Whether `Canvas`'s internal `ScreenToCanvasSpace`/`CanvasToScreenSpace` get renamed is decided in the
  plan, with a grep of all callers.
- `UserScale` bounds (e.g. clamp to [0.25, 4]) — the spec only requires finite and > 0.
