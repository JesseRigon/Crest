# Tenant branding — display name, logos, and the login page as a page

A tenant presents ITS OWN identity, not the platform's: the login page greets with the tenant's
display name, and the app chrome uses tenant-uploaded logos wherever a logo appears. Branding
sits on the display and query systems:

- **Brand details are tenant settings data.** The display name, the logo renditions and
  the design-system selection are part of the tenant's settings, wherever those are
  stored (the platform's site settings, `ISite` and its `Properties` sections);
  there is no branding store beside them and no branding endpoint of its own.
- **Every surface reads branding through the `site` system query** and binds it to
  component slots ([queries.md](queries.md) › System queries and slot bindings); nothing
  reads settings or media paths directly for display. ("Slot" is the display system's word
  for a data binding on a component, so a logo placement is a **rendition** here.)
- **The login page is a page like every other page**: an entry in the route table, rendered
  as a tree of components from a template, with the anonymous route permission, overridable
  per tenant in the designer ([blazor-display.md](blazor-display.md) § 6a, § 10). It is not
  a hand-built Razor exception that fetches its own data.

Visual language (tokens, type scales, presets) is the design system's
([design-systems.md](design-systems.md)); this document covers the identity facts and the
pages that show them.

Today the login page is still a module `@page` component (`InteractiveWebAssembly`, the
auth-cookie render-mode rule) that shows `branding.login`, or the name as text when it is
unset; it becomes a designed page once the admin and member shells render data-defined pages
([blazor-display.md](blazor-display.md) › decision 9).

## Display name

- [x] **`ISite.SiteName` is the display name.** No second field. Every editor of it goes
  through the one settings pipeline (`ISiteService`), so no two screens can disagree. It is
  served by `GET api/crest/site` (`siteName`, behind `ManageSettings`) and edited on Settings ›
  General.
- [x] **The site name in the document title**: the SSR document head (`App.razor`) reads
  `site` in process (`SiteIdentity`), so `<title>` is `site › name` on all three shells.
- [x] **The login page greets with the site name**, through the `site` query.

## Logos — named renditions

A logo is not one file: the app consumes specific renditions in specific places.

- [x] **Named renditions**, each with its intended size, consumed by a specific surface:

  | Rendition | Used by | Notes |
  | --- | --- | --- |
  | `login` | the login page's header component | replaces or accompanies the display name |
  | `sidebar` | the admin and member layouts' nav header, the tenant chip in the switcher | small, square-ish |
  | `favicon` | the document head | ICO/PNG/SVG |
  | `email` | mail templates | reserved, not consumed yet |

  Rendition definitions are registry entries, so the settings editor and the `site` query
  never hard-code surface knowledge; a module that adds a surface registers the rendition it
  consumes. `BrandingRendition` (name, label, where it is used, intended size, accepted types,
  reserved) is registered with `services.AddBrandingRendition(...)` into `BrandingOptions`
  (`Crest.Infrastructure.Abstractions/Settings/Branding.cs`); `Crest.Settings` registers the
  four above.
- [x] **Files live in Media**, in a tenant-relative folder the service owns (resolved through
  `IMediaFileStore`, never a hand-typed absolute path), uploaded through the media pipeline so
  Media permissions and validation apply unchanged. `api/crest/site/branding-settings`
  (`BrandingController`, `ManageSettings`; uploads also need `ManageMedia`, Media's allowed
  extensions and the upload pipeline) stores each upload as `_branding/{rendition}-{random}.{ext}`,
  so a new logo never comes from a cache, and deletes the file it replaced. It deletes only
  files in that folder.
- [x] **The branding settings group.** `BrandingSettings { Renditions: name → media path }` on
  `ISite` (`site.Alter<BrandingSettings>`), beside the site name. The stored value is the media
  **path**; `ResolveRenditions` turns it into one URL per registered rendition at read time,
  empty when unset, through `IBrandingUrlResolver`, which Media implements over
  `IMediaFileStore.MapPathToPublicUrl`. Per-theme, per-culture and dark-mode variants are extra
  renditions when needed.

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

- [x] **The `site` query exposes `name` and `branding.*`.** The row is `Name` and `Branding`
  (rendition → URL or "") for any caller allowed to run it; `DefaultCulture` and `BaseUrl` only
  for a signed-in caller. Anonymous and Authenticated get `ExecuteApi_site` by default
  stereotype, and the site recipe grants it to Anonymous.
- [x] **The document head** binds `site › branding.favicon` on all three shells when set.
- [x] **The title bar's tenant chip** shows `branding.sidebar` before the title-bar avatar
  setting.
- [ ] **The layouts bind renditions through slots.** The admin client reads `api/queries/site`,
  falling back to the interim `GET api/crest/site/branding` (which answers from the same query)
  for a tenant whose Anonymous role predates the grant. The interim endpoint and
  `AdminShellState.Branding` go with the hand-built login page. The member layout's nav header
  has no logo spot yet (shells work, [shells-and-themes.md](shells-and-themes.md)).

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
- [ ] **The hand-built login page and `AdminShellState.Branding` are removed** once the
  template renders through the tree renderer; the registration and password-reset pages
  follow the same shape.

## Editing branding

Branding is edited where the tenant's settings are edited: the settings editor is itself a
page of components whose form binds the settings and saves through the settings action
behind `ManageSettings`. The branding group shows the display name (bound to `SiteName`)
and one row per registered rendition: the current image or "not set", an upload control
through the media adapter, a clear control. Rendition metadata comes from the registry.
The design-system editor ([design-systems.md](design-systems.md)) links to the group; it
does not host a Branding section of its own.

- [x] **The branding group in the settings editor**: Settings › General has a Branding card
  generated from the registry (`BrandingSettingsGroup.razor`).

## Permissions

- **Read**: the `site` query's anonymous subset is name and renditions, because the login
  page renders them; everything else in `site` needs the data permission.
- **Write**: `ManageSettings` for the name and the renditions (they are settings); logo
  uploads additionally pass Media's own checks. Editing the login page's template is the
  page's design permission; publishing it, the publish permission.

## Verification

- [x] **Unit** (`test/Crest.Tests/Settings/BrandingSettingsTests.cs`): the settings section
  round-trips; the `site` query resolves renditions to URLs and answers empty for an unset one;
  the anonymous row holds only `Name` and `Branding`.
- [ ] **Suite:** the login page shows the site name, and the logo once a probe uploads
  one; the settings editor's branding group persists a name edit that `api/crest/site`
  reflects; a tenant override of the login template renders and the route and permission
  stay the page's.

## Open questions

- **"The rest behind the data permission"** is implemented as "signed in" (`DefaultCulture` and
  `BaseUrl` for any signed-in caller). Whether a narrower data permission is meant is unconfirmed.

## Later

- [ ] **Site theme branding.** The public site's layout binds the same renditions; nothing new
  to store. The site document's favicon already binds `site › branding.favicon`.
- [ ] **Email templates** read the `email` rendition (it is registered and editable; the
  account email templates do not read it yet).
