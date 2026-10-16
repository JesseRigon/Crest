#!/usr/bin/env bash
# Shared dev tooling for hosts built on Grove. A host's dev script sources this file and
# passes its own solution, project, ports, data root and credentials; nothing here knows
# which host it serves.
#
#   source "${ROOT_DIR}/modules/Grove/tools/dev-lib.sh"
#
# What it provides:
#   Package feeds   source_stamp, evict_from_cache, pack_into_output, ensure_feed,
#                   restore_host (FORCE_RESTORE after a repack)
#   Local server    url_is_up, dev_process_limits, start_watch, stop_server, stop_all_watches
#   Test server     start_test_server, stop_test_server, wait_until_provisioned
#   Suite plumbing  check_solution_membership, is_legacy_script, run_host_suite
#
# Everything is a function; sourcing runs nothing. Functions return non-zero on failure
# rather than exiting, so the caller decides (set -e in the caller makes them fatal).

LOCAL_VERSION="${LOCAL_VERSION:-4.0.0-local}"
NUGET_CACHE="${NUGET_PACKAGES:-${HOME}/.nuget/packages}"
FORCE_RESTORE="${FORCE_RESTORE:-0}"

# One dev server per container: every host on Grove serves port 5010 and shares the
# container's memory, so a second watcher only doubles the memory and fights over the port.
WATCH_LOCK="${WATCH_LOCK:-/tmp/grove-dev-watch.lock}"
WATCH_PIDFILE="${WATCH_PIDFILE:-/tmp/grove-dev-watch.pid}"

require_dotnet() {
  if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET SDK is not installed in this environment." >&2
    return 1
  fi
}

# --- Package feeds ---------------------------------------------------------------
# Hosts consume Grove (and anything downstream) as packages packed into local output/
# folders, every pack the same LOCAL_VERSION. A repack therefore has to evict that version
# from the NuGet global cache, or restore keeps using the old copy.

# A stamp of a repository's state: its commit, its working-tree diff and its untracked files.
source_stamp() {
  local dir="$1"
  {
    git -C "${dir}" rev-parse HEAD
    git -C "${dir}" diff HEAD
    git -C "${dir}" ls-files --others --exclude-standard -z | (cd "${dir}" && xargs -0 -r sha1sum)
  } | sha1sum | cut -d' ' -f1
}

# Evict LOCAL_VERSION of every package in an output folder from the NuGet global cache.
evict_from_cache() {
  local nupkg id
  for nupkg in "$1"/*."${LOCAL_VERSION}".nupkg; do
    [ -e "${nupkg}" ] || continue
    id="$(basename "${nupkg}" ".${LOCAL_VERSION}.nupkg")"
    rm -rf "${NUGET_CACHE:?}/${id,,}/${LOCAL_VERSION}"
  done
}

# pack_into_output <repo dir> <solution> <output dir> [stamp]
# Packs the solution (Release, LOCAL_VERSION) into a fresh output folder and records the
# stamp beside the packages. Sets FORCE_RESTORE=1 so the next restore re-resolves.
pack_into_output() {
  local dir="$1" solution="$2" out="$3" stamp="${4:-}"
  echo "Packing ${dir}/${solution} into ${out} (${LOCAL_VERSION})..."
  evict_from_cache "${out}"
  rm -rf "${out}"
  mkdir -p "${out}"
  (cd "${dir}" && dotnet pack "${solution}" -m:"${DOTNET_MAXCPU:-3}" -c Release -p:Version="${LOCAL_VERSION}" -p:PackageVersion="${LOCAL_VERSION}" -o "${out}") || return 1
  evict_from_cache "${out}"
  if [ -n "${stamp}" ]; then
    echo "${stamp}" > "${out}/.source-stamp"
  fi
  FORCE_RESTORE=1
}

# ensure_feed <repo dir> <solution> <output dir> [force] [upstream stamp]
# Repacks when forced, when the repository changed since the last pack, or when the
# upstream stamp (the feed this one builds against) changed. Sets FEED_STAMP to the stamp
# recorded and FEED_PACKED to 1 when it packed, so a chain can pass both downstream:
#   ensure_feed "$GROVE" Grove.slnx "$GROVE_OUT"; ensure_feed "$ERP" Erp.slnx "$ERP_OUT" "$FEED_PACKED" "$FEED_STAMP"
ensure_feed() {
  local dir="$1" solution="$2" out="$3" force="${4:-0}" upstream="${5:-}"
  local stamp
  stamp="$(source_stamp "${dir}")${upstream:+-${upstream}}"
  FEED_STAMP="${stamp}"
  FEED_PACKED=0
  mkdir -p "${out}"
  if [ "${force}" = 1 ] || [ "$(cat "${out}/.source-stamp" 2>/dev/null)" != "${stamp}" ]; then
    pack_into_output "${dir}" "${solution}" "${out}" "${stamp}" || return 1
    FEED_PACKED=1
  fi
}

# restore_host <project or solution> <NuGet.config>
# After a repack the packages keep their version, so a no-op restore would not notice
# them; --force re-resolves against the fresh output folders.
restore_host() {
  local args=()
  if [ "${FORCE_RESTORE}" = 1 ]; then
    args+=(--force)
  fi
  dotnet restore "$1" --configfile "$2" "${args[@]}"
}

# --- Local server ------------------------------------------------------------------

# Up means the server answers at all: any HTTP status counts (a fresh tenant's home page may be a
# 404, or slow while its setup recipe runs), only "no answer" does not.
url_is_up() {
  local code
  code=$(curl -sS --max-time 30 -o /dev/null -w '%{http_code}' "$1" 2>/dev/null) || return 1
  [ "${code}" != "000" ]
}

# dev_process_limits: what every dev build and dev server runs under. MSBuild worker nodes
# and the compiler server exit with their build instead of idling; the host's heap is capped
# (GROVE_DEV_HEAP_LIMIT, default 6 GB) so a leak fails the app with an OutOfMemoryException
# in its log rather than exhausting the container; an edit hot reload cannot apply restarts
# the app, so a long session does not accumulate hot-reload deltas.
dev_process_limits() {
  export MSBUILDDISABLENODEREUSE=1
  export UseSharedCompilation=false
  export DOTNET_CLI_TELEMETRY_OPTOUT=1
  export DOTNET_GCHeapHardLimit="${GROVE_DEV_HEAP_LIMIT:-0x180000000}"
  export DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
}

# stop_all_watches: stops every `dotnet watch` in the container, whichever host started it,
# and the build servers. Exact process matching: only dotnet processes whose command line is
# a watch, never a shell that mentions one.
stop_all_watches() {
  local pid cmd
  for pid in $(pgrep -x dotnet) $(pgrep -f 'dotnet-watch\.dll' 2>/dev/null); do
    cmd=$(tr '\0' ' ' < "/proc/${pid}/cmdline" 2>/dev/null) || continue
    case "${cmd}" in
      *dotnet-watch.dll*|"dotnet watch "*) kill -9 "${pid}" >/dev/null 2>&1 || true ;;
    esac
  done
  rm -f "${WATCH_PIDFILE}"
}

# start_watch <project> [log file]
# Runs the host under `dotnet watch`, the only one in the container: it takes the watch lock,
# stops any other watcher and the app it supervised, applies dev_process_limits and records
# the watcher's PID in WATCH_PIDFILE. With a log file it runs in the background and returns;
# without one it runs in the foreground until stopped.
start_watch() {
  local project="$1" log="${2:-}"
  exec 9>"${WATCH_LOCK}"
  if ! flock -n 9; then
    echo "Another dev server is starting; try again in a moment." >&2
    return 1
  fi
  stop_all_watches
  dev_process_limits
  if [ -n "${log}" ]; then
    mkdir -p "$(dirname "${log}")"
    nohup dotnet watch --project "${project}" > "${log}" 2>&1 9>&- &
    echo $! > "${WATCH_PIDFILE}"
    echo "dotnet watch started (PID $(cat "${WATCH_PIDFILE}")); log: ${log}"
    flock -u 9
  else
    echo $$ > "${WATCH_PIDFILE}"
    flock -u 9
    exec dotnet watch --project "${project}"
  fi
}

# stop_server <host project file name, e.g. My.Host.csproj> <port> [root dir]
# dotnet watch supervises a child host process; the watcher is stopped first so it does not
# restart what is killed next. It ignores SIGTERM, so a watcher still there after a moment
# is killed outright - otherwise every stop leaves one behind, and stale watchers restart the
# host and fight over the port. Then the host binary, then anything still on the port.
stop_server() {
  local project="$1" port="$2" root="${3:-}"
  local name="${project%.csproj}"
  local watch_pattern="dotnet(-watch\.dll| watch) --project .*${name//./\\.}\.csproj"
  pkill -f "${watch_pattern}" >/dev/null 2>&1 || true
  for _ in 1 2 3 4 5; do
    pgrep -f "${watch_pattern}" >/dev/null 2>&1 || break
    sleep 1
  done
  pkill -9 -f "${watch_pattern}" >/dev/null 2>&1 || true
  if [ -n "${root}" ]; then
    pkill -f "${root}/bin/.*/${name}$" >/dev/null 2>&1 || true
  fi
  if command -v lsof >/dev/null 2>&1; then
    lsof -ti :"${port}" | xargs -r kill -9 >/dev/null 2>&1 || true
  fi
  rm -f "${WATCH_PIDFILE}"
  dotnet build-server shutdown >/dev/null 2>&1 || true
}

# --- Throwaway test server -----------------------------------------------------------
# A suite runs against its own server, never the dev server: a real `dotnet run --no-build`
# (hot reload does not move embedded resources and static assets, so a suite against a
# watch server cannot see data-file, recipe or migration changes) on its own port, with its
# own data root (Grove:DataRoot: tenants, tenant databases, the global store), so every run
# is a fresh install. The dev server keeps running beside it, untouched.

# start_test_server <project> <port> <data root> <log file> [url]
# Starts the server in its own process group and records it in TEST_SERVER_PGID and
# TEST_SERVER_DATA_ROOT for stop_test_server. Waits until the URL answers.
start_test_server() {
  local project="$1" port="$2" data_root="$3" log="$4" url="${5:-http://localhost:$2}"
  rm -rf "${data_root}"
  mkdir -p "${data_root}" "$(dirname "${log}")"
  dev_process_limits
  setsid dotnet run --no-build --project "${project}" -- \
    --urls "http://0.0.0.0:${port}" --Grove:DataRoot="${data_root}" > "${log}" 2>&1 &
  TEST_SERVER_PGID=$!
  TEST_SERVER_DATA_ROOT="${data_root}"
  echo "Waiting for the test server on ${url} (log: ${log})..."
  for _ in $(seq 1 120); do
    if url_is_up "${url}"; then
      return 0
    fi
    if ! kill -0 "${TEST_SERVER_PGID}" 2>/dev/null; then
      echo "The test server exited during startup; see ${log}" >&2
      return 1
    fi
    sleep 2
  done
  echo "Timed out waiting for the test server; see ${log}" >&2
  return 1
}

# stop_test_server: kills the test server's process group and removes its data root. Safe
# to call twice and from a trap (EXIT, INT, TERM), so a failed or interrupted run tears down.
stop_test_server() {
  if [ -n "${TEST_SERVER_PGID:-}" ]; then
    kill -TERM -- "-${TEST_SERVER_PGID}" >/dev/null 2>&1 || true
    sleep 1
    kill -KILL -- "-${TEST_SERVER_PGID}" >/dev/null 2>&1 || true
    TEST_SERVER_PGID=
  fi
  if [ -n "${TEST_SERVER_DATA_ROOT:-}" ]; then
    rm -rf "${TEST_SERVER_DATA_ROOT}"
    TEST_SERVER_DATA_ROOT=
  fi
}

# wait_until_provisioned <url> [timeout seconds]
# A fresh instance answers before its Default tenant is set up (the setup recipe runs on the
# first requests). Provisioned means the login page renders its form.
wait_until_provisioned() {
  local url="$1" timeout="${2:-300}" waited=0
  echo "Waiting for ${url} to finish provisioning..."
  while ((waited < timeout)); do
    if curl -fsS --max-time 5 "${url}/login" 2>/dev/null | grep -q 'UserName'; then
      return 0
    fi
    sleep 3
    waited=$((waited + 3))
  done
  echo "${url} did not provision within ${timeout}s." >&2
  return 1
}

# --- Suite plumbing ------------------------------------------------------------------

# check_solution_membership <solution> <search root>
# A test project on disk but absent from the solution would be silently skipped and the run
# would still pass. Compares the two and prints each missing project; returns the count
# (0 when complete), so `check_solution_membership ... || fail`.
check_solution_membership() {
  local solution="$1" root="$2" unlisted=0 csproj
  while IFS= read -r -d '' csproj; do
    if ! grep -q "$(basename "${csproj}")" "${solution}"; then
      echo "Test project not in $(basename "${solution}"): ${csproj#${root}/}" >&2
      echo "  add it with: dotnet sln $(basename "${solution}") add <path>" >&2
      unlisted=$((unlisted + 1))
    fi
  done < <(find "${root}" -path "*/tests/*" -name "*.csproj" \
             -not -path "*/obj/*" -not -path "*/bin/*" -print0 2>/dev/null | sort -z)
  if ((unlisted > 0)); then
    echo "${unlisted} test project(s) missing from the solution - not running a partial suite." >&2
  fi
  return "${unlisted}"
}

# is_legacy_script <path>
# True for a standalone Playwright script that is not yet a check. A glob over
# tests/playwright/*.js also matches checks/ and harness/ (modules that only export) and
# the suite entry points; those are excluded so nothing runs twice.
is_legacy_script() {
  case "$1" in
    */tests/playwright/checks/*|*/tests/playwright/harness/*) return 1 ;;
    */run-admin-suite.js|*/run-client-suite.js) return 1 ;;
    *) return 0 ;;
  esac
}

# run_host_suite <entry script> <base url> [output root]
# Runs a host's suite entry (a script calling the harness's runHostSuite) once: one browser,
# one login, every check. CHECK_FILTER and VERBOSE pass through from the environment.
run_host_suite() {
  local entry="$1" url="$2" out="${3:-}"
  BASE_URL="${url}" OUTPUT_ROOT="${out:-${OUTPUT_ROOT:-}}" node "${entry}"
}
