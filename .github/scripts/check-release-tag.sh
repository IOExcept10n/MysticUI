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
