---
name: engine-integration
description: Reference for IcyUI's core-library-plus-engine-integration-project layout (IcyUI.MonoGame, IcyUI.FNA, IcyUI.Stride). Use when editing any of those three projects, or when a change to core IcyUI could affect them, to reason about cross-engine implications before making the change.
---

IcyUI is a game-UI framework split into a core library plus per-engine integration projects, all referencing `sources/IcyUI/IcyUI.csproj` (RootNamespace `Icy`):

| Project | Status | Notes |
|---|---|---|
| `IcyUI.MonoGame` | Primary, actively developed | RootNamespace `Icy.MonoGame`. References `MonoGame.Framework.DesktopGL`. Also depended on by `MonoGame Sample`. |
| `IcyUI.Stride` | Real implementation | Rendering, input, assets. References `Stride.Core`/`Stride.Engine` 4.3+ (first version with `win-arm64` natives). Inside `namespace Icy.Stride.*`, write `global::Stride.…` in crefs/qualified names — `Stride.X` resolves to `Icy.Stride.X` first. |
| `IcyUI.FNA` | Unimplemented stub | Only contains the default SDK-template `Class1.cs` — wired into the solution but has no real code yet. Don't assume it mirrors MonoGame/Stride behavior. |

## When editing engine-integration code

- Changes to the core `IcyUI` library that alter public APIs, rendering abstractions, or input handling can affect all three integrations differently — check whether `IcyUI.MonoGame` and `IcyUI.Stride` both need corresponding updates, since only those two have real implementations to break.
- Don't assume a fix or feature validated only in `IcyUI.MonoGame` also applies to `IcyUI.Stride` — the two engines have different rendering/input models (e.g. Stride draws from a `SceneRendererBase` in the compositor with a frame-scoped `GraphicsContext`, while MonoGame draws from `Game.Draw()`).
- `IcyUI.FNA` being a stub means it currently can't regress — but if asked to "implement FNA support," treat it as greenfield work following the `IcyUI.MonoGame` integration as the closest reference pattern.
- The repo is mid-migration toward being engine/platform-independent (see the `platform-independent` branch, e.g. removal of `SharpDX.Direct3D9` from the sample). Prefer changes that keep engine-specific code isolated to the relevant `IcyUI.<Engine>` project rather than leaking engine types into core `IcyUI`.
