#!/usr/bin/env bash
# Crest-owned test entrypoint. Host repos (a product host, Crest.Crest.Host) delegate
# here instead of walking Crest's internal subproject layout
# themselves - this script is the one place that knows that layout.
#
# This script holds no credentials and makes no decisions about .env, server lifecycle, or
# whether tests should run at all - that's entirely the calling host's job. It only accepts
# BASE_URL (already resolved by the caller) and discovers/runs what's underneath it.
#
# Discovers and runs, for every module's tests/ subdirectory (Crest.* and
# Crest.* alike):
#   - a C# test project (*.csproj directly under tests/<ProjectName>/) via `dotnet test`
#   - a Playwright suite (tests/playwright/) via the existing checks/ convention
# plus this directory's own shared modules/Crest/tests/playwright/ suite.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CREST_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

export BASE_URL="${BASE_URL:-${CREST_SERVER_URL:-}}"

overall_failed=0

# Every test project in ONE dotnet process, via Crest.Tests.slnx.
#
# This used to be a loop of `dotnet test <csproj>`, one process per project. Each one
# re-restored and re-evaluated the whole project graph - the vendored workflow engine
# included - which cost minutes of MSBuild startup to execute well under a second of
# tests, and every consuming host that delegates here paid it.
#
# The solution is .slnx, not .sln, deliberately: `dotnet sln add` on a .sln mirrors each
# project's directory as a solution folder, and a folder named identically to a sibling
# project is MSB5004 ("two projects named X"), which this tree hits 11 times. .slnx keeps
# a flat project list and has neither problem. A new tests/ project is added to that
# solution rather than to a loop here.
#
# No --no-build: a host may or may not have built first, and this script is called
# standalone too, so it builds what it needs.
CREST_TESTS_SLNX="${CREST_DIR}/Crest.Tests.slnx"
echo "=== Crest C# tests (dotnet test Crest.Tests.slnx) ==="
if [ ! -f "${CREST_TESTS_SLNX}" ]; then
  echo "Crest.Tests.slnx not found at ${CREST_TESTS_SLNX}" >&2
  overall_failed=1
else
  # A test project that is on disk but not in the solution would be silently skipped, and
  # the run would still pass - the failure mode this whole arrangement has to rule out.
  # So the two are compared before running, and a mismatch fails the run loudly.
  unlisted=0
  while IFS= read -r -d '' csproj; do
    name="$(basename "${csproj}")"
    if ! grep -q "${name}" "${CREST_TESTS_SLNX}"; then
      echo "Test project not in Crest.Tests.slnx: ${csproj#${CREST_DIR}/}" >&2
      echo "  add it with: dotnet sln Crest.Tests.slnx add <path>" >&2
      unlisted=$((unlisted + 1))
    fi
  done < <(find "${CREST_DIR}" -path "*/tests/*" -name "*.csproj" \
             -not -path "*/obj/*" -not -path "*/bin/*" -print0 2>/dev/null | sort -z)

  if ((unlisted > 0)); then
    echo "${unlisted} test project(s) missing from the solution - not running a partial suite." >&2
    overall_failed=1
  elif ! dotnet test "${CREST_TESTS_SLNX}"; then
    overall_failed=1
  fi
fi

echo
echo "=== Crest shared Playwright suite ==="
if [ -f "${SCRIPT_DIR}/playwright/run-admin-suite.js" ]; then
  if ! node "${SCRIPT_DIR}/playwright/run-admin-suite.js"; then
    overall_failed=1
  fi
else
  echo "(shared run-admin-suite.js not found, skipping)"
fi

exit "${overall_failed}"
