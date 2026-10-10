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
