---
name: build-test
description: Build or run tests for the IcyUI solution. Use when the user asks to build the project, run tests, or verify changes compile — there is no CI or build script in this repo, so these are the canonical commands.
---

IcyUI has no CI config and no build scripts — use plain `dotnet` commands against `sources/IcyUI.sln`.

## Build the whole solution

```
dotnet build "sources/IcyUI.sln"
```

## Run all tests

```
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"
```

Run a single test with the standard xUnit filter, e.g.:

```
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~UIElementTests"
```

## Gotchas

- `sources/MonoGame Sample/` has a space in its directory and project name — always quote the path when referencing `MonoGame Sample.csproj` on the command line.
- Everything targets `net10.0` (sample hosts: `net10.0-windows`) through `sources/Directory.Build.props`; the SDK is pinned by the root `global.json`. To try another TFM without editing files, pass `-p:IcyTargetFramework=net8.0` (useful for bisecting runtime-behavior changes).
- Package versions live only in `sources/Directory.Packages.props` (Central Package Management) — a `Version=` attribute on a `PackageReference` is a build error.
- `MonoGame Sample.csproj` has a custom `RestoreDotnetTools` MSBuild target that runs `dotnet tool restore` (installs `dotnet-mgcb` via `sources/MonoGame Sample/.config/dotnet-tools.json`) before `Restore` — building it standalone may take longer on first run. Keep those tool versions equal to the MonoGame package version.
- `IcyUI.FNA` only contains the default SDK-template `Class1.cs` — it builds but has no real implementation; don't expect it to exercise any FNA-specific behavior.
- StyleCop.Analyzers runs only in the shipped libraries (`IcyUI`, `IcyUI.MonoGame`, `IcyUI.Stride`) — building tests or samples alone won't surface style warnings.
- Warnings are not replayed for up-to-date projects; use `--no-incremental` when counting warnings.
- On Windows-on-ARM64, a successful build doesn't prove the samples run: engine natives are resolved from `runtimes/win-arm64/native` at runtime only.
