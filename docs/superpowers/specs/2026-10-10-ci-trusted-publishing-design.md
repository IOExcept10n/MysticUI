# CI and trusted publishing to nuget.org, plus the IcyUI repo rename

> Design spec, discussed with Ivan on 2026-10-10. Follow-up to `2026-10-10-nuget-packaging-design.md`: it replaces
> that spec's manual API-key push. Ivan renamed the GitHub repository from `MysticUI` to `IcyUI` the same day.

## Context

- **Publishing uses an API key today.** The packaging sub-project documented a local
  `dotnet nuget push --api-key <key>`. nuget.org recommends trusted publishing instead: a GitHub Actions workflow
  exchanges its OIDC token for a one-hour, single-use API key, so no long-lived secret exists.
- **There is no CI.** Ivan builds on an x64 desktop and a Windows-on-ARM64 laptop, so architecture-specific breaks
  surface only on whichever machine he happens to switch to.
- **The repo is renamed.** It is now `IOExcept10n/IcyUI` (public, default branch `main`). Old-name links remain in:
  - the package metadata (`PackageProjectUrl`, `RepositoryUrl`);
  - the four package READMEs;
  - `CLAUDE.md`'s naming history.

  The local `origin` remote still uses the old URL. GitHub redirects it, but SourceLink embeds the remote URL in
  locally packed PDBs.

### Trusted publishing, as documented by nuget.org (checked 2026-10-10)

- **The policy:** created on nuget.org under the user's Trusted Publishing page. It names:
  - the repository owner and repository;
  - the workflow **file name** (no path);
  - optionally, a GitHub environment.
- **Scopes:** a policy's scopes can allow publishing new packages and new versions, with a glob over package IDs.
- **In the workflow:** the job needs `permissions: id-token: write`. `NuGet/login@v1` takes `user:` (the nuget.org
  profile name, not an email) and outputs `NUGET_API_KEY`.
- **The temporary key:** valid for one hour; one OIDC token yields one key.
- **Activation:** policies for private repositories may start "temporarily active" for 7 days, until the first
  publish. This repo is public.

## Decisions

| Question | Decision |
|---|---|
| Scope | Everyday CI (`ci.yml`) and a release workflow (`release.yml`). The DocFX site deployment belongs to the docs sub-project. |
| CI architectures | `windows-latest` (x64) and `windows-11-arm` (ARM64), matching Ivan's two machines. |
| Version source of truth | `<Version>` in `sources/Directory.Build.targets`. The release workflow fails if the tag isn't `v` + that version. |
| Publish guard | GitHub environment `nuget`: deployments only from `v*` tags, and Ivan as required reviewer. The nuget.org policy is restricted to that environment. |
| GitHub Release | Created per tag, with the `.nupkg` and `.snupkg` files and generated notes; a prerelease when the version has a `-` suffix. |
| Action pinning | Every action pinned to a full commit SHA with a version comment. Dependabot updates the `github-actions` ecosystem monthly. |
| Rename | All current links move to `IOExcept10n/IcyUI`. Historical mentions stay. |

## Design

### 1. Rename cleanup

- **`sources/Directory.Build.targets`:**
  - `PackageProjectUrl` → `https://github.com/IOExcept10n/IcyUI/tree/platform-independent` (still the development
    branch until the MVP merge);
  - `RepositoryUrl` → `https://github.com/IOExcept10n/IcyUI`.
- **The four `sources/IcyUI*/README.md` files:** the "Source and samples" and "Issues" links move to `IOExcept10n/IcyUI`.
- **`CLAUDE.md`, Naming history:** "The GitHub repository was renamed to `IcyUI` on 2026-10-10; only the local folder is
  still `MysticUI`." Remaining old-name references outside historical docs are still bugs.
- **Local remote:** `git remote set-url origin git@github.com:IOExcept10n/IcyUI.git`. This is local git config, not a
  commit; Ivan's other machine needs the same command.
- **Unchanged:** the root `README.md` history paragraph, the "pre-rewrite `MysticUI`" note in `docfx/docs/markup-spec.md`,
  and the dated specs and plans under `docs/superpowers/`.

### 2. `.github/workflows/ci.yml`

- **Triggers:** `push` to `main` and `platform-independent`, `pull_request`, and `workflow_dispatch`.
- **Concurrency:** group `ci-${{ github.ref }}`, `cancel-in-progress: true`.
- **Top-level `permissions`:** `contents: read`.
- **Shell:** `defaults: run: shell: bash` (Git Bash ships on the Windows runners), so the commands and the scripts in
  `.github/scripts/` behave as they do in Ivan's Git Bash. The same applies to `release.yml`.
- **Job `build-test`:** matrix `os: [windows-latest, windows-11-arm]`, `fail-fast: false`. Steps:
  1. **Checkout.**
  2. **Install the SDK:** `actions/setup-dotnet` with `global-json-file: global.json`.
  3. **NuGet cache:** `actions/cache` on `~/.nuget/packages`. The key is the OS + architecture + a hash of
     `sources/Directory.Packages.props`, `**/*.csproj` and `**/.config/dotnet-tools.json`.
  4. **Build:** `dotnet build "sources/IcyUI.sln" -c Release`. Shipped libraries already fail on any warning in Release.
  5. **DesktopGL sample:** `dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj" -c Release -p:MonoGameBackend=DesktopGL`.
  6. **Test:** `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" -c Release --no-build --logger "trx;LogFileName=results.trx" --results-directory test-results`.
  7. **Check the test results:** a script (`.github/scripts/check-trx.sh`, bash) reads the `.trx` `ResultSummary` and
     fails unless:
     - `outcome` is `Completed` (not `Failed`, `Aborted` or anything else);
     - `failed` is `0`;
     - `total` is greater than `0`.

     It prints total, passed and failed. It runs `if: always()` after the test step, so an aborted host is reported as
     such.
  8. **Upload the results:** `test-results/` as an artifact named `test-results-${{ matrix.os }}`, `if: always()`.
  9. **Pack (x64 only):** for each of the four shipped projects,
     `dotnet pack <project> -c Release --no-build -o artifacts`. This runs package validation on every push.
  10. **Upload the packages (x64 only):** `artifacts/` as an artifact named `packages`.

### 3. `.github/workflows/release.yml`

- **Triggers:** `push` of tags `v*`, and `workflow_dispatch` (a dry run: everything except publishing).
- **Top-level `permissions`:** `contents: read`. No concurrency cancel: a release must never be cancelled half-way.
- **Job `build`** (`windows-latest`):
  1. Checkout with `fetch-depth: 0`, then `setup-dotnet` and the NuGet cache as in CI.
  2. **Tag guard** (tag runs only), via `.github/scripts/check-release-tag.sh <tag>`:
     - The version comes from `dotnet msbuild sources/IcyUI/IcyUI.csproj -getProperty:Version`; the tag must equal
       `v<version>`.
     - The tagged commit must be an ancestor of `origin/main` or `origin/platform-independent`, checked with
       `git merge-base --is-ancestor`.
     - Either failure prints the reason and exits non-zero.
  3. **Build, test and check results** exactly as in CI steps 4, 6 and 7. The DesktopGL sample build is skipped, since
     CI covers it.
  4. **Pack and upload:** pack the four projects into `artifacts/` and upload them as the artifact `packages`.
- **Job `publish`** (needs `build`; `if: startsWith(github.ref, 'refs/tags/v')`; `windows-latest`):
  - `environment: nuget`, so it pauses for Ivan's approval.
  - `permissions: id-token: write`, `contents: read`.
  - **Steps:**
    1. `actions/download-artifact` for `packages`, then `setup-dotnet`.
    2. `NuGet/login@v1` with `user: ${{ secrets.NUGET_USER }}`.
    3. `dotnet nuget push "packages/*.nupkg" --api-key ${{ steps.login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`.
       The `.snupkg` files next to them are pushed automatically.
  - **No checkout:** no repository code runs in this job.
- **Job `github-release`** (needs `publish`; `ubuntu-latest`):
  - `permissions: contents: write`.
  - It downloads `packages` and runs
    `gh release create "$TAG" packages/* --generate-notes --verify-tag --title "$TAG"`, adding `--prerelease` when
    the tag contains `-`.
  - `GH_TOKEN` is `${{ github.token }}`, and `--repo ${{ github.repository }}` is passed because there is no checkout.

### 4. `.github/dependabot.yml`

One ecosystem, `github-actions`, at `/`, interval `monthly`. NuGet updates stay manual, since the Stride and MonoGame
versions are deliberate choices.

### 5. Documentation: `CLAUDE.md`

- **"No CI and no build scripts" becomes a CI paragraph:**
  - `ci.yml` builds, tests and packs on x64 and ARM64 for every push and PR;
  - the only scripts are the two small checks in `.github/scripts/`;
  - plain `dotnet` commands remain the local workflow.
- **The Release block becomes the tag flow:**
  1. Bump `<Version>` in `sources/Directory.Build.targets`.
  2. Commit and push, then wait for green CI.
  3. `git tag v<version>` and `git push origin v<version>`.
  4. Approve the `nuget` deployment in the Actions run.

  The local `dotnet pack` stays documented for testing packages. The API-key push line is removed.
- **New subsection "One-time publishing setup",** Ivan's steps:
  1. **nuget.org → Trusted Publishing → new policy:** owner `IOExcept10n`, repository `IcyUI`, workflow `release.yml`,
     environment `nuget`. Scopes: push new packages and new versions, package glob `IcyUI*`.
  2. **GitHub → Settings → Environments → new `nuget`:**
     - required reviewer: Ivan;
     - deployment branches and tags: selected, with the tag rule `v*`;
     - environment secret `NUGET_USER`: the nuget.org profile name.
  3. **First release:** push the branch, wait for green CI, then run `release.yml` manually from `platform-independent`
     as a dry run. Then tag `v0.1.0-alpha.1`, push the tag, and approve.

## Error handling

- **Tag/version mismatch, or a tag on an unpushed commit:** the `build` job fails before building, and nothing is published.
- **Test failure or crashed test host:** the `.trx` check fails the job, so no pack or publish happens.
- **The nuget.org policy is missing or misconfigured:** `NuGet/login` fails, and no packages are pushed. Fix the policy,
  then re-run the failed `publish` job.
- **Partial push:** for example, two of four packages accepted, then a network error. Re-running `publish` skips the
  already-pushed versions (`--skip-duplicate`) and pushes the rest.
- **The GitHub Release fails after a successful publish:** re-run only `github-release`. The packages are already on
  nuget.org and are unaffected.

## Testing

GitHub Actions can't be run from the development session, so verification is split:

- **Static checks:** `actionlint` (a single binary downloaded to the job's temp folder, not committed) passes on both
  workflow files, including its shellcheck integration for `run:` blocks.
- **`check-trx.sh`, run locally against:**
  - a real `.trx` from a local test run: passes, printing the counts;
  - a copy edited to `outcome="Failed"`: fails;
  - a copy with `failed="1"`: fails;
  - a copy with `total="0"`: fails.
- **`check-release-tag.sh`, run locally:**
  - `v0.1.0-alpha.1` at a pushed commit: passes;
  - `v0.1.0-alpha.2`: fails with a version-mismatch message;
  - a commit that's on no remote branch (a temporary local commit, then reset): fails with an "unpushed" message.
- **The CI commands themselves** (`build`, the DesktopGL build, `test` with the trx logger, `pack --no-build`) run
  locally in the same form and succeed.
- **Ivan's checks (not claimed here):**
  - the first CI run is green on both runners;
  - the `release.yml` dry run is green, and its `packages` artifact contains 4 + 4 files;
  - the first tagged release publishes after his approval and creates the GitHub Release.

## Performance

- **Library runtime:** no impact.
- **CI time:** roughly 5–10 minutes per push, on two runners that are free for public repos. The NuGet cache keeps
  restores short. The ARM64 runner can queue longer. If that becomes a nuisance, restrict it to pushes (no PRs) later.
- **Release:** about one CI run plus the approval wait.

## Out of scope

- DocFX site build and deployment (the docs sub-project).
- Linux/macOS runners (the samples target `net10.0-windows`; the core could run there later).
- Code coverage reporting, NuGet dependency updates via Dependabot, and package signing.
- Renaming the local folder `MysticUI`.
