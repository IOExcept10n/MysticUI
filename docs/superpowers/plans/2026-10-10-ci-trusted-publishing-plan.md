# CI and Trusted Publishing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move every current link to the renamed `IOExcept10n/IcyUI` repo. Add GitHub Actions CI (x64 + ARM64) and a tag-triggered release workflow that publishes to nuget.org via trusted publishing, behind an approval-gated `nuget` environment.

**Architecture:**
- **Workflows:** two of them. `ci.yml` builds, tests and packs on every push and PR. `release.yml` builds and packs, then a separate `publish` job pushes with an OIDC-derived key, then a GitHub Release is created.
- **Scripts:** two small bash scripts in `.github/scripts/` hold the logic worth testing locally: the `.trx` result check and the tag/version/pushed-commit guard.
- **Actions:** pinned to SHAs, and Dependabot keeps them current.

**Tech Stack:** GitHub Actions (`windows-latest`, `windows-11-arm`, `ubuntu-latest`), `NuGet/login@v1`, .NET 10 SDK via `global.json`, bash, `gh` CLI (preinstalled on runners), actionlint + shellcheck (local verification only).

**Spec:** `docs/superpowers/specs/2026-10-10-ci-trusted-publishing-design.md`

## Global Constraints

- Repository: `IOExcept10n/IcyUI`, public, default branch `main`, development branch `platform-independent`.
- Workflow file names are part of the nuget.org policy: **`release.yml`** must keep that name.
- GitHub environment name: **`nuget`**. Secret: **`NUGET_USER`** (nuget.org profile name).
- Every action is pinned to a full SHA with a version comment. Use exactly these (resolved 2026-10-10):
  - `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`
  - `actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0`
  - `actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0`
  - `actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2`
  - `actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2`
  - `NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0`
- Workflow `run:` steps use `bash` (`defaults: run: shell: bash`).
- Least privilege:
  - top level: `contents: read`;
  - `publish`: `id-token: write` + `contents: read`;
  - `github-release`: `contents: write`.
- Shell scripts must be stored with LF line endings (`.gitattributes`). This machine has `core.autocrlf=true`, and so may the Windows runners.
- Never push, tag, create the nuget.org policy, or touch GitHub settings: those are Ivan's. Never claim a CI run happened.
- Historical mentions of `MysticUI` stay:
  - the root `README.md` history paragraph;
  - `docfx/docs/markup-spec.md`;
  - `docs/superpowers/**`;
  - `.remember/**`.
- `dotnet test` prints "Passed!" even when the host crashes. Read the `Total:` count.

## Review Focus

1. **CRLF in a `.sh` file on a Windows runner** breaks bash with `$'\r': command not found`. Expected: scripts run identically on Windows runners. Pinned in Task 2 Step 1 (`.gitattributes`) and Step 7 (`git ls-files --eol` shows `i/lf w/lf`).
2. **`check-trx.sh` given a missing file or a directory with no `.trx`** (host crashed before the logger wrote anything). Expected: a clear error and a failing exit, never a pass. Pinned in Task 2 Step 5 (missing-file case).
3. **The tag guard run on a tag whose commit is pushed, but where the tag object is annotated** (`git tag -a`). `git rev-list -n 1 <tag>` must resolve to the commit, not the tag object. Pinned in Task 2 Step 6 (annotated-tag case).
4. **`dotnet msbuild -getProperty:Version` output with a trailing CR or blank line on Windows,** making `v0.1.0-alpha.1` ≠ `v0.1.0-alpha.1\r`. Expected: a match. Pinned in Task 2 Step 6 (the passing case runs on Windows Git Bash, where CRs appear).
5. **The `windows-11-arm` runner lacks something the build needs** (Git Bash, `dotnet tool restore` for mgcb on ARM64). It can't be tested locally. The plan uses only `setup-dotnet`-installed tools plus Git Bash, and Task 5 lists it in Ivan's first-run checklist.

---

### Task 1: Rename cleanup

**Files:**
- Modify: `sources/Directory.Build.targets` (lines with `PackageProjectUrl`, `RepositoryUrl`)
- Modify: `sources/IcyUI/README.md`, `sources/IcyUI.Design/README.md`, `sources/IcyUI.MonoGame/README.md`, `sources/IcyUI.Stride/README.md` (the last two lines of each)
- Modify: `CLAUDE.md` (Naming history; the `PackageProjectUrl` note in the Release section)
- Local config (not committed): `origin` remote URL

**Interfaces:**
- Produces: the repo URL `https://github.com/IOExcept10n/IcyUI`, used in Task 5's docs.

- [ ] **Step 1: The failing check.**

```bash
git grep -n -i "mysticui" -- . ':!docs/superpowers' ':!.remember' ':!README.md' ':!docfx/docs/markup-spec.md'
git remote get-url origin
```
Expected:
- 11 hits: `CLAUDE.md` naming history, 2 in `sources/Directory.Build.targets`, 2 in each of the 4 READMEs;
- the remote is `git@github.com:IOExcept10n/MysticUI.git`.

- [ ] **Step 2: Replace the links.**

```bash
sed -i 's#github.com/IOExcept10n/MysticUI#github.com/IOExcept10n/IcyUI#g' \
  sources/Directory.Build.targets sources/IcyUI/README.md sources/IcyUI.Design/README.md \
  sources/IcyUI.MonoGame/README.md sources/IcyUI.Stride/README.md
```
In `CLAUDE.md`, replace the Naming history paragraph
```
The project was renamed MysticUI → AquaUI → IcyUI. The GitHub repo and local folder are still named `MysticUI`. Remaining old-name references outside historical docs are bugs, so flag them.
```
with
```
The project was renamed MysticUI → AquaUI → IcyUI. The GitHub repository was renamed to `IOExcept10n/IcyUI` on 2026-10-10; only the local folder is still named `MysticUI`. Remaining old-name references outside historical docs are bugs, so flag them.
```
(`sed -i` keeps the files' existing line endings in Git Bash. Check `git diff --stat` afterwards: the line counts must be small, not whole-file.)

- [ ] **Step 3: Update the local remote.**

```bash
git remote set-url origin git@github.com:IOExcept10n/IcyUI.git
git remote get-url origin
```
Expected: `git@github.com:IOExcept10n/IcyUI.git`. `git fetch` can't be tested here: the session has no SSH key, as `Permission denied (publickey)` showed earlier.

- [ ] **Step 4: Verify.**

```bash
git grep -n -i "mysticui" -- . ':!docs/superpowers' ':!.remember' ':!README.md' ':!docfx/docs/markup-spec.md'
rm -rf artifacts && dotnet pack sources/IcyUI/IcyUI.csproj -c Release -o artifacts > "$CLAUDE_JOB_DIR/tmp/t1-pack.log" 2>&1; echo exit $?
unzip -p artifacts/IcyUI.0.1.0-alpha.1.nupkg IcyUI.nuspec | grep -E "projectUrl|<repository"
unzip -o -q artifacts/IcyUI.0.1.0-alpha.1.snupkg -d "$CLAUDE_JOB_DIR/tmp/t1-snupkg"; grep -a -o '"/_/\*":"[^"]*"' "$CLAUDE_JOB_DIR/tmp/t1-snupkg/lib/net10.0/IcyUI.pdb"
```
Expected:
- exactly one `git grep` hit, the new `CLAUDE.md` naming line ("…still named `MysticUI`…"), which is intentional;
- pack exit `0`;
- `projectUrl` is `https://github.com/IOExcept10n/IcyUI/tree/platform-independent`, and `repository url` is `https://github.com/IOExcept10n/IcyUI`;
- the SourceLink map is `https://raw.githubusercontent.com/IOExcept10n/IcyUI/<sha>/*`.

- [ ] **Step 5: Commit.**

```bash
git add sources/Directory.Build.targets sources/IcyUI*/README.md CLAUDE.md
git commit -m "Point package links and docs at the renamed IOExcept10n/IcyUI repository

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The two check scripts

**Files:**
- Create: `.gitattributes`
- Create: `.github/scripts/check-trx.sh`
- Create: `.github/scripts/check-release-tag.sh`

**Interfaces:**
- Produces (used by Tasks 3 and 4):
  - `bash .github/scripts/check-trx.sh <path-to-results.trx>` exits 0 only for a completed run with total > 0, failed = 0 and error = 0. It prints `Tests: outcome=… total=… passed=… failed=… error=…`.
  - `bash .github/scripts/check-release-tag.sh <tag>`, run from the repo root, exits 0 only if `<tag>` == `v` + `Version` of `sources/IcyUI/IcyUI.csproj` and the tag's commit is an ancestor of `origin/main` or `origin/platform-independent`. It prints `Release tag <tag> matches version <v> at <sha>, which is on origin/<branch>.`
  - Both emit a GitHub `::error::` line on failure.

- [ ] **Step 1: `.gitattributes`.**

Create `.gitattributes` at the repo root:
```
# Shell scripts run under bash on the Windows CI runners too; CRLF would break them.
*.sh text eol=lf
```

- [ ] **Step 2: Write the test cases first (they fail: the scripts don't exist).**

```bash
T="$CLAUDE_JOB_DIR/tmp/trx"; ls "$T/results.trx" || dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --logger "trx;LogFileName=results.trx" --results-directory "$T" | tail -1
sed 's/<ResultSummary outcome="Completed">/<ResultSummary outcome="Failed">/' "$T/results.trx" > "$T/outcome-failed.trx"
sed 's/ failed="0"/ failed="1"/' "$T/results.trx" > "$T/one-failed.trx"
sed 's/<Counters total="[0-9]*"/<Counters total="0"/' "$T/results.trx" > "$T/zero-total.trx"
sed 's/ error="0"/ error="2"/' "$T/results.trx" > "$T/errors.trx"
for f in results outcome-failed one-failed zero-total errors missing; do bash .github/scripts/check-trx.sh "$T/$f.trx" >/dev/null 2>&1; echo "$f -> $?"; done
```
Expected now: every case `-> 127`, because the script is missing.

- [ ] **Step 3: Write `.github/scripts/check-trx.sh`.**

```bash
#!/usr/bin/env bash
# Fails unless a VSTest .trx file records a completed run with tests and no failures.
# `dotnet test` can print "Passed!" after the test host crashes, so CI checks the results file instead.
# Usage: check-trx.sh <results.trx>
set -euo pipefail

trx="${1:?usage: check-trx.sh <results.trx>}"
if [[ ! -f "$trx" ]]; then
  echo "::error::No test results at '$trx': the test host may have crashed before writing them."
  exit 1
fi

outcome=$(grep -o '<ResultSummary outcome="[^"]*"' "$trx" | head -n 1 | cut -d '"' -f 2 || true)
counters=$(grep -o '<Counters [^>]*>' "$trx" | head -n 1 || true)
counter() { grep -o " $1=\"[0-9]*\"" <<< "$counters" | cut -d '"' -f 2 || true; }
total=$(counter total)
passed=$(counter passed)
failed=$(counter failed)
errors=$(counter error)

echo "Tests: outcome=${outcome:-?} total=${total:-?} passed=${passed:-?} failed=${failed:-?} error=${errors:-?}"
if [[ "$outcome" != "Completed" || -z "$total" || "$total" -eq 0 || "${failed:-1}" -ne 0 || "${errors:-1}" -ne 0 ]]; then
  echo "::error::Test run did not complete cleanly (outcome=${outcome:-missing}, total=${total:-missing}, failed=${failed:-missing}, error=${errors:-missing})."
  exit 1
fi
```

- [ ] **Step 4: Write `.github/scripts/check-release-tag.sh`.**

```bash
#!/usr/bin/env bash
# Guards a release: the tag must be v<Version> (sources/Directory.Build.targets, read through IcyUI.csproj), and the
# tagged commit must already be on origin/main or origin/platform-independent, so SourceLink and the nuspec point at a
# commit GitHub has. Run from the repository root with full history fetched.
# Usage: check-release-tag.sh <tag>
set -euo pipefail

tag="${1:?usage: check-release-tag.sh <tag>}"
version=$(dotnet msbuild sources/IcyUI/IcyUI.csproj -getProperty:Version | tr -d '\r' | sed '/^[[:space:]]*$/d' | tail -n 1)
if [[ "$tag" != "v$version" ]]; then
  echo "::error::Tag '$tag' doesn't match the package version '$version'. Expected tag 'v$version'."
  exit 1
fi

commit=$(git rev-list -n 1 "$tag")
for branch in main platform-independent; do
  if git merge-base --is-ancestor "$commit" "origin/$branch" 2>/dev/null; then
    echo "Release tag $tag matches version $version at $commit, which is on origin/$branch."
    exit 0
  fi
done
echo "::error::Commit $commit (tag '$tag') isn't on origin/main or origin/platform-independent. Push the branch before tagging."
exit 1
```

- [ ] **Step 5: Run the `check-trx.sh` cases.**

Re-run the loop from Step 2.
Expected: `results -> 0`, and `outcome-failed`, `one-failed`, `zero-total`, `errors`, `missing` all `-> 1`.
Also run `bash .github/scripts/check-trx.sh "$T/results.trx"`. Expected: prints `Tests: outcome=Completed total=1839 passed=1839 failed=0 error=0` (or the current count).

- [ ] **Step 6: Run the `check-release-tag.sh` cases with temporary local tags.**

`origin/platform-independent` here is the last fetched state. The commits since are unpushed, which makes a real "unpushed" case.
```bash
PUSHED=$(git rev-parse origin/platform-independent); echo "pushed: $PUSHED"
git tag tmp-check-1 HEAD
git tag v0.1.0-alpha.1 "$PUSHED"
git tag -a v0.1.0-alpha.2 "$PUSHED" -m tmp
bash .github/scripts/check-release-tag.sh v0.1.0-alpha.1; echo "pushed,lightweight -> $?"
bash .github/scripts/check-release-tag.sh v0.1.0-alpha.2; echo "version mismatch -> $?"
git tag -d v0.1.0-alpha.1 >/dev/null; git tag -a v0.1.0-alpha.1 "$PUSHED" -m tmp
bash .github/scripts/check-release-tag.sh v0.1.0-alpha.1; echo "pushed,annotated -> $?"
git tag -d v0.1.0-alpha.1 >/dev/null; git tag v0.1.0-alpha.1 HEAD
bash .github/scripts/check-release-tag.sh v0.1.0-alpha.1; echo "unpushed -> $?"
git tag -d v0.1.0-alpha.1 v0.1.0-alpha.2 tmp-check-1 >/dev/null; git tag --list 'v*' 'tmp-*'
```
Expected:
- `pushed,lightweight -> 0` and `pushed,annotated -> 0`, each printing the "matches version … on origin/platform-independent" line;
- `version mismatch -> 1` with the mismatch error, and `unpushed -> 1` with the "Push the branch" error;
- the final `git tag --list` prints nothing (all temporary tags removed).

If any `v0.1.0-alpha.*` tag already exists locally before this step, stop and report: don't delete a real tag.

- [ ] **Step 7: Check line endings and commit.**

```bash
git add .gitattributes .github/scripts/check-trx.sh .github/scripts/check-release-tag.sh
git update-index --chmod=+x .github/scripts/check-trx.sh .github/scripts/check-release-tag.sh
git ls-files --eol .github/scripts/
git commit -m "Add the CI checks for test results and release tags

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Expected: `git ls-files --eol` shows `i/lf` for both scripts (index LF). The working-tree copy can be `w/crlf` only if it was
written before `.gitattributes` existed. In that case, run `git rm --cached -r .github/scripts && git add .github/scripts` and re-check.

---

### Task 3: `ci.yml` and Dependabot

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/dependabot.yml`
- Tooling (not committed): `$CLAUDE_JOB_DIR/tmp/tools/actionlint.exe`, `$CLAUDE_JOB_DIR/tmp/tools/shellcheck.exe`

**Interfaces:**
- Consumes: `check-trx.sh` (Task 2).
- Produces: the artifact names `test-results-<os>` and `packages`; the cache key format reused by Task 4.

- [ ] **Step 1: Get the linters (the failing check: nothing to lint yet).**

```bash
D="$CLAUDE_JOB_DIR/tmp/tools"; mkdir -p "$D"; cd "$D"
curl -sSL -o actionlint.zip https://github.com/rhysd/actionlint/releases/download/v1.7.12/actionlint_1.7.12_windows_amd64.zip && unzip -o -q actionlint.zip actionlint.exe
curl -sSL -o shellcheck.zip https://github.com/koalaman/shellcheck/releases/download/v0.11.0/shellcheck-v0.11.0.zip && unzip -o -q shellcheck.zip
ls; cd - >/dev/null
"$D/actionlint.exe" -version
```
Expected: `actionlint.exe` and `shellcheck.exe` exist (the shellcheck zip may unpack as `shellcheck.exe` at its root; adjust the path if it lands in a subfolder), and actionlint prints `1.7.12`.

- [ ] **Step 2: Write `.github/workflows/ci.yml`.**

```yaml
# Builds, tests and packs IcyUI on x64 and ARM64 Windows for every push and pull request.
name: CI

on:
  push:
    branches: [main, platform-independent]
  pull_request:
  workflow_dispatch:

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

defaults:
  run:
    shell: bash

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  build-test:
    name: Build and test (${{ matrix.os }})
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, windows-11-arm]
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1

      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ runner.arch }}-${{ hashFiles('sources/Directory.Packages.props', 'sources/**/*.csproj', 'sources/MonoGame Sample/.config/dotnet-tools.json') }}
          restore-keys: nuget-${{ runner.os }}-${{ runner.arch }}-

      - name: Build (Release, shipped libraries warning-free)
        run: dotnet build "sources/IcyUI.sln" -c Release

      - name: Build the MonoGame sample on DesktopGL
        run: dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" -c Release -p:MonoGameBackend=DesktopGL

      - name: Test
        run: dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" -c Release --no-build --logger "trx;LogFileName=results.trx" --results-directory test-results

      - name: Check test results
        if: always()
        run: bash .github/scripts/check-trx.sh test-results/results.trx

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: test-results-${{ matrix.os }}
          path: test-results/
          if-no-files-found: warn

      - name: Pack
        if: matrix.os == 'windows-latest'
        run: |
          for project in IcyUI IcyUI.Design IcyUI.MonoGame IcyUI.Stride; do
            dotnet pack "sources/$project/$project.csproj" -c Release --no-build -o artifacts
          done

      - name: Upload packages
        if: matrix.os == 'windows-latest'
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: packages
          path: artifacts/
          if-no-files-found: error
```

- [ ] **Step 3: Write `.github/dependabot.yml`.**

```yaml
# Keeps the SHA-pinned GitHub Actions current. NuGet versions stay manual: the Stride and MonoGame versions are
# deliberate choices (see sources/Directory.Packages.props).
version: 2
updates:
  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: monthly
```

- [ ] **Step 4: Lint.**

```bash
D="$CLAUDE_JOB_DIR/tmp/tools"; "$D/actionlint.exe" -shellcheck "$D/shellcheck.exe" .github/workflows/ci.yml; echo "actionlint -> $?"
"$D/shellcheck.exe" .github/scripts/*.sh; echo "shellcheck -> $?"
```
Expected: `actionlint -> 0` and `shellcheck -> 0`, with no output. Fix real findings. If actionlint reports an unknown input for an action whose `action.yml` at the pinned SHA does have that input (its built-in metadata can lag new majors), ledger a ruling and continue.

- [ ] **Step 5: Run the CI commands locally, in the same form.**

```bash
L="$CLAUDE_JOB_DIR/tmp/ci-local"; mkdir -p "$L"
dotnet build "sources/IcyUI.sln" -c Release > "$L/build.log" 2>&1; echo "build -> $?"; grep -E "Warn|Error\(s\)" "$L/build.log" | tail -2
dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" -c Release -p:MonoGameBackend=DesktopGL > "$L/gl.log" 2>&1; echo "desktopgl -> $?"
rm -rf test-results; dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" -c Release --no-build --logger "trx;LogFileName=results.trx" --results-directory test-results 2>&1 | tail -1
bash .github/scripts/check-trx.sh test-results/results.trx; echo "check -> $?"
rm -rf artifacts; for project in IcyUI IcyUI.Design IcyUI.MonoGame IcyUI.Stride; do dotnet pack "sources/$project/$project.csproj" -c Release --no-build -o artifacts > /dev/null || echo "pack $project failed"; done; ls artifacts | wc -l
rm -rf test-results
```
Expected:
- `build -> 0` with `0 Error(s)`, and `desktopgl -> 0`;
- the tests show the current total (1839) with 0 failed, and `check -> 0`;
- 8 files in `artifacts`.

`test-results/` must not be committed. Add `test-results/` to `.gitignore` if `git status` shows it (it's removed above, but CI and local runs recreate it).

- [ ] **Step 6: Commit.**

```bash
git add .github/workflows/ci.yml .github/dependabot.yml
git add .gitignore 2>/dev/null
git commit -m "Build, test and pack on x64 and ARM64 Windows in CI

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `release.yml`

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: `check-trx.sh` and `check-release-tag.sh` (Task 2); the cache key format and the artifact name `packages` (Task 3).
- Produces: the workflow file name `release.yml`, the environment `nuget` and the secret `NUGET_USER`, all three referenced by Task 5's setup docs.

- [ ] **Step 1: The failing check.**

```bash
"$CLAUDE_JOB_DIR/tmp/tools/actionlint.exe" .github/workflows/release.yml; echo "-> $?"
```
Expected: a non-zero exit (the file doesn't exist).

- [ ] **Step 2: Write `.github/workflows/release.yml`.**

```yaml
# Publishes IcyUI to nuget.org through trusted publishing when a v* tag is pushed.
# nuget.org's trusted-publishing policy names this file (release.yml) and the `nuget` environment; keep both names.
# A manual run is a dry run: it builds, tests and packs, but never publishes.
name: Release

on:
  push:
    tags: ['v*']
  workflow_dispatch:

permissions:
  contents: read

defaults:
  run:
    shell: bash

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  build:
    name: Build, test and pack
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json

      - uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ runner.arch }}-${{ hashFiles('sources/Directory.Packages.props', 'sources/**/*.csproj', 'sources/MonoGame Sample/.config/dotnet-tools.json') }}
          restore-keys: nuget-${{ runner.os }}-${{ runner.arch }}-

      - name: Check the tag against the version and the pushed branches
        if: startsWith(github.ref, 'refs/tags/v')
        run: bash .github/scripts/check-release-tag.sh "$GITHUB_REF_NAME"

      - name: Build
        run: dotnet build "sources/IcyUI.sln" -c Release

      - name: Test
        run: dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" -c Release --no-build --logger "trx;LogFileName=results.trx" --results-directory test-results

      - name: Check test results
        if: always()
        run: bash .github/scripts/check-trx.sh test-results/results.trx

      - name: Pack
        run: |
          for project in IcyUI IcyUI.Design IcyUI.MonoGame IcyUI.Stride; do
            dotnet pack "sources/$project/$project.csproj" -c Release --no-build -o artifacts
          done

      - name: Upload packages
        uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        with:
          name: packages
          path: artifacts/
          if-no-files-found: error

  publish:
    name: Publish to nuget.org
    needs: build
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: windows-latest
    environment: nuget
    permissions:
      contents: read
      id-token: write
    steps:
      - uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          name: packages
          path: packages

      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          dotnet-version: 10.0.x

      - name: NuGet login (OIDC → temporary API key)
        id: login
        uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
        with:
          user: ${{ secrets.NUGET_USER }}

      - name: Push packages and symbols
        run: dotnet nuget push "packages/*.nupkg" --api-key "${{ steps.login.outputs.NUGET_API_KEY }}" --source https://api.nuget.org/v3/index.json --skip-duplicate

  github-release:
    name: GitHub Release
    needs: publish
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333 # v8.0.2
        with:
          name: packages
          path: packages

      - name: Create the release
        env:
          GH_TOKEN: ${{ github.token }}
          TAG: ${{ github.ref_name }}
        run: |
          prerelease=()
          if [[ "$TAG" == *-* ]]; then prerelease=(--prerelease); fi
          gh release create "$TAG" packages/* --repo "$GITHUB_REPOSITORY" --title "$TAG" --generate-notes --verify-tag "${prerelease[@]}"
```

- [ ] **Step 3: Lint.**

```bash
D="$CLAUDE_JOB_DIR/tmp/tools"; "$D/actionlint.exe" -shellcheck "$D/shellcheck.exe" .github/workflows/*.yml; echo "actionlint -> $?"
```
Expected: `actionlint -> 0`, with no output. Same ruling rule as Task 3 Step 4 for lagging action metadata. actionlint may warn
that the `nuget` environment or the `NUGET_USER` secret is unknown. It can't see repo settings, so that's expected; ledger it.

- [ ] **Step 4: Check the `prerelease` logic in bash.**

```bash
for TAG in v0.1.0-alpha.1 v1.0.0; do prerelease=(); if [[ "$TAG" == *-* ]]; then prerelease=(--prerelease); fi; echo "$TAG -> [${prerelease[*]}]"; done
```
Expected: `v0.1.0-alpha.1 -> [--prerelease]` and `v1.0.0 -> []`.

- [ ] **Step 5: Commit.**

```bash
git add .github/workflows/release.yml
git commit -m "Publish tagged releases to nuget.org through trusted publishing

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `CLAUDE.md`: CI, release flow and one-time setup

**Files:**
- Modify: `CLAUDE.md` (Build & test: the "No CI" line; the whole `### Release` subsection)

**Interfaces:**
- Consumes: the names `ci.yml`, `release.yml`, `nuget`, `NUGET_USER` and `.github/scripts/*` (Tasks 2–4); the repo URL (Task 1).

- [ ] **Step 1: The failing check.**

```bash
grep -c "No CI and no build scripts\|--api-key <key>" CLAUDE.md; grep -c "release.yml" CLAUDE.md
```
Expected: `2` and `0`.

- [ ] **Step 2: Replace the "No CI" line.**

Replace
```
No CI and no build scripts. Use plain `dotnet` commands:
```
with
```
CI (`.github/workflows/ci.yml`) builds, tests and packs on x64 and ARM64 Windows for every push to `main`/`platform-independent` and every PR. The only scripts are the two checks in `.github/scripts/` (test results, release tag). Locally, use plain `dotnet` commands:
```

- [ ] **Step 3: Replace the whole `### Release` subsection** (from `### Release` up to, not including, `## Conventions`) with:

````markdown
### Release

The four shipped libraries are packages; everything else is `IsPackable=false` (`sources/Directory.Build.targets`). The version lives there too: one `<Version>` for all four, `0.1.0-alpha.N`. nuget.org never accepts the same version twice.

Releases go through `.github/workflows/release.yml` with nuget.org trusted publishing: no API key exists anywhere.

1. Bump `<Version>` in `sources/Directory.Build.targets`, commit, push, and wait for green CI.
2. `git tag v<version>` and `git push origin v<version>`.
3. In the Actions run, approve the `nuget` deployment. The workflow checks that the tag matches `<Version>` and that the commit is on `origin/main` or `origin/platform-independent`, then builds, tests, packs and publishes (`.snupkg` symbols included). Finally it creates a GitHub Release with the packages attached.

A manual run of `release.yml` is a dry run: it builds, tests and packs, and never publishes. To try packages locally:

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
````

- [ ] **Step 4: Verify and commit.**

```bash
grep -c "No CI and no build scripts\|--api-key <key>" CLAUDE.md; grep -c "release.yml" CLAUDE.md
git diff --stat
git add CLAUDE.md
git commit -m "Document CI, the tag-based release flow and the trusted-publishing setup

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Expected: `0`, then `3` or more. The diff touches only `CLAUDE.md`.

- [ ] **Step 5: Final suite and hand-over list.**

```bash
dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj 2>&1 | tail -1
git status --short
```
Expected: the test `Total:` unchanged with 0 failed, and a clean tree.

Hand Ivan these steps (none of them claimed):
1. **On both machines:** `git remote set-url origin git@github.com:IOExcept10n/IcyUI.git`. This one is already done.
2. **One-time setup:** steps 1–2 of the "One-time publishing setup" in `CLAUDE.md`.
3. **First CI run:** push, then check that CI is green on `windows-latest` **and** `windows-11-arm`. The ARM64 runner is untested until then: Git Bash, and the `dotnet tool restore` for mgcb on ARM64.
4. **Dry run:** run `release.yml` manually and check its `packages` artifact.
5. **First release:** tag `v0.1.0-alpha.1`, push the tag, approve, then check nuget.org and the GitHub Release.
