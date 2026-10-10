# NuGet Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the four shipped libraries pack as `0.1.0-alpha.1` NuGet packages with symbols, and make `IcyUI.MonoGame` backend-neutral, with a `MonoGameBackend` switch in the MonoGame sample.

**Architecture:** Shared pack metadata goes in `sources/Directory.Build.targets`, gated on `IcyShippedLibrary`. Per-package
values (`Description`, `PackageTags`, `README.md`) stay in each project. `IcyUI.MonoGame` keeps compiling against DesktopGL but marks
it `PrivateAssets="all"`. The sample picks `MonoGame.Framework.$(MonoGameBackend)`, whose own build targets set `MonoGamePlatform`.
That property is what the content builder passes as `/platform`, so the `.mgcb` file needs no change.

**Tech Stack:** .NET 10 SDK (10.0.401), MSBuild Central Package Management, NuGet pack/validation, MonoGame 3.8.5.1, Stride 4.3.0.2507.

**Spec:** `docs/superpowers/specs/2026-10-10-nuget-packaging-design.md`

## Global Constraints

- Version `0.1.0-alpha.1`, one shared `<Version>` for all four packages.
- Package IDs: `IcyUI`, `IcyUI.Design`, `IcyUI.MonoGame`, `IcyUI.Stride` (the assembly names; no `PackageId` override needed).
- `PackageLicenseExpression=MIT`; no packed `LICENSE` file, no `PackageRequireLicenseAcceptance`.
- `Authors=Ivan Olyanishin`. Repository URL `https://github.com/IOExcept10n/MysticUI` (the real repo name, not a naming bug).
- `IcyUI.MonoGame` package: **no** MonoGame dependency; hosts need `MonoGame.Framework.DesktopGL` or `.WindowsDX` **3.8.5 or later**.
- `MonoGame Sample` default backend `WindowsDX`; the alternative is `-p:MonoGameBackend=DesktopGL`. Any other value is a build error.
- Never put `Version=` on a `PackageReference` (Central Package Management).
- Shipped libraries stay at **zero** warnings in Debug and Release. Release turns warnings into errors, so `dotnet pack -c Release` fails on any warning.
- No new projects kept in the repo for verification. Throwaway consumer projects live in `$CLAUDE_JOB_DIR/tmp` (or another temp dir outside the repo).
- `dotnet test` prints "Passed!" even when the test host crashes. Always read the `Total:` count.
- Never claim Ivan's smoke tests. List them for him instead.

## Review Focus

1. **An unrelated sample or test becomes packable, or a shipped one is silently skipped.** Expect exactly 4 `.nupkg` + 4 `.snupkg` files. Pinned in Task 1 Step 5.
2. **The DesktopGL dependency leaks back through some other path.** A `ProjectReference` or `build` assets could make the WindowsDX sample end up with the DesktopGL `MonoGame.Framework.dll` again. Expect one DLL whose hash matches the WindowsDX package. Pinned in Task 2 Step 4.
3. **`MonoGamePlatform` doesn't follow the backend**, so DesktopGL builds content for `/platform:Windows`. Expect the content output under `Content/bin/DesktopGL/`. Pinned in Task 2 Step 5.
4. **StyleCop leaks into the package dependencies.** It must stay `PrivateAssets=all`, and no `StyleCop.Analyzers` should appear in any `.nuspec`. Pinned in Task 1 Step 5.
5. **A consumer gets no IntelliSense docs or source stepping:** the XML doc file or SourceLink is missing from the package. Pinned in Task 1 Step 5 (XML in `.nupkg`) and Task 1 Step 6 (SourceLink in PDB).

---

### Task 1: Shared pack metadata, per-package readmes, and license cleanup

**Files:**
- Modify: `sources/Directory.Build.targets`
- Modify: `sources/IcyUI/IcyUI.csproj`
- Modify: `sources/IcyUI.Design/IcyUI.Design.csproj`
- Modify: `sources/IcyUI.MonoGame/IcyUI.MonoGame.csproj` (description/tags/readme only; the backend change is Task 2)
- Modify: `sources/IcyUI.Stride/IcyUI.Stride.csproj`
- Create: `sources/IcyUI/README.md`, `sources/IcyUI.Design/README.md`, `sources/IcyUI.MonoGame/README.md`, `sources/IcyUI.Stride/README.md`
- Possibly modify: `sources/Directory.Packages.props` (only if Step 6 shows SourceLink needs the package)

**Interfaces:**
- Consumes: the existing `IcyShippedLibrary=true` property in each shipped `.csproj`.
- Produces: `dotnet pack "sources/IcyUI.sln" -c Release -o artifacts` → 4 `.nupkg` + 4 `.snupkg`. Task 2 and Task 3 rely on this command and on the `artifacts/` folder (already git-ignored).

- [ ] **Step 1: Record the baseline (expected: wrong).**

Run from the repo root:
```bash
rm -rf artifacts && dotnet pack "sources/IcyUI.sln" -c Release -o artifacts 2>&1 | tail -5; ls artifacts
```
Expected: packages named `*.1.0.0.nupkg`, including sample/FNA packages, no `.snupkg`. This is the failing "test".

- [ ] **Step 2: Add the shared pack metadata to `sources/Directory.Build.targets`.**

Replace the whole file with:
```xml
<Project>
  <!-- The shipped libraries (IcyShippedLibrary=true) build without warnings. Release builds, which produce the packages,
       turn any new warning into an error; Debug builds keep them as warnings so work in progress still compiles.
       NuGet's audit warnings stay warnings: a feed that's unreachable off-network mustn't fail a release build. -->
  <PropertyGroup Condition="'$(IcyShippedLibrary)' == 'true' and '$(Configuration)' == 'Release'">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);NU1900;NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
  </PropertyGroup>

  <!-- Only the shipped libraries are packages. This lives here, not in Directory.Build.props, because projects set
       IcyShippedLibrary in their own body, which MSBuild evaluates after the props file and before this one.
       Per-package values (Description, PackageTags) stay in each project; the shared ones below win on purpose. -->
  <PropertyGroup Condition="'$(IcyShippedLibrary)' != 'true'">
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <PropertyGroup Condition="'$(IcyShippedLibrary)' == 'true'">
    <IsPackable>true</IsPackable>
    <Version>0.1.0-alpha.1</Version>
    <Authors>Ivan Olyanishin</Authors>
    <Copyright>Copyright (c) Ivan Olyanishin</Copyright>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/IOExcept10n/MysticUI</PackageProjectUrl>
    <RepositoryUrl>https://github.com/IOExcept10n/MysticUI</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <EnablePackageValidation>true</EnablePackageValidation>
  </PropertyGroup>
  <PropertyGroup Condition="'$(IcyShippedLibrary)' == 'true' and '$(Configuration)' == 'Release'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>
  <ItemGroup Condition="'$(IcyShippedLibrary)' == 'true'">
    <None Include="$(MSBuildProjectDirectory)\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

`ContinuousIntegrationBuild` is a property SourceLink and the compiler read at build time. Setting it in `.targets` works
because those consumers evaluate it inside targets, not during property evaluation. Step 6 checks this.

- [ ] **Step 3: Update the four project files.**

`sources/IcyUI/IcyUI.csproj`: in the first `PropertyGroup`, remove `<PackageLicenseFile>LICENSE</PackageLicenseFile>` and
`<PackageRequireLicenseAcceptance>True</PackageRequireLicenseAcceptance>` and add:
```xml
    <Description>Engine-agnostic game UI library: controls, layout, XML markup, styles and templates, data binding, animations and input routing. Use it through an engine package such as IcyUI.Stride or IcyUI.MonoGame.</Description>
    <PackageTags>ui;gui;game;gamedev;controls;markup;binding;stride;monogame</PackageTags>
```
Delete the whole `ItemGroup` that packs `..\..\LICENSE`:
```xml
  <ItemGroup>
    <None Include="..\..\LICENSE">
      <Pack>True</Pack>
      <PackagePath>\</PackagePath>
    </None>
  </ItemGroup>
```

`sources/IcyUI.Design/IcyUI.Design.csproj`: the same removals (both properties and the `LICENSE` `ItemGroup`), and add:
```xml
    <Description>Development-time tooling for IcyUI: a markup design document with an edit engine and hot reload, an in-game editor frame, and Outline/Properties/Toolbox panels. Shipped games don't need it at runtime.</Description>
    <PackageTags>ui;gui;game;gamedev;editor;designer;hot-reload;icyui</PackageTags>
```

`sources/IcyUI.MonoGame/IcyUI.MonoGame.csproj`: add to the first `PropertyGroup`:
```xml
    <Description>MonoGame integration for IcyUI: rendering, input and asset loading. Backend-neutral: reference MonoGame.Framework.DesktopGL or MonoGame.Framework.WindowsDX 3.8.5 or later yourself.</Description>
    <PackageTags>ui;gui;game;gamedev;monogame;xna;icyui</PackageTags>
```

`sources/IcyUI.Stride/IcyUI.Stride.csproj`: add to the first `PropertyGroup`:
```xml
    <RootNamespace>Icy.Stride</RootNamespace>
    <Description>Stride integration for IcyUI: a scene renderer overlay, input and asset loading.</Description>
    <PackageTags>ui;gui;game;gamedev;stride;icyui</PackageTags>
```

- [ ] **Step 4: Write the four readmes.**

`sources/IcyUI/README.md`:
````markdown
# IcyUI

Engine-agnostic game UI library for .NET: controls, layout, XML markup, styles and templates, data binding,
animations and input routing.

This package is the core. Install it through an engine integration:

```
dotnet add package IcyUI.Stride --prerelease
dotnet add package IcyUI.MonoGame --prerelease
```

**Status:** early alpha (0.1.0-alpha). APIs may change between alphas.

Source, samples and issues: https://github.com/IOExcept10n/MysticUI
````

`sources/IcyUI.Design/README.md`:
````markdown
# IcyUI.Design

Development-time tooling for [IcyUI](https://www.nuget.org/packages/IcyUI): a markup design document with an edit
engine and hot reload, an in-game editor frame, and Outline/Properties/Toolbox panels.

```
dotnet add package IcyUI.Design --prerelease
```

Use it in development builds or tools. A shipped game doesn't need it at runtime.

**Status:** early alpha (0.1.0-alpha). APIs may change between alphas.

Source, samples and issues: https://github.com/IOExcept10n/MysticUI
````

`sources/IcyUI.MonoGame/README.md`:
````markdown
# IcyUI.MonoGame

MonoGame integration for [IcyUI](https://www.nuget.org/packages/IcyUI).

```
dotnet add package IcyUI.MonoGame --prerelease
```

**Pick a MonoGame backend yourself.** This package doesn't depend on one. Reference `MonoGame.Framework.DesktopGL`
or `MonoGame.Framework.WindowsDX`, version **3.8.5 or later**.

```csharp
using Icy.Configuration;
using Icy.MonoGame.Configuration;
using Icy.UI;

// In Game.Initialize():
this.UseIcyUI();
var configuration = this.GetIcyConfiguration();
configuration.UseDefaultTheme();
var canvas = new Canvas(configuration) { IsInputEnabled = true };

// In Game.Draw(), after clearing:
canvas.Render();
```

**Known issues (DesktopGL):**
- On Windows-on-ARM64, `Game.Dispose()` can hang on shutdown (OpenGL runs through `OpenGLOn12`).
- No touch input: DesktopGL doesn't fill `TouchPanel`.

**Status:** early alpha (0.1.0-alpha). APIs may change between alphas.

Source, samples and issues: https://github.com/IOExcept10n/MysticUI
````

`sources/IcyUI.Stride/README.md`:
````markdown
# IcyUI.Stride

Stride integration for [IcyUI](https://www.nuget.org/packages/IcyUI). Requires Stride 4.3 or later.

```
dotnet add package IcyUI.Stride --prerelease
```

```csharp
using Icy.Configuration;
using Icy.Stride.Configuration;
using Icy.UI;

// In Game.BeginRun(), after the scene system is set up:
IcyUISceneRenderer overlay = this.UseIcyUI();
var configuration = this.GetIcyConfiguration();
configuration.UseDefaultTheme();
var canvas = new Canvas(configuration) { IsInputEnabled = true };
overlay.Canvases.Add(canvas);
```

**Status:** early alpha (0.1.0-alpha). APIs may change between alphas.

Source, samples and issues: https://github.com/IOExcept10n/MysticUI
````

Before committing, check each snippet's member names against `Stride Sample/SampleGame.cs` and `MonoGame Sample/SampleGame.cs`,
which use exactly these calls (the Stride sample sets up in `BeginRun()`, the MonoGame sample in `Initialize()`).

- [ ] **Step 5: Pack and inspect (the passing "test").**

```bash
rm -rf artifacts && dotnet pack "sources/IcyUI.sln" -c Release -o artifacts 2>&1 | grep -E "warn|error|Successfully" ; ls artifacts
for p in IcyUI IcyUI.Design IcyUI.MonoGame IcyUI.Stride; do
  echo "== $p"; unzip -l artifacts/$p.0.1.0-alpha.1.nupkg | grep -E "\.dll|\.xml|README"
  unzip -p artifacts/$p.0.1.0-alpha.1.nupkg $p.nuspec | grep -E "<version>|<license|<dependency |<readme|<repository"
done
unzip -p artifacts/*.nupkg '*.nuspec' | grep -c StyleCop
```
Expected:
- **Files:** exactly `IcyUI`, `IcyUI.Design`, `IcyUI.MonoGame`, `IcyUI.Stride` as `.0.1.0-alpha.1.nupkg` + `.snupkg`, and nothing else.
- **Contents:** each `.nupkg` lists `lib/net10.0/<id>.dll`, `lib/net10.0/<id>.xml` and `README.md`.
- **Metadata:** `<version>0.1.0-alpha.1</version>`, `<license type="expression">MIT</license>`, `<readme>README.md</readme>`, and a `<repository ... commit="...">`.
- **Dependencies:**
  - `IcyUI` → only `CommunityToolkit.*`;
  - `IcyUI.Design` → `IcyUI`;
  - `IcyUI.Stride` → `IcyUI`, `Stride.Core`, `Stride.Engine`;
  - `IcyUI.MonoGame` still → `IcyUI` + `MonoGame.Framework.DesktopGL` (Task 2 removes it).
- **StyleCop count:** `0`.

If the pack fails on a package-validation error, read it; don't disable validation. If the solution-level pack runs
into the Stride sample's asset build or the MonoGame sample's content build, pack the four projects one by one instead
(`dotnet pack sources/IcyUI.Stride/IcyUI.Stride.csproj -c Release -o artifacts`, and so on). Use that form in Task 3's
documentation instead of the `.sln` form.

- [ ] **Step 6: Check SourceLink without the package.**

```bash
unzip -o -q artifacts/IcyUI.0.1.0-alpha.1.snupkg -d "$CLAUDE_JOB_DIR/tmp/snupkg"
grep -a -o "https://raw.githubusercontent.com/IOExcept10n/MysticUI/[0-9a-f]*/\*" "$CLAUDE_JOB_DIR/tmp/snupkg/lib/net10.0/IcyUI.pdb" | head -1
grep -a -c "/_/" "$CLAUDE_JOB_DIR/tmp/snupkg/lib/net10.0/IcyUI.pdb"
```
Expected:
- the first command prints one `raw.githubusercontent.com/.../<commit>/*` URL, which is the SourceLink map;
- the second command prints a count > 0, meaning deterministic `/_/` paths, so `ContinuousIntegrationBuild` took effect.

The remote is SSH (`git@github.com:...`). The SDK's built-in SourceLink maps that to the https raw URL. If no URL
appears, add `<PackageVersion Include="Microsoft.SourceLink.GitHub" Version="8.0.0" />` to the `Core` group of
`sources/Directory.Packages.props`, and add it to the `ItemGroup` in `Directory.Build.targets` (conditioned on
`IcyShippedLibrary`) with `<PrivateAssets>all</PrivateAssets>`. Then re-run Steps 5–6. If the count of `/_/` is 0, move
the `ContinuousIntegrationBuild` group to `sources/Directory.Build.props`, conditioned on `'$(Configuration)' == 'Release'` only
(it's harmless for non-packed projects), and re-run.

- [ ] **Step 7: Build and test stay green.**

```bash
dotnet build "sources/IcyUI.sln" -c Debug 2>&1 | grep -E "Warn|Error" | tail -3
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" 2>&1 | tail -3
```
Expected: `0 Warning(s)` for the shipped libraries (sample/test warnings, if any, are pre-existing; compare with `git stash`
if unsure), `0 Error(s)`, and the test `Total:` equal to the pre-task count (1825 as of `225609b`, unless more were added since) with 0 failed.

- [ ] **Step 8: Commit.**

```bash
git add sources/Directory.Build.targets sources/IcyUI/IcyUI.csproj sources/IcyUI.Design/IcyUI.Design.csproj \
  sources/IcyUI.MonoGame/IcyUI.MonoGame.csproj sources/IcyUI.Stride/IcyUI.Stride.csproj \
  sources/IcyUI/README.md sources/IcyUI.Design/README.md sources/IcyUI.MonoGame/README.md sources/IcyUI.Stride/README.md
git add sources/Directory.Packages.props 2>/dev/null
git commit -m "Pack the shipped libraries as 0.1.0-alpha.1 with readmes, symbols and an MIT expression

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Backend-neutral IcyUI.MonoGame and the sample's backend switch

**Files:**
- Modify: `sources/IcyUI.MonoGame/IcyUI.MonoGame.csproj`
- Modify: `sources/IcyUI.MonoGame/Configuration/MonoGameBuildingExtensions.cs` (XML docs on the `MonoGameBuildingExtensions` class)
- Modify: `sources/MonoGame Sample/MonoGame Sample.csproj`

**Interfaces:**
- Consumes: Task 1's pack command and `artifacts/`.
- Produces: the `MonoGameBackend` MSBuild property (`WindowsDX` | `DesktopGL`) in `MonoGame Sample`, which Task 3 documents.

- [ ] **Step 1: Record the baseline.**

```bash
dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" 2>&1 | tail -2
OUT="sources/MonoGame Sample/bin/Debug/net10.0-windows"
sha256sum "$OUT/MonoGame.Framework.dll" ~/.nuget/packages/monogame.framework.windowsdx/3.8.5.1/lib/net*/MonoGame.Framework.dll ~/.nuget/packages/monogame.framework.desktopgl/3.8.5.1/lib/net*/MonoGame.Framework.dll
```
Expected (failing): the output DLL's hash matches the **DesktopGL** package, not WindowsDX. If the output path differs (e.g. a RID
subfolder), find it with `find "sources/MonoGame Sample/bin/Debug" -name MonoGame.Framework.dll`.

- [ ] **Step 2: Make the library's MonoGame reference private.**

In `sources/IcyUI.MonoGame/IcyUI.MonoGame.csproj` replace
```xml
    <PackageReference Include="MonoGame.Framework.DesktopGL" />
```
with
```xml
    <!-- Compile-time only: every MonoGame backend ships the same MonoGame.Framework API, and the host picks one
         (DesktopGL or WindowsDX, 3.8.5+). PrivateAssets keeps it out of the package and out of referencing projects. -->
    <PackageReference Include="MonoGame.Framework.DesktopGL" PrivateAssets="all" />
```

- [ ] **Step 3: Add the backend switch to the sample.**

In `sources/MonoGame Sample/MonoGame Sample.csproj`, add a new `PropertyGroup` after the second one:
```xml
  <PropertyGroup>
    <!-- The MonoGame backend this sample runs on: WindowsDX (D3D11, default) or DesktopGL (OpenGL/SDL).
         Build the other one with: dotnet build "MonoGame Sample" -p:MonoGameBackend=DesktopGL.
         The backend package's own targets set MonoGamePlatform, which the content builder passes as /platform. -->
    <MonoGameBackend Condition="'$(MonoGameBackend)' == ''">WindowsDX</MonoGameBackend>
  </PropertyGroup>
```
Replace `<PackageReference Include="MonoGame.Framework.WindowsDX" />` with
```xml
    <PackageReference Include="MonoGame.Framework.$(MonoGameBackend)" />
```
and add before `</Project>`:
```xml
  <Target Name="CheckMonoGameBackend" BeforeTargets="Restore;CollectPackageReferences">
    <Error Condition="'$(MonoGameBackend)' != 'WindowsDX' and '$(MonoGameBackend)' != 'DesktopGL'"
           Text="MonoGameBackend must be WindowsDX or DesktopGL, not '$(MonoGameBackend)'." />
  </Target>
```

Restore assets are per-project, not per-property, so switching backends needs a restore. A plain `dotnet build` restores
implicitly. Keep using it rather than `--no-restore` when switching.

- [ ] **Step 4: Verify WindowsDX (default).**

```bash
dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" 2>&1 | tail -2
find "sources/MonoGame Sample/bin/Debug" -name MonoGame.Framework.dll -exec sha256sum {} \;
sha256sum ~/.nuget/packages/monogame.framework.windowsdx/3.8.5.1/lib/net*/MonoGame.Framework.dll
find "sources/MonoGame Sample/bin/Debug" -iname "SDL2.dll"
ls "sources/MonoGame Sample/Content/bin"
```
Expected:
- the build succeeds;
- there's exactly one `MonoGame.Framework.dll`, with the **WindowsDX** hash;
- no `SDL2.dll` in the output;
- the content was built under `Content/bin/Windows`.

- [ ] **Step 5: Verify DesktopGL, then an invalid value.**

```bash
dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" -p:MonoGameBackend=DesktopGL 2>&1 | tail -2
find "sources/MonoGame Sample/bin/Debug" -name MonoGame.Framework.dll -exec sha256sum {} \;
sha256sum ~/.nuget/packages/monogame.framework.desktopgl/3.8.5.1/lib/net*/MonoGame.Framework.dll
ls "sources/MonoGame Sample/Content/bin"
dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" -p:MonoGameBackend=Vulkan 2>&1 | grep -m1 "MonoGameBackend must be"
```
Expected:
- the DesktopGL build succeeds, with the **DesktopGL** hash and content under `Content/bin/DesktopGL`;
- the `Vulkan` build prints the error message.

Then rebuild the default, so the working output is WindowsDX again:
`dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj"`.

If the DesktopGL build leaves stale WindowsDX files (or vice versa) because both backends share `bin/Debug`, add
`<BaseOutputPath>bin\$(MonoGameBackend)\</BaseOutputPath>` and `<BaseIntermediateOutputPath>obj\$(MonoGameBackend)\</BaseIntermediateOutputPath>`
to the sample. The intermediate path must go in a `Directory.Build.props`-time location, so put it in a new
`sources/MonoGame Sample/Directory.Build.props` that imports the parent:
```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
  <PropertyGroup>
    <MonoGameBackend Condition="'$(MonoGameBackend)' == ''">WindowsDX</MonoGameBackend>
    <BaseIntermediateOutputPath>obj\$(MonoGameBackend)\</BaseIntermediateOutputPath>
    <BaseOutputPath>bin\$(MonoGameBackend)\</BaseOutputPath>
  </PropertyGroup>
</Project>
```
In that case, move the default from the `.csproj` into that file, and repeat Steps 4–5 with the new output paths.

- [ ] **Step 6: Document the requirement in the library's XML docs.**

In `sources/IcyUI.MonoGame/Configuration/MonoGameBuildingExtensions.cs`, replace the class summary with:
```csharp
    /// <summary>
    /// Provides extension methods for configuring the IcyUI in MonoGame applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IcyUI.MonoGame doesn't depend on a MonoGame backend. The host references one of:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>MonoGame.Framework.DesktopGL</c> (OpenGL/SDL), 3.8.5 or later;</description></item>
    /// <item><description><c>MonoGame.Framework.WindowsDX</c> (Direct3D 11), 3.8.5 or later.</description></item>
    /// </list>
    /// <para>
    /// On DesktopGL, touch input isn't reported, and on Windows-on-ARM64 <see cref="Game.Dispose()"/> can hang on shutdown.
    /// </para>
    /// </remarks>
```
(Keep the existing summary sentence; only the `<remarks>` block is new.)

- [ ] **Step 7: Pack and check the dependency.**

```bash
rm -rf artifacts && dotnet pack "sources/IcyUI.sln" -c Release -o artifacts 2>&1 | grep -E "warn|error" ; \
unzip -p artifacts/IcyUI.MonoGame.0.1.0-alpha.1.nupkg IcyUI.MonoGame.nuspec | grep "<dependency "
```
Expected: no warnings or errors, and exactly one dependency line: `IcyUI`, version `0.1.0-alpha.1`. No `MonoGame.Framework.*`.
(Use the per-project pack form if Task 1 Step 5 switched to it.)

- [ ] **Step 8: Tests stay green, then commit.**

```bash
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" 2>&1 | tail -3
git add "sources/IcyUI.MonoGame/IcyUI.MonoGame.csproj" "sources/IcyUI.MonoGame/Configuration/MonoGameBuildingExtensions.cs" "sources/MonoGame Sample/"
git status --short
git commit -m "Make IcyUI.MonoGame backend-neutral and let the MonoGame sample pick WindowsDX or DesktopGL

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Check `git status --short` before committing: only the intended files should be staged (no `bin/`/`obj/`).

---

### Task 3: Release documentation and the consumer check

**Files:**
- Modify: `CLAUDE.md` (Layout table row, Build & test section, Known issues)
- Throwaway (not committed): `$CLAUDE_JOB_DIR/tmp/consumer-stride/`, `$CLAUDE_JOB_DIR/tmp/consumer-monogame/`

**Interfaces:**
- Consumes: `artifacts/` from Task 2 Step 7; `MonoGameBackend` from Task 2.
- Produces: nothing code-facing.

- [ ] **Step 1: Consumer check, Stride.**

```bash
C="$CLAUDE_JOB_DIR/tmp/consumer-stride"; rm -rf "$C"; mkdir -p "$C"; cd "$C"
dotnet new console -f net10.0 -o . --force >/dev/null
cat > nuget.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$(cygpath -w "C:/Users/oishi/source/repos/MysticUI/artifacts")" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
dotnet add package IcyUI.Stride --version 0.1.0-alpha.1
cat > Program.cs <<'EOF'
using Icy.Stride.Configuration;
using Icy.UI;
System.Console.WriteLine(typeof(StrideBuildingExtensions).Assembly.GetName().Version);
System.Console.WriteLine(typeof(Canvas).Assembly.GetName().Name);
EOF
dotnet build 2>&1 | tail -2
ls ~/.nuget/packages/icyui/0.1.0-alpha.1/lib/net10.0/
```
Expected: the build succeeds, and the `icyui` cache folder holds `IcyUI.dll` and `IcyUI.xml`. The local `nuget.config` with
`<clear />` keeps the user-level feeds (including the unreachable work feed) out of the restore.

This leaves `0.1.0-alpha.1` in the global packages cache. Before any re-check after repacking, delete
`~/.nuget/packages/icyui*/0.1.0-alpha.1` first, or the stale cached copy is used.

- [ ] **Step 2: Consumer check, MonoGame.**

```bash
C="$CLAUDE_JOB_DIR/tmp/consumer-monogame"; rm -rf "$C"; mkdir -p "$C"; cd "$C"
dotnet new console -f net10.0-windows -o . --force >/dev/null
cp "$CLAUDE_JOB_DIR/tmp/consumer-stride/nuget.config" .
dotnet add package IcyUI.MonoGame --version 0.1.0-alpha.1
dotnet build 2>&1 | grep -m3 -E "error|Build succeeded"
dotnet add package MonoGame.Framework.WindowsDX --version 3.8.5.1
cat > Program.cs <<'EOF'
using Icy.MonoGame.Configuration;
using Microsoft.Xna.Framework;
System.Console.WriteLine(typeof(MonoGameBuildingExtensions).FullName);
System.Console.WriteLine(typeof(Game).Assembly.Location);
EOF
dotnet build 2>&1 | tail -2
find bin -name "MonoGame.Framework.dll" | wc -l
```
Expected:
- the build *before* adding WindowsDX succeeds: the `Program.cs` template doesn't touch MonoGame types, and the package
  restores with no MonoGame dependency;
- after adding WindowsDX the build succeeds, with exactly `1` `MonoGame.Framework.dll`.

(`-f net10.0-windows`: `dotnet new console` may reject it. If so, create with `net10.0` and set `<TargetFramework>net10.0-windows</TargetFramework>` in the generated `.csproj`.)

- [ ] **Step 3: Update `CLAUDE.md`.**

Layout table row for `IcyUI.MonoGame`: replace `References \`MonoGame.Framework.DesktopGL\`.` with:
```
Backend-neutral: compiles against DesktopGL privately; hosts reference DesktopGL or WindowsDX 3.8.5+.
```
Layout table row for `MonoGame Sample`, `Stride Sample`: keep it, and append this sentence:
```
`MonoGame Sample` runs on WindowsDX by default; `-p:MonoGameBackend=DesktopGL` switches it.
```

Under **Build & test**, after the `dotnet test` bullet list, add:
````markdown
### Release

The four shipped libraries are packages; everything else is `IsPackable=false` (`sources/Directory.Build.targets`).
The version lives there too: one `<Version>` for all four, `0.1.0-alpha.N`. nuget.org never accepts the same version twice.

```
dotnet pack "sources/IcyUI.sln" -c Release -o artifacts
dotnet nuget push "artifacts/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key <key>
```

The push uploads the `.snupkg` symbol packages next to the `.nupkg` files. Release packing inherits the zero-warning rule.
````
(If Task 1 Step 5 switched to per-project packing, write those four `dotnet pack` lines instead of the `.sln` one.)

**Known issues**:
- The WoA deadlock bullet's first sentence becomes: "...on Windows-on-ARM64, MonoGame **DesktopGL** deadlocks on shutdown."
  Append: "WindowsDX has no SDL layer; whether the WindowsDX sample exits cleanly on WoA is for Ivan's smoke test."
- Replace the sub-bullet "The backends are mixed: ..." with:
  "`IcyUI.MonoGame` is backend-neutral and `MonoGame Sample` defaults to WindowsDX (`-p:MonoGameBackend=DesktopGL` for the other). The touch gap is DesktopGL's; WindowsDX touch is unverified."
- Replace "Settle the backend before applying engine-binary-dependent fixes like this one." with:
  "An SDL touch fix would apply to DesktopGL hosts only."

- [ ] **Step 4: Final verification.**

```bash
dotnet build "sources/IcyUI.sln" -c Release 2>&1 | grep -E "Warn|Error" | tail -2
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" 2>&1 | tail -3
git status --short
```
Expected: 0 errors and 0 warnings in the shipped libraries, the test `Total:` unchanged with 0 failed, and only `CLAUDE.md` modified.

- [ ] **Step 5: Commit.**

```bash
git add CLAUDE.md
git commit -m "Document the release flow and the MonoGame backend switch

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Hand over Ivan's smoke tests (don't claim them).**

List for Ivan:
1. **`MonoGame Sample` on WindowsDX (default), on both machines:** the shell and demos work. On ARM64, also record whether the process exits cleanly on close and whether touch works.
2. **`MonoGame Sample` on DesktopGL, on x64:** `dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" -p:MonoGameBackend=DesktopGL`, then run it.
3. **`Stride Sample`:** unchanged; a quick launch is enough.
4. **Optional:** reference the local `artifacts/` packages from the new game repo before pushing to nuget.org.
