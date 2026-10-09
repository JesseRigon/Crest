# User settings: future features

One per-user preference exists beyond culture: **hidden admin-menu items**
(`CrestUserMenuPreferences.HiddenItemKeys` on `User.Properties`, self-service
`GET`/`PUT api/crest/navigation/me/hidden`, applied by `CrestAdminMenuBuilder` under the
tenant layout overlay — [docs/admin-menu.md](admin-menu.md) › Two hide layers). It is the pattern the landing-page idea below should copy:
a Crest-owned document on the user, one self-service endpoint, no admin override. The
rest of this doc is planning. This is a holding doc for small, per-user
preference features that don't belong in [docs/localization.md](localization.md) (culture/
language specifically) or a host's business-data plans. Add new self-contained user-preference ideas here rather than starting a new
plan doc per feature, unless one grows large enough to deserve its own file.

## Default admin landing page

When a user logs into the Crest admin, they currently always land on the dashboard
(`Dashboard.razor`, `@page "/Dashboard"` — the admin shell's own base-relative root,
served whenever a request resolves to `AdminOptions.AdminUrlPrefix` with no further
path segment; see `Home.razor`'s authenticated branch, which redirects there).

**Confirmed: the platform has no built-in equivalent of this.** Checked
`AccountBaseController.RedirectToLocal` (only honors a `returnUrl` query parameter or
falls back to the site root `~/`, never `/Admin`), `AdminOptions` (only exposes
`AdminUrlPrefix`, no landing-page concept), and `AdminController.Index()` (a bare
`return View()`, no redirect logic). No `UserOptions`/profile property anywhere
stores a preferred admin destination, per-user or per-tenant. So there's nothing to
mirror or integrate with — this would be a new feature.

- [ ] **Let each user pick their own default landing page.** Let each user pick their own default landing page from Crest's admin (e.g. "Content
  Items" instead of "Dashboard", or a specific saved view/filter once those exist).
  Stored per-user, read by `Home.razor`'s authenticated branch instead of the current
  hardcoded `Navigation.NavigateTo("Dashboard")`.

  Rough shape, following the same pattern [docs/localization.md](localization.md) already
  describes for per-user culture (`UserLocalizationSettings.Culture`,
  `CrestLocalizationController`'s self-service `GET`/`PUT` endpoint,
  `DisplayManager.Manifest`-carried value):
  - [ ] A new per-user setting, likely on a Crest-owned settings document (not
    `UserLocalizationSettings` — different concern) — e.g. `UserPreferencesSettings` or
    similar, storing a canonical route string.
  - [ ] Self-service `GET`/`PUT` endpoint scoped to the current user, same shape as
    `api/crest/localization/me`.
  - [ ] Surfaced through the manifest (`DisplayManager.Manifest`) so `Home.razor` can read
    it without an extra round trip on every login.
  - [ ] `Home.razor`'s authenticated branch reads it instead of the hardcoded
    `"Dashboard"` target — falls back to `Dashboard` if unset (the current, only
    option).
  - [ ] Needs validation: the stored route must still correspond to a real, currently
    reachable Blazor page (a saved preference pointing at a since-removed/renamed page,
    or a page the user's permissions no longer cover, must fall back to `Dashboard`
    gracefully rather than 404 or redirect-loop).

### Decisions needed

- [ ] **Decide where in the UI a user sets this.** Where in the UI does a user set this? A dropdown on `ProfileSettings.razor` next to
  the culture picker is the obvious place, consistent with existing patterns.
- [ ] **Decide whether this needs admin-level override/enforcement.** Does this need admin-level override/enforcement (e.g. can a tenant admin force all
  users to land on a specific page), or is it purely a personal preference with no
  tenant-wide default? Stock Orchard's absence of any concept here means there's no
  existing convention to follow either way — this is a from-scratch design decision.
- [ ] **Decide which pages it covers.** Should this cover only top-level nav destinations, or arbitrary deep-linkable pages
  (e.g. a specific content item's edit page)? Simpler to start with just top-level
  nav destinations (whatever's already enumerable from `DisplayManager.AdminMenu`)
  and expand later if needed.

## Other candidates for this doc

None yet — add here as they come up.
