# User settings: future features

One per-user preference exists beyond culture: **hidden admin-menu items**
(`GroveUserMenuPreferences.HiddenItemKeys` on `User.Properties`, self-service
`GET`/`PUT api/grove/navigation/me/hidden`, applied by `AdminMenuBuilder` under the
tenant layout overlay — [docs/admin-menu.md](admin-menu.md) › Two hide layers). It is the pattern the landing-page idea below follows:
a Grove-owned document on the user and one self-service endpoint. The
rest of this doc is planning: a holding doc for small, per-user preference features that
don't belong in [docs/localization.md](localization.md) (culture and language) or a host's
business-data plans. Add new self-contained user-preference ideas here rather than starting a new
plan doc per feature, unless one grows large enough to deserve its own file.

## Default admin landing page

When a user logs into the Grove admin, they always land on the dashboard
(`Dashboard.razor`, `@page "/Dashboard"` — the admin shell's own base-relative root,
served whenever a request resolves to `AdminOptions.AdminUrlPrefix` with no further
path segment; see `Home.razor`'s authenticated branch, which redirects there).

**The platform has no built-in equivalent:**
`AccountBaseController.RedirectToLocal` only honors a `returnUrl` query parameter or
falls back to the site root `~/`, never `/Admin`; `AdminOptions` only exposes
`AdminUrlPrefix`; `AdminController.Index()` is a bare `return View()`. No `UserOptions`/profile property anywhere
stores a preferred admin destination, per-user or per-tenant, so this is a new feature.

- [ ] **Let each user pick their own default landing page** from Grove's admin (e.g. "Content
  Items" instead of "Dashboard", or a specific saved view/filter once those exist).
  Stored per-user, read by `Home.razor`'s authenticated branch instead of the current
  hardcoded `Navigation.NavigateTo("Dashboard")`.

  Rough shape, following the same pattern [docs/localization.md](localization.md) already
  describes for per-user culture (`UserLocalizationSettings.Culture`,
  `LocalizationController`'s self-service `GET`/`PUT` endpoint,
  `DisplayManager.Manifest`-carried value):
  - [ ] A new per-user setting, likely on a Grove-owned settings document (not
    `UserLocalizationSettings` — different concern) — e.g. `UserPreferencesSettings` or
    similar, storing a canonical route string.
  - [ ] Self-service `GET`/`PUT` endpoint scoped to the current user, same shape as
    `api/grove/localization/me`.
  - [ ] Surfaced through the manifest (`DisplayManager.Manifest`) so `Home.razor` can read
    it without an extra round trip on every login.
  - [ ] `Home.razor`'s authenticated branch reads it instead of the hardcoded
    `"Dashboard"` target — falls back to `Dashboard` if unset (the current, only
    option).
  - [ ] Needs validation: the stored route must still correspond to a real, currently
    reachable Blazor page (a saved preference pointing at a since-removed/renamed page,
    or a page the user's permissions no longer cover, must fall back to `Dashboard`
    gracefully rather than 404 or redirect-loop).

### Open questions

- **Where in the UI a user sets this.** A dropdown on `ProfileSettings.razor` next to the
  culture picker is the obvious place, consistent with existing patterns.
- **Whether a tenant admin can override or enforce it** (e.g. force all users to land on a
  specific page), or whether it is purely a personal preference with no tenant-wide default.
  The platform has no convention here either way.
- **Which pages it covers.** Only top-level navigation destinations (whatever the admin menu
  already enumerates), or arbitrary deep-linkable pages (e.g. a specific content item's edit
  page)? Starting with top-level destinations is simpler.

## Other candidates for this doc

None yet — add here as they come up.
