# Tenant branding — display name and logos

**Status: not started.** Plan only. Nothing here is implemented.
A tenant presents ITS OWN identity, not the platform's: the login page greets
with the tenant's display name (today it says "Crest"), and the app
chrome uses tenant-uploaded logos wherever a logo appears. Managed by the tenant
admin from a **Branding** section at the TOP of the Design System page, above the
design-variables section. The design-variables section it sits above is documented in
[design-systems.md](design-systems.md).

Current state:

- A tenant's display name is the standard Orchard **`ISite.SiteName`**, already
  served by `GET api/crest/site` (`siteName`) and editable on the Crest Settings
  › General screen.
- The login page hardcodes the platform name:
  `Crest.Admin/wasm/Pages/Login.razor` line 16 —
  `<CrestText TextStyle="TextStyle.H3" Text="Crest" />`.
- The admin document title is also hardcoded:
  `Crest.Server/Components/App.razor` — `<title>Crest</title>`.
- `GET api/crest/site` requires `ManageSettings` — NOT usable from the anonymous
  login page. `GET api/crest/site/home` is the existing `[AllowAnonymous]`
  precedent on the same controller.
- The Design System page is `Crest.Admin/wasm/Pages/DesignSystem.razor`
  (route `/DesignSystem`, gated by `ManageSettings` in
  `CrestRoutePermissionProvider`).
- Tenant media already flows through Orchard Media elsewhere (icon overrides use
  it — the `icon-tenant-media` suite check is the precedent).

Per the standing rules: tenant-scoped settings live in **Orchard site settings**,
files live in **Orchard Media**, no parallel stores, no invented path literals.

## Display name

- [ ] **Use `ISite.SiteName` as the branding display name.** The branding display name IS `ISite.SiteName` — no second field. The Branding
  section edits it through the same site-settings pipeline the General settings
  screen uses (`ISiteService`), so the two screens can never disagree.
- [ ] **Add the anonymous branding read and render the site name on the login page.** Login page renders the site name instead of the literal. The name must be
  readable ANONYMOUSLY: add a small `[AllowAnonymous]` branding read
  (`GET api/crest/site/branding` on SiteController, or fold into `site/home`)
  returning only public-safe facts: site name + logo slot URLs. Nothing else
  from `ISite` leaks through it.
- [ ] **Use the site name in the document title.** `App.razor`'s `<title>` uses the site name too (SSR has `ISiteService`
  available directly — no endpoint needed there).

## Logos — named slots, size variants

- [ ] **Define the logo slots.** A logo is not one file: the app consumes specific renditions in specific
  places. Model as **named slots**, each with its intended size, consumed by a
  specific surface. Initial slot set (extend as surfaces appear):

  | Slot | Used by | Notes |
  | --- | --- | --- |
  | `login` | login page header | replaces/accompanies the display name |
  | `sidebar` | admin nav header / tenant chip | small, square-ish |
  | `favicon` | browser tab | ICO/PNG/SVG |
  | `email` | future mail templates | reserved, not consumed yet |

- [ ] **Store logo files in Orchard Media.** **Storage**: Orchard Media, under a tenant-relative media folder the service
  owns (resolved through `IMediaFileStore`, never a hand-typed absolute path).
  Upload goes through the existing Crest media adapter surface (same pipeline as
  icon tenant media), so Media permissions and validation apply unchanged.
- [ ] **Add `CrestBrandingSettings`.** **Settings**: a `CrestBrandingSettings` site-settings section
  (`ISite.Properties` via `ISiteService`, the standard custom-section pattern)
  holding `Slots: { name → media path }`. The setting stores the media PATH the
  tenant uploaded; URLs are resolved through the media file store at read time.
- [ ] **Read every slot through one shared accessor.** **"Shows up automatically"**: every consuming surface reads its slot through
  one shared accessor (client: a small `BrandingState` loaded from the anonymous
  branding read; server/SSR: the site settings directly). Slot empty → the
  surface falls back (login: site name text only; sidebar: current default;
  favicon: current static icon). Uploading a slot fills it everywhere on next
  render — no per-surface wiring beyond reading the slot.

## Branding section on the Design System page

At the top of `DesignSystem.razor`, before the design-variables section:

- [ ] **Company display name field.** **Company display name**: textbox bound to `SiteName`, saved through the
  site-settings pipeline (same permission the page already requires —
  `ManageSettings`).
- [ ] **Logo slot rows.** **Logo slots**: one row per slot — current image preview (or "not set"),
  upload control (through the media adapter), clear button. Slot metadata
  (label, expected size, where it's used) comes from the slot definitions so the
  UI never hardcodes surface knowledge.

## Permissions

- [ ] **Anonymous read.** Read (name + logo URLs): anonymous — it renders on the login page.
- [ ] **`ManageSettings` write.** Write: `ManageSettings` for the name (it IS a site setting); logo uploads
  additionally pass through Media's own permission checks.

## Verification

- [ ] **Unit tests.** Unit: branding settings round-trip; slot fallback rules.
- [ ] **Suite checks.** Suite: login page shows the site name (and logo once a probe uploads one);
  Design System page renders the Branding section, name edit persists and is
  reflected on `api/crest/site`; anonymous branding read returns only the
  public-safe fields.

## Out of scope / later

- [ ] **Per-theme, per-culture and dark-mode logos.** Per-theme or per-culture logos; dark-mode logo variants (add as extra slots
  when needed, the slot model already fits).
- [ ] **Site theme branding.** The Site (front-end) theme's branding — this plan covers the admin/login
  chrome; the public site pulls from the same settings when its theme work
  happens.
- [ ] **Email templates.** Email templates (slot reserved).
