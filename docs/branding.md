# Tenant branding — display name, logos, and the login page as a page

**Status: in progress, re-planned 2026-10-10 onto the display and query systems.** The
display name, the renditions, the settings group, the `site` query's branding and the
settings editor's branding group are done (2026-10-13, see What landed); the login page as a
designed page is not, because the admin and member shells do not render data-defined pages
yet (backlog › Decision 9). A tenant presents ITS OWN identity, not the platform's: the login page greets
with the tenant's display name (today it says "Crest"), and the app chrome uses
tenant-uploaded logos wherever a logo appears.

Three rulings shape the plan:

- **Brand details are tenant settings data.** The display name, the logo renditions and
  the design-system selection are part of the tenant's settings, wherever those are
  stored (the platform's site settings today, `ISite` and its `Properties` sections);
  there is no branding store beside them and no branding endpoint of its own.
- **Every surface reads branding through the `site` system query** and binds it to
  component slots ([queries.md](queries.md) › System queries and slot bindings); nothing
  reads settings or media paths directly for display. ("Slot" is the display system's word
  for a data binding on a component, so a logo placement is a **rendition** here.)
- **The login page is a page like every other page**: an entry in the route table, rendered
  as a tree of components from a template, with the anonymous route permission, overridable
  per tenant in the designer ([blazor-display.md](blazor-display.md) § 6a, § 10). It is not
  a hand-built Razor exception that fetches its own data.

Visual language (tokens, type scales, presets) is the design system's and is planned in
[design-systems.md](design-systems.md); this document covers the identity facts and the
pages that show them.

Current state:

- A tenant's display name is the platform's **`ISite.SiteName`**, served by
  `GET api/crest/site` (`siteName`, behind `ManageSettings`) and editable on the Settings
  › General screen.
- The login page is a module `@page` component (`InteractiveWebAssembly`, the auth-cookie
  render-mode rule) that greets with the site name read anonymously from
  `GET api/crest/site/branding` (`siteName` only). Both the hand-built page and the
  endpoint are what this plan replaces.
- The admin and member documents' `<title>` is the site name (`App.razor`, from
  `ISiteService` during SSR).
- Tenant media already flows through Media elsewhere (icon overrides use it; the
  `icon-tenant-media` suite check is the precedent).

## Display name

- [x] **`ISite.SiteName` is the display name.** No second field. Every editor of it goes
  through the one settings pipeline (`ISiteService`), so no two screens can disagree.
- [x] **The site name in the document title** (`App.razor`, SSR).
- [x] **The login page greets with the site name.** Done against the interim anonymous
  read; moves onto the `site` query with the page below.

## Logos — named renditions

A logo is not one file: the app consumes specific renditions in specific places.

- [x] **Define the renditions.** Model as **named renditions**, each with its intended
  size, consumed by a specific surface. Initial set (extend as surfaces appear):

  | Rendition | Used by | Notes |
  | --- | --- | --- |
  | `login` | the login page's header component | replaces or accompanies the display name |
  | `sidebar` | the admin and member layouts' nav header, the tenant chip in the switcher | small, square-ish |
  | `favicon` | the document head | ICO/PNG/SVG |
  | `email` | future mail templates | reserved, not consumed yet |

  Rendition definitions are registry entries (name, label, expected size, where it is
  used), so the settings editor and the `site` query never hard-code surface knowledge; a
  module that adds a surface registers the rendition it consumes.
- [x] **Files live in Media.** Under a tenant-relative media folder the service owns
  (resolved through `IMediaFileStore`, never a hand-typed absolute path). Upload goes
  through the existing media adapter surface (the same pipeline as icon tenant media), so
  Media permissions and validation apply unchanged.
- [x] **The branding settings group.** A settings section on the tenant's settings (the
  standard custom-section pattern over `ISite.Properties`) holding
  `Renditions: { name → media path }` beside the site name. The stored value is the media
  **path**; URLs are resolved through the media file store at read time, inside the `site`
  query. Per-theme, per-culture and dark-mode variants are extra renditions when needed;
  the model already fits.

## How surfaces read branding

The `site` system query answers `name`, `branding.{rendition}` (a resolved URL or empty)
and the design-system tokens; its anonymous-readable subset is decided by the query's
permission, so the login page reads it without a special endpoint. A surface binds a slot to
it: the layout's nav header binds `site › branding.sidebar`, the document head binds
`site › branding.favicon`, the login header binds `site › name` and
`site › branding.login`. An unset rendition comes back empty and the component falls back
(the login header shows the name as text, the sidebar shows the default mark, the head keeps
the static icon); setting one fills every surface on the next render because every surface
binds the same query. The client keeps the query's result in its session copy like any
other query result; SSR runs the same query in process.

- [x] **The `site` query exposes `name` and `branding.*`** from the settings group, with
  the anonymous subset (name, renditions) and the rest behind the data permission.
- [ ] **The layouts and the document head bind renditions** (partly: see What landed) through slots; the interim
  `GET api/crest/site/branding` read and `DisplayManager.Branding` go.

## What landed (2026-10-13)

- **Registry.** `BrandingRendition` (name, label, where it is used, intended size, accepted
  types, reserved) registered with `services.AddBrandingRendition(...)` into `BrandingOptions`
  (`Crest.Infrastructure.Abstractions/Settings/Branding.cs`). `Crest.Settings` registers
  `login`, `sidebar`, `favicon` and `email` (reserved).
- **Settings group.** `BrandingSettings { Renditions: name → media path }` on `ISite`
  (`site.Alter<BrandingSettings>`). `ResolveRenditions` turns it into one URL per registered
  rendition, empty when unset, through `IBrandingUrlResolver`, which Media implements over
  `IMediaFileStore.MapPathToPublicUrl`.
- **Files.** `api/crest/site/branding-settings` (`BrandingController`, `ManageSettings`;
  uploads also need `ManageMedia`, Media's allowed extensions and the upload pipeline) stores
  each upload as `_branding/{rendition}-{random}.{ext}` in the media store, so a new logo never
  comes from a cache, and deletes the file it replaced. It deletes only files in that folder.
- **`site` query.** The row is `Name` and `Branding` (rendition → URL or "") for any caller
  allowed to run it; `DefaultCulture` and `BaseUrl` only for a signed-in caller. Anonymous and
  Authenticated get `ExecuteApi_site` by default stereotype, and the site recipe grants it
  to Anonymous. *Interpretation to confirm:* "the rest behind the data permission" is read
  as "signed in".
- **Surfaces.** The SSR document head (`App.razor`) reads `site` in process
  (`SiteIdentity`): `<title>` is `site › name` and `<link rel="icon">` is
  `site › branding.favicon` on all three shells when set. The admin client reads
  `api/queries/site` (falling back to the interim `GET api/crest/site/branding`, which now
  answers from the same query, for a tenant whose Anonymous role predates the grant). The
  login page shows `branding.login`, or the name as text when it is unset. The title bar's
  tenant chip shows `branding.sidebar` before the title-bar avatar setting.
- **Editor.** Settings › General has a Branding card generated from the registry: the
  current image or "Not set", an upload, a clear (`BrandingSettingsGroup.razor`).
- **Tests.** `test/Crest.Tests/Settings/BrandingSettingsTests.cs`: round-trip, resolution
  and empty, the anonymous row holding only `Name` and `Branding`.

Left, and why: the interim endpoint and `AdminShellState.Branding` go with the hand-built
login page; the member layout's nav header has no logo spot yet (the shells work owns it);
the login page as a designed page waits on admin and member shells rendering data-defined
pages.

## The login page as a page

The login page is an entry in the route table with the properties only a page has (the
route, the shell bucket, `[AllowAnonymous]`, the auth-cookie render-mode rule declared by the
page, the document title) and a tree from a shipped template, overridable per tenant:

- [ ] **A shipped `Login` template** (Liquid, theme-qualified in the alternates chain:
  `Login`, `Login__{shell}`, the theme's own) composed of registered components: the
  branding header (slots bound to `site › name` and `site › branding.login`), the login form
  component (user name, password, remember me, the external-provider buttons from the
  `user` query's provider list), the links to registration and password reset where the
  shell offers them. The admin shell and the member shell each have their own login page
  record, so the surfaces stay separate by construction
  ([shells-and-themes.md](shells-and-themes.md) › The member portal).
- [ ] **The login form submits a workflow action**, the one sign-in operation behind the
  gate (`LoginService`'s `ILoginFormEvent` sequence as the action; see
  [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)).
  No component calls an endpoint of its own; a slot never binds a write.
- [ ] **Overrides, not forks.** A tenant restyles or rearranges the login page through the
  override model (instance and node overrides on the template's components; design-system
  tokens for the look); the route, the bucket and the anonymous permission are the page's
  and are not editable there. The design permission for the page is admins' by default.
- [ ] **The hand-built login page and `DisplayManager.Branding` are removed** once the
  template renders through the tree renderer; the registration and password-reset pages
  follow the same shape.

## Editing branding

Branding is edited where the tenant's settings are edited: the settings editor is itself a
page of components whose form binds the settings and saves through the settings action
behind `ManageSettings`. The branding group shows the display name (bound to `SiteName`)
and one row per registered rendition: the current image or "not set", an upload control
through the media adapter, a clear control. Rendition metadata comes from the registry.
The design-system editor ([design-systems.md](design-systems.md)) links to the group; it
does not host a hand-written Branding section of its own.

- [x] **The branding group in the settings editor**, generated from the settings section
  and the rendition registry.

## Permissions

- **Read**: the `site` query's anonymous subset is name and renditions, because the login
  page renders them; everything else in `site` needs the data permission.
- **Write**: `ManageSettings` for the name and the renditions (they are settings); logo
  uploads additionally pass Media's own checks. Editing the login page's template is the
  page's design permission; publishing it, the publish permission.

## Verification

- [ ] **Unit:** the settings section round-trips; the `site` query resolves renditions to
  URLs and answers empty for an unset one; the anonymous subset contains nothing else.
- [ ] **Suite:** the login page shows the site name, and the logo once a probe uploads
  one; the settings editor's branding group persists a name edit that `api/crest/site`
  reflects; a tenant override of the login template renders and the route and permission
  stay the page's.

## Out of scope / later

- [ ] **Site theme branding.** The public site's layout binds the same renditions when its
  theme work happens; nothing new to store. The site document's favicon already binds
  `site › branding.favicon`.
- [ ] **Email templates** (rendition reserved: `email` is registered and editable; no mail
  template reads it yet).
