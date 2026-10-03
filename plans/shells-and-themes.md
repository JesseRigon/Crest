# Shells and themes: admin, site, member — and theme compatibility

## Status

**Planning.** The admin and site shells are built and documented in
[docs/BlazorWeb.md](../docs/BlazorWeb.md); the member shell, the generalized shell
dispatch, and the theme-compatibility contracts below are not. The member portal exists
today only as `[AllowAnonymous]` pages in the Members module borrowing another shell's
client — this plan replaces that arrangement.

## Three shells

A Crest tenant serves three audiences, and each is a shell: its own base path, document,
route bucket, authentication behaviour and navigation model.

| | Admin | Site | Member |
| --- | --- | --- | --- |
| Audience | staff (tenant users) | anonymous public | members (organization-bound users) |
| Entry | the tenant's admin prefix | the tenant root | the tenant's member prefix |
| Authentication | required; unauthenticated → staff login | none | required; unauthenticated → **member** login |
| Navigation | `AdminMenu`, filtered by permission | site content / menus | the member's entitlements, scoped to the active organization |
| Data scope | tenant-wide per role and hierarchy | public content | one organization, fail-closed |
| Rendering | SSR + `InteractiveAuto` | content pages | app-like, `InteractiveAuto` |

The member shell is **first-class but optional**: it ships with Crest, registers in the
theme registry, appears in the theme-selection UI with its own active-theme card and
filter, and is present only when the Members feature is enabled.

### What the member shell owns

`OrchardCore.Crest.Member` (theme) + `OrchardCore.Crest.Member.Client` (a razor class
library compiled into the one WASM app, exactly as Admin.Client and Site.Client are):

- the layout, header and navigation region;
- the member login, registration, password-reset and external-login pages at the member
  base, so the staff and member login surfaces are separate **by construction** rather
  than by a runtime check (`MemberLoginSurfaceGate` becomes a backstop, not the gate);
- the active-organization switcher;
- "my account": profile, credentials, organization bindings, and the member's own view of
  their memberships;
- the route table and page-contribution seam modules plug into.

Modules contribute member pages the way they already contribute admin pages: a
`blazor-wasm` library whose `[Route]`-attributed components are scanned into the member
bucket, lazy-loaded per route. `Crest.Members` keeps the portal's server machinery
(sessions, class stamping, org bindings, provisioning) and contributes only generic
member pages; its staff-facing screens (membership tiers, perks, default groups) stay in
the **admin** bucket, where they belong.

### One theme, per-member authorization

The member theme is **shared by every organization-bound user in the tenant**, exactly as
the admin theme is shared by every staff user: one theme, one active-theme setting, one
set of shapes and assets, chosen per tenant.

**Data access and permissions are not shared.** Two members signing into the same member
theme may see entirely different data and hold entirely different permissions, scoped to
the organization they are acting in and to their member class. The theme decides how a
page looks; it decides nothing about what the page is allowed to read.

The practical consequence is that **no authorization may be expressed in the theme or in a
component**. A shared theme that hid a nav entry or a panel would be doing access control
in markup that every member downloads — the same WASM assembly, the same lazy-loaded
route. Every scope decision belongs server-side, in the query, keyed on the member's
active organization and class, and fails closed; the UI renders what the server returned
and nothing more. Hiding a control in the client is a presentation choice layered on top
of a server decision that has already been made, never the decision itself.

## Generalizing shell dispatch

The existing mechanism is two-valued in three places. Each becomes a registry of shells,
with Site as the fallback:

- **`RouteBucket`** (`Admin`/`Site`) → a shell id carried by `RouteComponentEntry` and by
  the `ThemeOwnerMetadata` endpoint marker.
- **`RouteGateMatcherPolicy`** reads "admin marker present → Admin valid, else Site
  valid". It becomes "the shell this request was dispatched to → that bucket is valid,
  Site is the fallback bucket" — still a decision the middleware made once per request,
  never a theme-id comparison (see [docs/BlazorWeb.md](../docs/BlazorWeb.md) for why that
  distinction is load-bearing).
- **`BlazorAdminThemeMiddleware`** + `BlazorAdminThemeOptions` + `IBlazorAdminThemeDetector`
  know one shell base pair (`AdminPath`/`LoginPath`). They become a shell **selector** over
  a registered list of shells, each with its base path, bucket, theme detector and
  authentication policy. The `PathBase += shellBase; Path = @page literal` move,
  the `IStartupFilter` registration (it must run before `UseRouting`), and the Path-only
  strip for `_framework`/`_content`/`_blazor`/`api` requests are unchanged — a third shell
  reuses all of it.
- **`App.razor`** gains a third branch (base href, head assets, `<MemberRoutes>`), selected
  by the same `HttpContext.Items` marker.

**Not a new WASM app.** There is one WASM app (`OrchardCore.Crest.Client`) with one
`Program.cs`, one lazy-module graph and one asset set; the shells are libraries inside it.
A third shell therefore adds a theme, a client library, a bucket and a branch — not a
third build output.

### Auth-cookie pages render `InteractiveWebAssembly`

The admin login page is already a deliberate exception to `InteractiveAuto`: under Auto's
first-visit server circuit a credential POST goes out on the server-side loopback
`HttpClient`, so the auth `Set-Cookie` lands in that handler and never reaches the
browser. The member login, registration and any other page that establishes a session hits
the identical trap, so the rule is general: **a page that sets an authentication cookie
renders `InteractiveWebAssembly`**, declared by the page, not special-cased per shell.

### The selector takes the request

The shell selector matches on the **request**, not on a path prefix string, and keeps its
matching rules in options. Path prefixes are the only rule it implements: every shell is
selected by its prefix, and the site shell is the fallback when none matches.

Taking the request rather than a prefix costs nothing now and is what keeps other matching
rules — a shell on its own hostname, say — addable later without reshaping the selector.

## Theme compatibility

Themes are **not** interchangeable. A Crest shell is a Blazor Web App document plus a
route bucket plus a design system; a module's pages are written against that. A host's
admin pages do not work under an arbitrary Orchard theme — the Crest admin theme (or a
direct descendant of it) is a dependency, and the system must say so instead of rendering
a broken shell.

### How a theme declares itself

Manifest tags are already the mechanism (`admin` and `hidden` are read today):

- a **bucket** tag — `admin`, `member`, or neither (site);
- `crest-blazor` for a theme that hosts a Crest shell document. `IBlazorAdminThemeDetector`
  already accepts "the theme id matches, or the theme carries the `blazor` tag"; this
  formalizes and extends it per bucket.

**Descendants count.** Orchard's `BaseTheme` chain is exactly "a direct child fork with
simple mods": a child theme inherits its parent's shapes and assets, so
`BaseTheme = OrchardCore.Crest.Admin` is a compatible admin theme. Every compatibility
check walks the `BaseTheme` chain, never a single id.

### How a module declares what it needs

A module states its required shell contract — "my admin pages need a `crest-blazor` admin
theme", "my member pages need a `crest-blazor` member theme" — and the check resolves
against the active theme of that bucket and its `BaseTheme` ancestors.

### The two guards

Both directions are guarded, with deliberately different severity:

**Enabling a feature is refused** when its contract is unsatisfiable: the active theme of
the required bucket is incompatible and no compatible one is active. The feature would
render nothing usable, and the admin has not yet asked for that outcome. The refusal names
the module, the required contract and the active theme.

**Changing a theme warns, twice, and proceeds.** The active theme is the admin's own
decision about their site; refusing it would strand a tenant whose theme was removed or
whose fork is unrecognized. So `ThemesController.SetCurrent` (and the reset endpoints) run
a compatibility check first and, when enabled features declare contracts the new theme
does not satisfy:

1. the first dialog states that this is a **breaking change**, lists every affected
   feature by name and what will break;
2. the second requires an explicit, separate confirmation ("I understand this will break
   *n* features") before the change is sent.

The API carries this honestly rather than trusting the UI: the POST is rejected with the
incompatibility report unless it carries an explicit acknowledgement flag, so a scripted
or recipe-driven change gets the same gate and the same report. After the change, the
affected features are listed as **incompatible** in the features and themes UI with the
same text, so the state is visible rather than merely having been warned about once.

### Theme-selection UI

- Three sections — Site, Admin, Member — each with its own active-theme card at the top
  and its own filter; the Member section appears only when Members is enabled.
- Each theme shows its bucket, whether it is `crest-blazor`-capable, and its `BaseTheme`
  ancestry when it has one.
- Incompatible themes are listed, marked, and selectable only through the two-step
  confirmation above.

## The member base path

`MemberOptions.MemberUrlPrefix`, defaulting to **`members`**, tenant-settable exactly as
`AdminOptions.AdminUrlPrefix` is: one option, post-configured per tenant, read through
`IOptions<MemberOptions>` and never hardcoded at a call site. Every member URL is built
from it — navigation, the login surface, cross-shell links — so a tenant that changes the
prefix gets working links with no further edits, the way `AdminUrlPrefix "backoffice"`
already works for the admin shell.

The override is part of the feature, not a later addition: a default nobody can change is
not a tenant setting, and a path that only works at its default value is the bug this
option exists to prevent. Any consuming host should therefore set a non-default prefix on
at least one tenant, so both the default and the override are exercised.

The prefix is how the member shell is reached, for every tenant, with no alternative
mechanism in this plan.

## Open questions

- **Does the site shell need a bucket tag** of its own for symmetry, or is "neither admin
  nor member" good enough? The latter keeps existing site themes valid with no manifest
  edit, which argues for it.
- **Contract granularity:** per module, or per contributed page set? Per module is simpler
  and probably enough.

## Phases

- [x] 0. This plan; the bucket/contract vocabulary agreed, and the member base path settled
      (`members` by default, tenant-settable).
- [ ] 1. Generalize shell dispatch: shell registry, bucket as a shell id, selector over
      the registered shells, `App.razor` branch per shell, the auth-cookie render-mode
      rule declared by pages. Admin and Site behaviour unchanged, proven by the existing
      suite.
- [ ] 2. `OrchardCore.Crest.Member` + `Member.Client`: layout, navigation, login and
      account pages, the org switcher, the page-contribution seam. Members' portal pages
      move into it; its staff pages stay in the admin bucket.
- [ ] 3. Theme compatibility: manifest tags, `BaseTheme`-walking detector, module
      contracts, the feature-enable refusal, the two-step theme-change confirmation with
      its API acknowledgement, and the incompatibility badges.
- [ ] 4. Theme-selection UI: three sections, filters, the Member section gated on the
      Members feature.
