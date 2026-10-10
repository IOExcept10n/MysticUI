# NuGet packaging for 0.1.0-alpha.1, with a backend-neutral IcyUI.MonoGame

> Design spec, discussed with Ivan on 2026-10-10. First remaining sub-project of the release-prep list (StyleCop to zero
> and the review are done; the DocFX site follows). Ivan pushes the packages to nuget.org himself.

## Context

The four shipped libraries (`IcyUI`, `IcyUI.Design`, `IcyUI.MonoGame`, `IcyUI.Stride`) build at zero warnings, but none
of them is ready to pack:

- **No package metadata.** No `Version`, `Authors`, `Description`, readme, repository URL or tags; everything would pack
  as `1.0.0` named after the assembly.
- **Inconsistent licensing.** Only `IcyUI` and `IcyUI.Design` pack the `LICENSE` file, and both set
  `PackageRequireLicenseAcceptance=True`, which shows a license dialog on every install. The engine packages pack no
  license at all.
- **No symbols or SourceLink.** The Stride game, the first consumer, couldn't step into IcyUI sources.
- **The MonoGame backend leaks.** `IcyUI.MonoGame` has a plain `PackageReference` to `MonoGame.Framework.DesktopGL`, so
  the package would force DesktopGL on every consumer. The same reference flows into `MonoGame Sample`, which references
  `MonoGame.Framework.WindowsDX`: both backends ship an assembly named `MonoGame.Framework`, MSBuild silently keeps one,
  and the sample has been running on DesktopGL while its content is built for `/platform:Windows`.
- **Samples are packable.** They're exes, which `dotnet pack` packs by default.

All four package IDs are free on nuget.org (checked 2026-10-10).

The library code is already backend-neutral: of `IcyUI.MonoGame`'s ~1,900 lines, only `MonoGameNativeWindow` is
backend-aware, and it already branches at runtime (`SdlGameWindow` → `SDL_GetWindowWMInfo`; otherwise
`GameWindow.Handle` is the `HWND`). Everything else uses the XNA API both backends share.

## Decisions

| Question | Decision |
|---|---|
| How `IcyUI.MonoGame` ships | Backend-neutral: compiled against DesktopGL with `PrivateAssets="all"`, no MonoGame dependency in the package. The host references `MonoGame.Framework.DesktopGL` or `.WindowsDX` 3.8.5+. |
| How both backends stay exercised | One `MonoGame Sample` with a `MonoGameBackend` MSBuild property, default `WindowsDX`, `-p:MonoGameBackend=DesktopGL` for the other. |
| Version | `0.1.0-alpha.1`, one shared `<Version>` for all four packages. Fixes ship as `alpha.2`, `alpha.3`, ... |
| License | `PackageLicenseExpression=MIT`; no packed `LICENSE`, no license acceptance. |
| Readme | One `README.md` per package, in the project folder. |
| Symbols | `.snupkg` symbol packages plus SourceLink. |
| Icon | None for the alpha. |
| Verification | SDK package validation, plus a one-time throwaway consumer project during implementation. Nothing new is kept in the repo for it. |

## Design

### Shared metadata: `sources/Directory.Build.targets`

`IcyShippedLibrary` is set inside each `.csproj`, and `Directory.Build.props` is imported *before* the project body, so
a condition on it there is always false. The pack settings therefore live in `Directory.Build.targets`, next to the
existing warnings-as-errors rule:

- **For every project:** `IsPackable=false` unless the project is a shipped library. Test projects already default to
  non-packable; this covers the sample exes.
- **For `IcyShippedLibrary=true`:**
  - `IsPackable=true`, `Version=0.1.0-alpha.1`;
  - `Authors=Ivan Olyanishin`, `Copyright=Copyright (c) Ivan Olyanishin`;
  - `PackageLicenseExpression=MIT`;
  - `PackageProjectUrl` and `RepositoryUrl` = the GitHub repository (still named `MysticUI`; that is the real URL, not
    a naming leftover), `RepositoryType=git`;
  - `PackageReadmeFile=README.md`, with an item packing the project's own `README.md` at the package root;
  - `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`, `PublishRepositoryUrl=true`, `EmbedUntrackedSources=true`;
  - `EnablePackageValidation=true`;
  - `ContinuousIntegrationBuild=true` in Release, for deterministic paths in packed PDBs.

The targets file is evaluated after the project body, so its values win over the project's. That's intended for the
shared values above. Per-package values (`Description`, `PackageTags`) are set only in each `.csproj`, and the targets
file never touches them.

**SourceLink.** The .NET 8+ SDK includes SourceLink for GitHub. Verify that a packed PDB contains the GitHub source map
without any package. Add `Microsoft.SourceLink.GitHub` (version in `Directory.Packages.props`, `PrivateAssets=all`) only
if it doesn't.

### Per-project changes

| Project | Changes |
|---|---|
| `IcyUI` | `Description`, `PackageTags`, `README.md`. Remove the `LICENSE` `<None Pack>` item and `PackageLicenseFile`/`PackageRequireLicenseAcceptance`. |
| `IcyUI.Design` | Same as `IcyUI`. Its description and readme say it's dev-time tooling a shipped game doesn't need. |
| `IcyUI.MonoGame` | `Description`, `PackageTags`, `README.md`. `MonoGame.Framework.DesktopGL` gets `PrivateAssets="all"`. |
| `IcyUI.Stride` | `Description`, `PackageTags`, `README.md`. `RootNamespace=Icy.Stride`, matching every namespace in the project. |

Readmes are short:
- what the package is;
- `dotnet add package <id> --prerelease`;
- a minimal setup snippet;
- a link to the repository (the docs site link is added once the site exists).

The `IcyUI.MonoGame` readme states the backend requirement and lists the known DesktopGL issues: the Windows-on-ARM64
shutdown deadlock and no touch input.

The `IcyUI.MonoGame` package can't enforce a MonoGame version floor once the dependency is private. The readme and the
XML docs of the package's entry point (the MonoGame builder extension) state "3.8.5 or later".

### MonoGame backend switch: `MonoGame Sample`

- `<MonoGameBackend Condition="'$(MonoGameBackend)' == ''">WindowsDX</MonoGameBackend>`.
- `<PackageReference Include="MonoGame.Framework.$(MonoGameBackend)" />`. Both IDs are already versioned in
  `Directory.Packages.props`.
- The content platform follows the backend: `Windows` for WindowsDX, `DesktopGL` for DesktopGL. Either pass the
  platform from MSBuild to `MonoGame.Content.Builder.Task`, or keep one `.mgcb` per backend and pick it by condition.
  Choose whichever the 3.8.5 task supports cleanly, preferring the single `.mgcb`.
- Any other backend value fails the build with a clear `<Error>`.
- Because `IcyUI.MonoGame`'s DesktopGL reference no longer flows, the default sample build contains only the WindowsDX
  `MonoGame.Framework.dll`. Verify this in the build output.

### Release flow

`CLAUDE.md` gets a "Release" block:

```
dotnet pack "sources/IcyUI.sln" -c Release -o artifacts
dotnet nuget push "artifacts/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key <key>
```

The `.snupkg` files next to the `.nupkg` files are uploaded by the same push. `artifacts/` is already in `.gitignore`.
Release packing inherits the zero-warning rule, so a warning fails the pack.

`CLAUDE.md` also changes:
- The "mixed backends" bullet under Known issues is rewritten: the sample defaults to WindowsDX, and DesktopGL is
  selectable.
- The WoA deadlock entry notes that it is DesktopGL-only.
- The `IcyUI.MonoGame` row in the Layout table becomes "backend-neutral; host picks DesktopGL or WindowsDX".

### Error handling

- A host that references `IcyUI.MonoGame` without any MonoGame backend fails to compile, because the MonoGame types are
  missing. That error is clear enough, and the readme covers it.
- A host on MonoGame older than 3.8.5 may fail at runtime with `MissingMethodException`. This is documented, not
  detected.
- `MonoGameNativeWindow` keeps its existing fallback: any failure yields `Handle = 0` and core uses the system DPI.

## Testing

- **Existing suite:** `dotnet test` stays green; check the Total, not only "Passed!". `dotnet build -c Release` of the
  solution stays at zero warnings.
- **Both sample backends build:** the default (WindowsDX) and `-p:MonoGameBackend=DesktopGL`. Each output folder holds
  exactly one `MonoGame.Framework.dll`, from the matching backend package.
- **Pack:** `dotnet pack -c Release -o artifacts` produces exactly four `.nupkg` and four `.snupkg` files, and nothing
  for tests or samples. Package validation passes.
- **Package contents:** each `.nupkg` holds the DLL, the XML documentation file, `README.md`, and a `.nuspec` with
  version `0.1.0-alpha.1` and license expression `MIT`. Dependencies:
  - `IcyUI` → CommunityToolkit packages only;
  - `IcyUI.Design` → `IcyUI`;
  - `IcyUI.MonoGame` → `IcyUI` only, with no MonoGame dependency;
  - `IcyUI.Stride` → `IcyUI`, `Stride.Core`, `Stride.Engine`.

  StyleCop must not appear as a dependency.
- **Throwaway consumer** (in the job's temp folder, not committed): a console project with a `nuget.config` pointing at
  `artifacts/` installs `IcyUI.Stride`, and separately `IcyUI.MonoGame` + `MonoGame.Framework.WindowsDX`. Each must
  restore and build against a type from the package.
- **Ivan's smoke tests:**
  - **MonoGame Sample on WindowsDX,** on both machines. On ARM64, also record whether the process now exits cleanly and
    whether touch works; WindowsDX has no SDL, but neither is claimed in advance.
  - **MonoGame Sample on DesktopGL,** on x64.
  - **Stride:** no change expected.

## Performance

No runtime cost to consumers: the changes are build and pack metadata. `ContinuousIntegrationBuild`, symbols and
SourceLink affect only the pack output and PDB contents. The MonoGame sample's default renderer changes from DesktopGL
(OpenGL, and on ARM64 OpenGLOn12) to WindowsDX (D3D11). This changes the sample's frame timings, not the library's.

## Out of scope

- Pushing to nuget.org (Ivan), and ID-prefix reservation.
- A package icon.
- Per-backend MonoGame packages; an FNA package (still a stub).
- The DocFX site, which is the next sub-project. Its getting-started pages will use these package IDs.
- Tagging `v0.1.0-alpha.1` and merging `platform-independent` into `main`. These happen at release time, after the docs.
- Package validation against a baseline version, which needs a published `0.1.0-alpha.1` first.
