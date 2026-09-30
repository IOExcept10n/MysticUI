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
- `MonoGame Sample.csproj` targets `net8.0-windows` and has a custom `RestoreDotnetTools` MSBuild target that runs `dotnet tool restore` (installs `dotnet-mgcb` via `sources/MonoGame Sample/.config/dotnet-tools.json`) before `Restore` — building it standalone may take longer on first run.
- `IcyUI.FNA` only contains the default SDK-template `Class1.cs` — it builds but has no real implementation; don't expect it to exercise any FNA-specific behavior.
- Style is enforced via StyleCop.Analyzers + `EnforceCodeStyleInBuild=True`, but only wired into the `IcyUI` and `IcyUI.MonoGame` project files — building `IcyUI.Stride`, `IcyUI.FNA`, `IcyUI.Tests`, or `MonoGame Sample` alone will not surface style warnings even though `sources/.editorconfig` applies solution-wide.
