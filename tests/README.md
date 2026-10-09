# Crest tests

This directory is the entrypoint for every test that lives inside the `Crest`
submodule when it runs standalone: `run-tests.sh` here runs everything underneath it. A
host that builds Crest's test projects into its own solution and starts its browser suite
with Crest's shared checks (see "Adding a browser check" below) does not call
`run-tests.sh`, which would run both a second time.

## Stack

Two different tools cover two different layers, deliberately not merged into one:

- **C# (unit/integration, server-side logic)** — [xUnit](https://xunit.net/) as the test
  runner, [`Verify`](https://github.com/VerifyTests/Verify) (`Verify.XunitV3`) for any
  snapshot/diff-style assertion (JSON, XML, HTTP responses, images — anything you'd
  otherwise hand-roll a baseline comparator for), and
  [`DotNetEnv`](https://github.com/tonerdo/dotnet-env) for loading `.env`-style
  credentials/ports/connection strings into test setup where needed. `NSubstitute` is
  the mocking library.
- **Browser E2E (admin UI)** — the existing Node/Playwright suite under
  `tests/playwright/` (this directory). Screenshot-diff checks use the harness at
  `tests/playwright/harness/screenshot-diff.js`: a committed `base/` baseline, a `new/`
  directory that's wiped and repopulated every run, and `UPDATE_BASE=1` to promote a
  fresh capture to the new baseline.

We adopted xUnit + Verify + DotNetEnv instead of building a bespoke JSON/HTML/API
baseline-diff harness — `Verify` already does exactly that (a committed `.verified.*`
file, a `.received.*` file on mismatch, a diff on failure) and is a maintained package,
so a custom harness would just be duplicate maintenance surface for no benefit.

Browser E2E deliberately stays on Node/Playwright rather than moving to
`Microsoft.Playwright` (the .NET binding) — the existing suite already works and is
wired into `dev.sh`; there's no reason to migrate it. `Microsoft.Playwright` remains an
option later if a concrete need for C#-side browser tests comes up, since it drives the
same underlying browser engine.

**Native (non-browser) UI testing is a known future need**, not yet implemented —
Avalonia is planned for this project, and Playwright (Node or .NET) cannot drive a
native desktop window. When that work starts, look at `Avalonia.Headless` (if the app
being tested is itself built in Avalonia) or FlaUI/WinAppDriver/Appium for OS-level
native UI automation.

## Layout and discovery

```
modules/Crest/
  tests/
    run-tests.sh                 <- the entrypoint host dev.sh scripts call
    playwright/                  <- shared browser E2E suite (existing)
      checks/                    <- one file per check, hardcoded into run-admin-suite.js
      harness/                   <- auth, health, instance bootstrap, screenshot-diff
      run-admin-suite.js
      run-client-suite.js
  Crest.Icons/
    tests/
      Crest.Icons.Tests/
        Crest.Icons.Tests.csproj   <- discovered and run by run-tests.sh
  Crest.<OtherProject>/
    tests/
      <OtherProject>.Tests/...                 <- same convention
```

`run-tests.sh` runs every test project in one `dotnet test` over
`Crest.Tests.slnx`, refusing a partial run when a `*/tests/*` project is
missing from that solution, then runs this directory's own shared Playwright suite.
Adding a new C# test project to any `Crest.*` subproject means: create it
under `<Subproject>/tests/<ProjectName>/`, and **exclude that `tests/` folder from the
parent project's own compile glob** — SDK-style projects (especially
`Microsoft.NET.Sdk.Razor` ones) default-glob every `.cs` file under the project
directory, so without an explicit exclusion the parent project will try to compile the
test files itself and fail on missing test package references. See
`Crest.Icons.csproj`'s `<Compile Remove="tests\**\*.cs" />` for the pattern
to copy.

## Why the host repos don't do this scan themselves

A product host has a flat `modules/*/tests/` layout (one `tests/` dir per module —
its business modules, `Crest`, any future module). `Crest` is the one
module that isn't flat — it's a submodule with its own nested subprojects, each
potentially owning a `tests/` dir. Rather than have every host repo's `dev.sh`
duplicate knowledge of that nested layout, `Crest` owns discovering and
running its own tests, and reports pass/fail back to whichever host invoked it. This
keeps `Crest.Crest.Host/dev/dev.sh` (which only ever needs to run this one
module's tests) a thin wrapper. A product host that registers every Crest test project in
its own solution and runs Crest's shared checks at the head of its own browser suite needs
no delegation at all.

## Adding a browser check

1. Write `tests/playwright/checks/<name>.js` (or `<Project>/tests/playwright/checks/<name>.js`)
   exporting `async function (page, { baseUrl, outputRoot, consoleErrors })` that returns
   one result `{ name, pass, message }` or an array of them. The page is already logged in
   as admin; `consoleErrors` holds the browser console errors raised during this check.
2. Crest-owned checks are registered once in `tests/playwright/run-admin-suite.js`'s
   `buildSharedAdminChecks()`, after any check they depend on (a feature-enable check
   precedes the checks that need the feature); a host's own suite list starts with those.
3. Wait for a **positive signal** and assert on it. `await x.waitFor({ timeout: 20000 })
   .catch(() => false)` on the happy path passes after twenty wasted seconds when the
   element never appears — that is the pattern behind every slow check in the table.
4. Screenshots compare against `tests/playwright/output/base/`
   through the harness's `screenshot-diff`. A deliberate UI change (a new menu entry)
   fails the diff; look at the `*.diff.png` in the run directory, and if it is the
   intended change promote `output/new/<name>.png` to `output/base/`.

## Credentials never live here

`run-tests.sh` and everything under this directory hold **no credentials, no `.env`
loading, and no server-lifecycle logic**. This submodule is checked out into multiple
independent host repos (a product host, `Crest.Crest.Host`, and potentially
others), each with its own environment, admin accounts, and database — a credential
baked in here would either leak between hosts or be wrong for at least one of them.

The boundary: `run-tests.sh` accepts `BASE_URL` as an already-resolved input (falling
back to `CREST_SERVER_URL` only as a convenience, not as its own
source of truth) and the Playwright harness (`harness/auth.js`) reads `ADMIN_USER`/
`ADMIN_PASSWORD`/`CLIENT_USER`/`CLIENT_PASSWORD` from the environment with generic
fallback defaults — it never hardcodes a real credential. Each host's own `dev/.env`
(e.g. the host's `dev/.env` `ORCHARD_AUTOSETUP_ADMIN_*` values) is what actually
supplies these at test time; that file is host-repo-local and never copied into or
read from this submodule.

If a future test needs a new credential or connection string, add the env var to the
*host's* `.env` and `dev.sh`, not to anything under `modules/Crest/`.
