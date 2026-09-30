# CLAUDE.md

IcyUI is a cross-engine game UI library: an engine-agnostic core plus thin per-engine integration projects. It is a solo pet project by Ivan Olyanishin (MIT), still pre-MVP.

## Branches

- `platform-independent` is the long-lived development branch for the new iteration. Commit routine work directly to it. It gets merged into `main` (replacing the old iteration) at the first stable MVP.
- `main` still holds the old pre-rewrite code. Don't base new work on it.

## Layout (`sources/`)

| Project | Status | Notes |
|---|---|---|
| `IcyUI` | Core | RootNamespace `Icy`. Controls, layout, markup, styles/templates, binding, animations, input routing, rendering abstractions. Must not reference any engine types. |
| `IcyUI.MonoGame` | Primary integration | References `MonoGame.Framework.DesktopGL`. |
| `IcyUI.Stride` | Real integration | References `Stride.Core`/`Stride.Engine`. |
| `IcyUI.FNA` | Stub | Only the SDK template `Class1.cs`. Treat FNA work as greenfield, modelled on `IcyUI.MonoGame`. |
| `IcyUI.Tests` | xUnit | Links some `Shared Samples` demos so their markup is exercised by tests. |
| `Shared Samples` | Demo source | `*Demo.cs` files linked into both sample hosts. |
| `MonoGame Sample`, `Stride Sample` | Sample hosts | Every new demo is registered in **both** hosts' `SampleGame.cs`. |

The built-in theme lives in `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (embedded resource). New controls get an entry there.

## Build & test

No CI and no build scripts. Use plain `dotnet` commands:

```
dotnet build "sources/IcyUI.sln"
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"
```

- `MonoGame Sample` has a space in its path, so always quote it. It restores `dotnet-mgcb` via a local tool manifest on first build.
- StyleCop and `EnforceCodeStyleInBuild` are wired only into `IcyUI` and `IcyUI.MonoGame`.
- Development happens on both x64 and Windows-on-ARM64 machines. Engine package versions must provide `win-arm64` natives.

## Conventions

- **Every public API gets complete XML documentation.** The DocFX site (`docfx/`) is generated from it. Use `<see cref>`/`<see langword>`, `<list>`, `<para>`, etc.
- Keep engine-specific code inside its `IcyUI.<Engine>` project. Core changes to rendering, input or public API must be checked against both MonoGame and Stride.
- Match surrounding code style: StyleCop rules, file-scoped types, existing naming.

## Process

1. Every feature or phase starts with a design discussion. Ivan prefers heavy brainstorming before any code.
2. Commit a spec: `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.
3. Commit a plan: `docs/superpowers/plans/YYYY-MM-DD-<topic>-plan.md`.
4. Implement, then do a whole-branch review before calling a phase done.
5. Ivan runs manual smoke tests of the sample apps himself. Never claim a smoke test that didn't happen.
6. Pooled item containers (`ItemsControl` descendants) are a recurring source of stale-content bugs. Test re-targeting and re-binding behaviorally.

## Naming history

The project was renamed MysticUI → AquaUI → IcyUI. The GitHub repo and local folder are still named `MysticUI`. Remaining old-name references outside historical docs are bugs, so flag them.
