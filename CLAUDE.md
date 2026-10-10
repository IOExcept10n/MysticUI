# CLAUDE.md

IcyUI is a cross-engine game UI library: an engine-agnostic core plus thin per-engine integration projects. It is a solo pet project by Ivan Olyanishin (MIT), still pre-MVP.

## Branches

- `platform-independent` is the long-lived development branch for the new iteration. Commit routine work directly to it. It gets merged into `main` (replacing the old iteration) at the first stable MVP.
- `main` still holds the old pre-rewrite code. Don't base new work on it.

## Layout (`sources/`)

| Project | Status | Notes |
|---|---|---|
| `IcyUI` | Core | RootNamespace `Icy`. Controls, layout, markup, styles/templates, binding, animations, input routing, rendering abstractions. Must not reference any engine types. |
| `IcyUI.Design` | Dev-time tooling | Markup design document, edit engine, editor frame, panels, overlay and workspace (Phase 10). References only `IcyUI`; games never need it at runtime. |
| `IcyUI.MonoGame` | Primary integration | Backend-neutral: compiles against DesktopGL privately; hosts reference DesktopGL or WindowsDX 3.8.5.1+. |
| `IcyUI.Stride` | Real integration | References `Stride.Core`/`Stride.Engine`. |
| `IcyUI.FNA` | Stub | Only the SDK template `Class1.cs`. Treat FNA work as greenfield, modelled on `IcyUI.MonoGame`. |
| `IcyUI.Tests` | xUnit | Links some `Shared Samples` demos so their markup is exercised by tests. |
| `Shared Samples` | Demo source | `*Demo.cs` files linked into both sample hosts. |
| `MonoGame Sample`, `Stride Sample` | Sample hosts | Every new demo is registered in **both** hosts' `SampleGame.cs`. `MonoGame Sample` runs on WindowsDX by default; `-p:MonoGameBackend=DesktopGL` switches it (each backend has its own `bin/<backend>`, `obj/<backend>`). |

The built-in theme lives in `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (embedded resource). New controls get an entry there.

## Build & test

Everything targets `net10.0` (the sample hosts use `net10.0-windows`). The SDK is pinned via the root `global.json` (10.0.x, `latestFeature`). Shared properties live in `sources/Directory.Build.props` (`IcyTargetFramework`, `Nullable`, `ImplicitUsings`), and **all package versions** in `sources/Directory.Packages.props` (Central Package Management). Never put `Version=` on a `PackageReference`.

CI (`.github/workflows/ci.yml`) builds and tests on x64 and ARM64 Windows, and packs on x64, for every push to `main`/`platform-independent` and every PR. The only scripts are the two checks in `.github/scripts/` (test results, release tag). Locally, use plain `dotnet` commands:

```
dotnet build "sources/IcyUI.sln"
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"
```

- `MonoGame Sample` has a space in its path, so always quote it. It restores `dotnet-mgcb` via a local tool manifest on first build.
- StyleCop is wired into the shipped libraries (`IcyUI`, `IcyUI.Design`, `IcyUI.MonoGame`, `IcyUI.Stride`), not into tests/samples. They build with **zero** warnings: each sets `IcyShippedLibrary`, and `sources/Directory.Build.targets` makes warnings errors in Release (NuGet audit warnings excepted). Keep Debug at zero too.
- `MonoGame Sample` opts out of `ImplicitUsings`/`Nullable`: with `UseWindowsForms`, implicit usings pull in `System.Windows.Forms`/`System.Drawing`, which clash with Icy/XNA type names.
- The MonoGame packages and the `mgcb` tools in `MonoGame Sample/.config/dotnet-tools.json` must stay on the same version.
- Development happens on both x64 and Windows-on-ARM64 machines. Engine package versions must provide `win-arm64` natives (Stride >= 4.3; MonoGame DesktopGL/WindowsDX 3.8.5 do). Missing natives fail only at runtime, not at build time.

### Release

The four shipped libraries are packages; everything else is `IsPackable=false` (`sources/Directory.Build.targets`). The version lives there too: one `<Version>` for all four, `0.1.0-alpha.N`. nuget.org never accepts the same version twice.

Releases go through `.github/workflows/release.yml` with nuget.org trusted publishing: no API key exists anywhere.

1. Bump `<Version>` in `sources/Directory.Build.targets`, commit, push, and wait for green CI.
2. `git tag v<version>` and `git push origin v<version>`.
3. The workflow's build job checks that the tag matches `<Version>` and that the commit is on `origin/main` or `origin/platform-independent`, then builds, tests and packs. Once it's green, approve the `nuget` deployment in the Actions run: the publish job pushes the packages (`.snupkg` symbols included), and a last job creates a GitHub Release with the packages attached.

A manual run of `release.yml` is a dry run, even when started from a tag: it builds, tests and packs, and never publishes. To try packages locally:

```
rm -rf artifacts
dotnet pack "sources/IcyUI.sln" -c Release -o artifacts
```

Release builds set `ContinuousIntegrationBuild` (`sources/Directory.Build.props`), so PDB paths are `/_/...` and SourceLink maps them to the commit on GitHub. `PackageProjectUrl` and the package READMEs link to the `platform-independent` tree; switch them to the repository root when it merges into `main`.

#### One-time publishing setup (Ivan)

1. **nuget.org → your profile → Trusted Publishing → new policy:**
   - repository owner `IOExcept10n`, repository `IcyUI`;
   - workflow file `release.yml`, environment `nuget`;
   - scopes: push new packages and push new versions, package glob `IcyUI*`.
2. **GitHub → IcyUI → Settings → Environments → New environment `nuget`:**
   - required reviewer: yourself;
   - deployment branches and tags: "Selected branches and tags", add the tag rule `v*`;
   - environment secret `NUGET_USER`: your nuget.org profile name (not your email).
3. **First release:**
   1. Push `platform-independent` and check that CI is green on both runners.
   2. Run `release.yml` manually from `platform-independent` (the dry run) and check that its `packages` artifact holds 4 `.nupkg` and 4 `.snupkg` files.
   3. Push the tag `v0.1.0-alpha.1` and approve the deployment.

## Conventions

- **Every public API gets complete XML documentation.** The DocFX site (`docfx/`) is generated from it. Use `<see cref>`/`<see langword>`, `<list>`, `<para>`, etc.
- Keep engine-specific code inside its `IcyUI.<Engine>` project. Core changes to rendering, input or public API must be checked against both MonoGame and Stride.
- Match surrounding code style: StyleCop rules, block-scoped `namespace X { }` declarations, file copyright header, existing naming.
- Layout limits (`Width`, `MinWidth`, `MaxWidth`, ...) use `float.NaN` as "unset". Don't pass them to `float.Clamp`/`Math.Min`/`Math.Max` directly: since .NET 9, `float.Clamp` propagates `NaN` bounds. Use `UIElement.ClampToLimits`-style handling.

## Process

1. Every feature or phase starts with a design discussion. Ivan prefers heavy brainstorming before any code.
2. Commit a spec: `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.
3. Commit a plan: `docs/superpowers/plans/YYYY-MM-DD-<topic>-plan.md`.
4. Implement, then do a whole-branch review before calling a phase done.
5. Ivan runs manual smoke tests of the sample apps himself. Never claim a smoke test that didn't happen.
6. Pooled item containers (`ItemsControl` descendants) are a recurring source of stale-content bugs. Test re-targeting and re-binding behaviorally.

## Known issues

- **High priority, scheduled for the graphics-API discussion right after Tier-2:** on Windows-on-ARM64, MonoGame **DesktopGL** deadlocks on shutdown. `Game.Dispose()` → `SdlGameWindow.Dispose` never returns, because OpenGL there runs through Microsoft's `OpenGLOn12` layer (Snapdragon has no native GL driver). The window closes but the process stays alive. Stride (D3D11) is unaffected. WindowsDX has no SDL layer; whether the WindowsDX sample exits cleanly on WoA is for Ivan's smoke test.
- UI scaling (`Canvas.EffectiveScale`, `IcyConfiguration.Scaling`) follows the host's DPI awareness: a host without a PerMonitorV2 `app.manifest` reports `DisplayScale = 1` and is bitmap-stretched by Windows. Both sample hosts ship the manifest.
- **No touch on MonoGame desktop; decide during the MonoGame backend-consistency pass:**
  - The core gesture recognizer works, but MonoGame DesktopGL never fills `TouchPanel`. Its SDL layer declares the finger event types but never reads finger data.
  - SDL2 itself tracks fingers, so polling `SDL_GetTouchFinger` from the connector is a known way out (same `SDL2.dll` as `MonoGameNativeWindow`).
  - `IcyUI.MonoGame` is backend-neutral and `MonoGame Sample` defaults to WindowsDX (`-p:MonoGameBackend=DesktopGL` for the other). The touch gap is DesktopGL's; WindowsDX touch is unverified.
  - An SDL touch fix would apply to DesktopGL hosts only.

## Naming history

The project was renamed MysticUI → AquaUI → IcyUI. The GitHub repository was renamed to `IOExcept10n/IcyUI` on 2026-10-10; only the local folder is still named `MysticUI`. Remaining old-name references outside historical docs are bugs, so flag them.
