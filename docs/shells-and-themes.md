# Shells and themes: admin, site, member — and theme compatibility

How a Crest tenant serves its three audiences as three shells, how a module contributes
pages to a shell, and how themes declare what they host so incompatible ones are caught.
The admin and site shells' hosting mechanics (endpoint gate, base paths, render modes) are
in [blazor-web.md](blazor-web.md); open work is under
[Still to build](#still-to-build).

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

`Crest.MemberTheme` (theme) + `Crest.MemberTheme.Client` (a razor class
library compiled into the one WASM app, exactly as Admin.Client and Site.Client are):

- the layout, header and navigation region;
- the member login and registration pages at the member base, so the staff and member
  login surfaces are separate **by construction** rather than by a runtime check
  (`MemberLoginSurfaceGate` becomes a backstop, not the gate);
- the active-organization switcher;
- "my account": profile, credentials, organization bindings, and the member's own view of
  their memberships;
- the route table and page-contribution seam modules plug into.

Modules contribute member pages the way they contribute admin pages, through a client
library whose `[Route]`-attributed components are scanned into a bucket - but a
**separate library per bucket**: admin pages in the module's `blazor-wasm/` library,
member pages in its `member-wasm/` library. `Crest.Members` keeps the portal's server
machinery (sessions, class stamping, org bindings, provisioning); its member-facing
client half (the shell seams below) is its `member-wasm` library, and its staff-facing
screens (membership tiers, perks, default groups) stay in `blazor-wasm`, in the admin
bucket.

### UI isolation: one bucket per assembly

A shell must never be able to render another shell's pages - a member must not reach a
back-office page by accident. Three layers make that hold, and each one alone is
insufficient:

1. **The API authorizes every read and write** - Orchard permissions, the member class
   ceiling, the active organization's binding. This is the security boundary; the other
   two keep the UI honest.
2. **The server route gate** scopes each URL to one bucket, so `/members/...` can only
   ever resolve a member page.
3. **Each shell's client router is handed only its own bucket's assemblies.** Blazor's
   `Router` routes every `[Route]` component in the assemblies it is given and has no
   per-type filter, so a bucket is an *assembly* property, never a per-page marker: an
   admin page and a member page in one assembly would both be routable in both shells
   on client-side navigation, past the server gate.

An assembly declares its bucket with `[assembly: CrestShell(...)]`
(`Crest.Components.Modules`). A module ships one client library per shell it contributes
to: `blazor-wasm/` for admin pages, `member-wasm/` (`CrestShells.Member`) for member pages,
`site-wasm/` (`CrestShells.Site`) for public pages. A module client library without the
attribute is admin, which is where module pages have always gone. All three shells are
built the same way: the theme client is the router's own assembly, and module libraries
of that shell reach it through the app's per-folder glob and are loaded with the shell.
An errant public link can therefore never render an admin or member page inside the
site, and the reverse holds for each shell. That one attribute is read everywhere the bucket
matters - the server's route-table providers, the endpoint bucket stamping, the lazy-module
generator and each shell's router - so they cannot disagree.

### The shell runtime is eager; module UIs load per shell

The WASM payload has one entry and one `Program.cs`, but what a browser *loads* is per
shell:

- **`Crest.Shell`** - eager, small: the shell runtime every shell needs
  (the lazy-module loader, per-bucket module manifests). Theme clients reference it; it
  references no theme.
- **Module client libraries** load per shell and per route: a member's browser loads
  member-bucket libraries only.
- **Seam implementations a module supplies** (`IMemberShellContext`,
  `IMemberAuthenticationService`) live in that module's lazily-loaded library, so they
  cannot be registered in the WASM container, which is sealed at `builder.Build()`
  before any lazy assembly loads. The shell loads its bucket's module libraries before
  it renders, activates the implementations against the container
  (`ActivatorUtilities`), and cascades them to its layout and pages.
- **Theme UIs load per shell.** Each theme client is lazy and loads when its shell's
  document boots: the document mounts one eager root, `Crest.Shell.CrestShellRoot`, naming
  the shell; it loads that shell's theme UI (`LazyModules.EnsureThemeAsync`) and renders
  the `Routes` component the theme declares (`[assembly: CrestShell(..., Routes = typeof(...))]`).
  The boot path and what every shell registers before the container is sealed - the HTTP
  pipeline, the API client, the admin shell state, the routing options - are the admin theme's
  **services**, `Crest.AdminTheme.Client.Services` (`[assembly: CrestEager]`, with
  `Crest.Shell`); `Program.cs` calls `ClientWebAssemblyHost` there and names no theme UI. The
  site and member theme clients have no services anything eager needs, so they load whole.
  The lazy-module generator makes every theme client a lazy root, names the eager roots by
  `[CrestEager]`, and emits each shell's theme (`themes:`); `LazyModules` keeps loaded theme
  UIs apart from modules (`LoadedThemes`, which the shell's component registry reads). Hosts
  reference `Crest.AdminTheme.Client.Services` directly. Between the first interactive render
  and the theme UI's arrival the root renders nothing.

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

## Shell dispatch

Dispatch is a registry of shells, with Site as the fallback:

- **`ShellId`** (`admin`, `site`, `member`: the string a client assembly declares with
  `[assembly: CrestShell(...)]`) is carried by `RouteComponentEntry` and by the
  `ThemeOwnerMetadata` endpoint marker. An id, not an enum, so a shell is a registration
  rather than a code change in every place that dispatches.
- **The registry** (`ShellRegistryOptions`, `Crest.Routing`) lists `ShellDescriptor`s: base
  path, shell, caller side, theme detector, authentication policy (login path, route
  authorization), not-found page, and whether pages carry the organization in their path.
  Crest registers admin, its login page and member from the tenant's configured paths
  (`ShellRegistryConfiguration`); a host adds its own with `services.Configure`.
  Every tenant reserves the member prefix, Members enabled or not.
- **`IShellSelector`** takes the request (not a path string) and picks an entry; path prefixes
  are the only rule it implements (`PathPrefixShellSelector`, longest base first), so another
  rule (a shell on its own hostname) can be added without reshaping it.
- **`ShellSelectionMiddleware`** (an `IStartupFilter`, before `UseRouting`) serves whichever
  entry the selector chose, the same way for each: the entry's theme check, the
  canonical-casing redirect, the access gate on the entry's side, the login redirect and
  (admin) route authorization, then `PathBase += shellBase; Path = @page literal`, and the
  Path-only strip for `_framework`/`_content`/`_blazor`/`api` requests. It stamps the shell
  (`BlazorHosting.ShellBucketItem`).
- **The site is resolved the same way.** There is **one routing system, on the Blazor side**,
  which forwards a URL to the legacy MVC shell when a legacy page owns it, until MVC is gone;
  endpoint routing is never left to referee between a Blazor catch-all page and MVC or static
  files. For a page request no entry claimed, the middleware matches the site route table
  (`SiteRouteResolver`): a module site page is served at its literal; a designed page or a
  content path becomes the content page, `/` — `ContentPage`'s only route, which reads the real path from `IShellRequestPath` (the middleware's
  stash) while prerendering and from the navigation manager once interactive, and which the
  site `Routes` renders for every URL it has no page for; **except** when a legacy endpoint
  owns the path, which passes through untouched — the forward to the legacy shell.
  `ILegacyRouteOwnership` answers that from the tenant's endpoint data source: every route
  endpoint without Blazor component metadata, minus the static-assets fallback, matched with
  the public template matcher plus the endpoint's required values (what makes a conventional
  area route per action) and inline constraints; compiled once, rebuilt on the data source's
  change token, prefiltered by the first literal segment. API paths and files are untouched.
  Legacy-frame requests (`?legacy-frame=1`, an iframe) bypass the middleware and reach MVC,
  since there is no Blazor catch-all. Not a `/{*Path}` catch-all on `ContentPage`: it wins
  over MVC routes and module assets, and no endpoint ordering convention reliably prevents
  that.
- **`RouteGateMatcherPolicy`** reads that stamp: that shell is valid, Site is the fallback — a
  decision made once per request, never a theme-id comparison (see
  [blazor-web.md](blazor-web.md) for why that distinction is load-bearing). It never referees
  Blazor against MVC.
- **`App.razor`** has a branch per shell (base href, head assets, `<MemberRoutes>` for the
  member shell), selected by the same stamp.

**Not a new WASM app.** There is one WASM app (the host app's WASM entry project) with one
`Program.cs`, one lazy-module graph and one asset set; the shells are libraries inside it.
A third shell therefore adds a theme, a client library, a bucket and a branch — not a
third build output.

### The tenant base and enabled modules

The server provides the tenant base; the client never derives it. `api/crest/routing` returns
`TenantBase` (the request's PathBase, which the Path-only API rewrite leaves at the tenant
layer); `RoutingOptions` keeps it and composes the admin, login and member paths on it, on the
client and in the server-side factory alike, and `ClientWebAssemblyHost.ShellOf` reads the
shell from the path after the tenant base. Each shell keeps its own login.

A disabled module is absent for that tenant. `IEnabledModuleAssemblies` (`Crest.Routing`,
cached for the shell's lifetime) maps each `*.BlazorWasm` to its module by the longest module id
(`ModuleClientAssemblies`) and keeps those whose module has an enabled feature; a library of a
module that is not installed is absent too (fail closed). The route table leaves their pages
out (admin URLs fall to the legacy frame, member URLs to not-found) and `RouteGateMatcherPolicy`
vetoes their endpoints (the site's pages, which endpoint routing matches directly). The browser
gets the list with the routing options (`api/crest/routing` › `EnabledModules`, anonymous like
the rest of that response, since the site shell needs it too) and `LazyModules.Restrict` loads
only those.

**Target: the page registry.** Today a page is described by the scanned route table, the
hand-written route permission providers and the admin menu's navigation providers. In the
target each page declares its route, shell, permission and menu placement once in Crest.Server's
page registry, and the route table, the route gate and the admin menu all derive from that entry
([architecture.md › One page registry](architecture.md#one-page-registry)). The shell
machinery in this document (buckets, selection, the gate per side) is unchanged by it.

### Auth-cookie pages render `InteractiveWebAssembly`

The admin login page is a deliberate exception to `InteractiveAuto`: under Auto's
first-visit server circuit a credential POST goes out on the server-side loopback
`HttpClient`, so the auth `Set-Cookie` lands in that handler and never reaches the
browser. The member login, registration and any other page that establishes a session hits
the identical trap, so the rule is general: **a page that sets an authentication cookie
renders `InteractiveWebAssembly`**, and the page declares it with
`[SetsAuthenticationCookie]` (`Crest.Components.Modules`). `App.razor` reads the attribute off
the routed page; no shell or path is special-cased. The admin login, the member login and the
member registration carry it.

## The member portal: login, sessions, sides

The member shell is the per-organization portal members sign in through: a vendor
organization has a vendor-flavoured portal on tenant A, a customer organization a
customer-flavoured one on tenant B, the same class of thing. Who a member is (bindings,
hierarchy, the member admin) is [parties.md › Members](parties.md#members-persons-who-act-for-an-organization);
what a member may do (the class, the ceiling, the caller) is
[workflows.md › Operations › The member caller](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery). This section is the sign-in side.

**Login surfaces are separate by construction.** A member signs in at the member portal
only, never at the tenant login; staff reach a member portal only through impersonation,
which starts from an authenticated staff session and never passes through a login. The
portal's login flow takes local tenant accounts and the external identity providers the
tenant configures (the stock `Crest.Users` external authentication features, Google, GitHub,
Microsoft, …, per tenant through site settings), with auto-provisioning creating the
member on first sign-in; the class and the binding are assigned by the portal's
registration flow, never by the raw external callback.

**The login-channel gate.** `ILoginFormEvent.ValidatingLoginAsync(IUser)` is the one veto
seam that can refuse an otherwise-valid credential, and it fires at all four sign-in points
(the password login and the three external-login paths), so one handler
(`MemberLoginSurfaceGate`) covers local and external logins. **The gate checks the side, from
the request**: the portal is a request the portal's endpoints marked
(`MemberPortalLoginContext`), an external-login callback carrying the mark on the external
cookie's properties (`MemberSessionKeys.PortalOrganization`, set by the portal's
external-login start, empty when no organization was named), or any sign-in made through the
member shell (the access gate's side); everything else is the tenant surface. It then reads
the bindings, the record of truth: the admin login takes a user holding a staff binding, the
portal a user holding at least one organization binding (to the named organization when one
is named). A dual user signs in at either. A portal refusal comes back to the portal's own
external-login page with the message, a tenant one to the tenant login.
`UserClassStampHandler` reads the same mark, so a user created by the platform's own
`RegisterAsync` or by external-login auto-registration during a portal request is stamped
member with the binding; a portal mark naming no organization may sign an existing member
in but never creates an account (no user exists outside an organization), and everything
else stays staff. Crest's JSON login
(`LoginService`) fires the standard `ILoginFormEvent` sequence exactly as the stock
controller does (LoggingIn → AuthenticateAsync → ValidatingLogin veto → SignIn → LoggedIn,
LoggingInFailed on every failure path), so moderation, e-mail confirmation, audit login
recording and the member gate all apply through the one seam; a veto's MVC result is
translated, never executed (redirects yield their path, TempData `error_*` messages fill
the 401 payload, anything else degrades to a generic refusal), and two-factor behaviour is
unchanged.

**One cookie, not two.** `SignInManager` is hard-wired to the application scheme; a second
cookie scheme would mean reimplementing two-factor, lockout, the security stamp and
external login. Portal sessions therefore ride the same per-tenant Identity cookie
(`orchauth_<tenant>`, Path deliberately unset) and are told apart by the request, not the
cookie. `AuthenticationProperties.Items` lives server-side when `CacheTicketStore` is
active and is the home for the session's last active organization and for the impersonator
identity; attaching items at sign-in needs `SignInManager.SignInAsync(user, properties,
method)`.

**The side comes from the request, not the user** (the caller of [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)). Pages carry the side in the path (the
admin prefix or the member prefix); API calls carry it in `X-Shell` (`admin` or `member`)
and, on the member side, the organization in `X-Org`, both sent by each shell's HttpClient
(`CrestShellContext`). A cookie-authenticated API call without `X-Shell` is malformed and
denied, never defaulted; a member-side request whose organization the user holds no
binding to is denied; a marker that disagrees with the shell the path selected is denied.
The caller is then built per request: on the staff side the user's tenant roles and no
organization; on the member side only the binding's roles with the class ceiling applied,
so the admin role of a dual user never reaches the portal. The active organization is not
written into the cookie, so switching never re-signs it and two tabs on two
organizations are independent; the session only remembers the last choice
(`MemberSessionService`) for a request that names none.

**Portal surfaces as built.** A `@page` component carrying `[AllowAnonymous]` yields
`RouteComponentEntry.AllowsAnonymous`, the shell middleware skips the login redirect and
the route gate for it, and `AdminRoutes` renders it shell-less. `api/crest/members/portal`:
`login` (organization optional; the first binding becomes the active organization),
`register` (creates the Person, wraps `RegisterAsync` so the tenant's registration
settings, moderation and e-mail confirmation apply, links person ↔ user, signs in with the
active organization), `external-providers`, `external-login` (the stock challenge with the
organization stamped on the external properties; the platform's own callback finishes it).
`forgot-password` and `reset-password` (the tenant's `ResetPasswordSettings` apply; the email
links to the member shell's reset page, never the tenant's; the answer never says whether
the account exists; only a member-bound account is sent a portal link). The portal login,
registration, password reset, external-login, organization picker, member home and account
pages live in `Crest.MemberTheme` (routes in `MemberRoutePaths`); `Crest.Members`'
`member-wasm` library supplies the shell's seams (`MemberAuthenticationService`,
`MemberShellContext`, which sets the shell context's organization).

**The organization is in the path.** Member pages are addressed as
`/{member prefix}/{organization slug}/…`, so a bookmark opens the right organization, two tabs
act in two organizations, and switching is a navigation. Pages marked
`[OrganizationFreePage]` (login, registration, password reset, external login, the picker,
not-found) are served at the member base instead, and **act in no organization: the page
request is admitted on the site side** (`ShellSelectionMiddleware.SideFor`), its caller a
visitor with no tenant rights, so a staff-only user or a member without bindings sees the page
rather than the member caller's "must name an organization" refusal; the page's own API calls - the sign-in, the session - declare the member side
themselves and are gated as member-side requests; the shell selector serves each kind only
in its place and the member router applies the same split on client-side navigation. The
slug is derived from the organization's name and is unique among the signed-in member's own
organizations (`MemberOrganizationDirectory`; an organization-free page's first segment and
the infrastructure segments are reserved, and the organization id always resolves too); the
server resolves it only among that user's bindings (`IShellOrganizationResolver`, implemented
by Members), so a path can never name an organization the user may not act for — anything
else is not found. A bare member base goes to the organization picker
(`MemberRoutePaths.Organizations`), which opens the only organization straight away. Slugs
are derived from the name, so renaming an organization changes its slug (the id still
resolves). The document base of an organization page is `{member base}/{slug}/`; the client reads the slug
off it (`CrestShellContext.OrganizationSlug`) and sends the matching organization as `X-Org`.

**One switcher, device-local tenants.** `CrestShellSwitcher` (`Crest.Components`) is the same
in the admin title bar and the member header: the accounts this device signed in to on top
(kept in the browser, `crestComponents.deviceAccounts`, one entry per tenant base and user,
added by signing in, kept after sign-out until removed), a separator, then the active
tenant's organizations from the user's own bindings (`AppManifest.Organizations` on the admin
side, the member session on the member side). Choosing a tenant opens it; choosing an
organization opens the member portal at that organization's path. No server data links
accounts across tenants: the app manifest lists only its own tenant, and no other tenant's
shell is started to look for a user of the same name. **E-mail is never an identity link.**
The device list is browser storage, which is per origin: tenants on one origin (addressed by
path) share it; tenants on subdomains of one site do not yet (that needs a cookie on the
parent domain); a tenant on an unrelated custom domain sees only its own.

**Impersonation.** Staff enter a member portal for support through an impersonation
session (`MemberImpersonationService`: the member principal signed in with the staff
identity kept in the authentication properties; the caller carries both as
`ImpersonatorUserId`). **Under one account:** the impersonated session acts on the member
side only — its admin-side requests are refused and the site sees a visitor without tenant
rights, so a dual user can be impersonated without their staff grants applying; stopping
(from the member shell's impersonation banner) restores the staff session exactly, its
persistence and its own last active organization included, and returns to the admin. Attribution splits deliberately:
everything **behaves** as the member (a generated invoice's CreatedBy is the member, the
scope is the member's organization, the portal renders as that member sees it) and the
**audit** records the staff user, "created by <staff> impersonating <member>", never the
member alone ([audit.md](audit.md)). The session carries both identities so no write path
knows about impersonation.

**The session cache and offline.** The client keeps one session copy
per (user, tenant, side, organization), the built caller and the access policies' verdict,
fed from the server's state cache under the tenant's permission version
([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) › Caller lifetime) and never authority. The session
expires as a whole after a tenant-set length (default 7 days); while online the copy
refreshes on a configurable interval and at once when a binding, role or policy write bumps
the version; offline, the client works from the last copy until the session expires.
Nothing is offline today; the rules that keep the door open: permissions on a device are
never authority (offline actions are queued as intents and re-authorized on reconnect); the
offline copy is a server-signed snapshot bound to a registered device, checked on sync,
revoked with a lost device; snapshot and data are encrypted at rest with a non-exportable
key; reads are the real exposure, so the server decides by permission at download time
what goes offline and sends only that; offline actions are audited on sync, flagged
offline. A refusing access policy removes access to that organization on the member side
only; it never signs the user out of the staff side.

**Tenant SSO stays staff-side.** Tenant SSO (the Default tenant as an OpenID Connect IdP, the
stock OpenId Server, Client and Validation features) is for tenant users across a person's
tenants; members are excluded, and a dual user gets it on the admin side only. A member of two
tenants has two member accounts.

## Account and protocol pages

The account and protocol pages a signed-in or signing-in user meets are shell pages
([blazor-display.md](blazor-display.md) › Rework plan, step 7):

- **Account security** is one shared component, `CrestAccountSecurity` (Crest.Components,
  `Account/`), over `api/crest/account` (Crest.Users' `AccountApiController`): change the
  password, change the email (where the tenant allows) and resend its confirmation, list,
  remove and link external logins (linking is a fetch for a five-minute data-protected ticket,
  then a navigation to the provider, so another site cannot start it). The admin profile
  settings page, the member account page and the site pages `/ChangePassword`,
  `/ChangeEmail`, `/ExternalLogins` show it - the paths the MVC screens had, so existing links
  keep working (`AccountPages` composes the server's links).
- **Email confirmation**: `/ConfirmEmail` (confirms once interactive, never in the prerender a
  mail scanner's GET would trigger) and `/ConfirmEmailSent`, site pages; the confirmation link
  and the login veto point there. The MVC resend action stays for the remaining MVC screens
  (two-factor, the legacy user list).
- **External sign-in completion**: when the platform's callback cannot finish on its own it
  redirects to the site pages `/ExternalLogin/Register` (a new account, with what the tenant's
  external registration settings still ask) or `/ExternalLogin/Link` (the existing account with
  the provider's email, by its password), which complete through
  `api/crest/account/external-login{,/register,/link}`; the MVC views and the manage actions are
  deleted, the rate limits kept on the same route names.
- **OpenID consent and logout confirmation**: the authorization and logout endpoints redirect
  to the site pages `/connect/consent` and `/connect/logout-confirm` with the request's
  parameters; the decision stays a plain form post back to the endpoint (OpenIddict answers in
  that request), with an antiforgery token from `api/crest/openid/consent`. Token and UserInfo
  stay protocol endpoints.
- **Error pages**: every shell has one (`CrestErrorPage`; admin, member - organization-free -
  and site pages at `/Error/{status}`). The status code pages re-execute inside the shell that
  answered (the shell stamp and base survive), so an admin URL's error shows in the admin
  document; API paths keep their bare status. The MVC Diagnostics controller and views are
  deleted. Not changed: an API refusal that redirects to `/Error/403` (Diagnostics
  re-execution of a page).
- Pages that make the browser receive a cookie (the account pages, the external sign-in
  completion, the consent page for its antiforgery cookie) declare `[SetsAuthenticationCookie]`,
  honoured in every shell, the site included.
- **Setup stays MVC**, the last island: an uninitialized tenant runs the
  minimum shell (the host's setup features, Crest.Setup and Recipes, no database), while the
  Blazor host - `MapRazorComponents`, the shell selection, the theme clients - is the `Crest`
  feature, which depends on database-backed modules and cannot run before setup. The native
  Tenants page sets a tenant up from the Default tenant without it. SiteTheme's
  `Views/Layout.cshtml` therefore stays too, with the account screens still on MVC (login,
  registration, password reset for staff, two-factor).

## Theme compatibility

Themes are **not** interchangeable. A Crest shell is a Blazor Web App document plus a
route bucket plus a design system; a module's pages are written against that. A host's
admin pages do not work under an arbitrary Orchard theme — the Crest admin theme (or a
direct descendant of it) is a dependency, and the system must say so instead of rendering
a broken shell.

### How a theme declares itself

Manifest tags are the mechanism (`admin` and `hidden` are read by Orchard itself):

- a **bucket** tag — `admin`, `member`, or neither (site). The site shell deliberately has
  no bucket tag of its own: "neither admin nor member" keeps every existing Orchard site
  theme valid with no manifest edit (`ThemeBuckets.GetBucket`);
- `crest-blazor` for a theme that hosts a Crest shell document. This one tag, read through
  the `BaseTheme` chain (`ThemeBuckets.IsCrestBlazorTheme`), is the only answer to "is this
  a Crest Blazor theme": the admin shell's detector and the compatibility check both ask
  it, so serving a shell and checking compatibility cannot disagree.

**Descendants count.** Orchard's `BaseTheme` chain is exactly "a direct child fork with
simple mods": a child theme inherits its parent's shapes and assets, so
`BaseTheme = Crest.AdminTheme` is a compatible admin theme. Every compatibility
check walks the `BaseTheme` chain, never a single id.

**Theme structure and branding.** A theme is page structures (templates, composites, zone
settings) with the design system (branding) layered on top, so either can be swapped or
imported alone; data themes and compiled module themes register through one mechanism; a
public marketplace carries data themes only ([blazor-display.md](blazor-display.md) ›
decision 8). The compatibility contract in this section (buckets, `crest-blazor`, contracts,
the two guards, the route gate) holds for the compiled part of a theme.

### How a module declares what it needs

A module's contract follows from what it ships: a module with a `blazor-wasm/` library
needs a `crest-blazor` admin theme, one with `member-wasm/` a `crest-blazor` member theme,
one with `site-wasm/` a `crest-blazor` site theme. The libraries are named for their module
(`Crest.Members.Member.BlazorWasm` belongs to `Crest.Members`), so every feature of that
module carries the contract with no declaration to keep in sync. A module whose needs go
beyond what it ships adds an `IShellContractProvider`. The check resolves each contract
against the active theme of that bucket and its `BaseTheme` ancestors. A client library
belongs to the module with the longest id its assembly name starts with
(`ShellCompatibilityService.OwningExtensionId`; `Crest.Members.Member.BlazorWasm` is
`Crest.Members`, not `Crest`), and `ShippedContracts` credits it only to that module. Contracts are per
module, not per contributed page set: a module's pages are written against one shell
contract in practice, and per-page granularity would multiply the declarations without
changing any answer.

Orchard has a site theme and an admin theme but no member theme, so the member theme is a
Crest site setting (`IMemberThemeService`), defaulting to `Crest.MemberTheme`.
Selecting a member theme sets that setting; it never touches the site theme.

### The two guards

Both directions are guarded, with deliberately different severity:

**Enabling a feature is refused** when its contract, or the contract of any feature it
would enable with it, is unsatisfiable: the active theme of the required bucket is
incompatible and no compatible one is active. The feature would
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
  and its own filter; the Member section appears only when an enabled feature ships member
  pages.
- Each theme shows its bucket, whether it is `crest-blazor`-capable, and its `BaseTheme`
  ancestry when it has one.
- Incompatible themes are listed, marked, and selectable only through the two-step
  confirmation above.

## The member base path

`MemberOptions.MemberUrlPrefix`, defaulting to **`members`**, tenant-settable exactly as
`AdminOptions.AdminUrlPrefix` is: one option, bound per tenant from the shell
configuration (`Crest_Member:MemberUrlPrefix`), read through `IOptions<MemberOptions>` and
never hardcoded at a call site. Every member URL is built
from it — navigation, the login surface, cross-shell links — so a tenant that changes the
prefix gets working links with no further edits, the way `AdminUrlPrefix "backoffice"`
already works for the admin shell.

The override is part of the feature, not a later addition: a default nobody can change is
not a tenant setting, and a path that only works at its default value is the bug this
option exists to prevent. Any consuming host should therefore set a non-default prefix on
at least one tenant, so both the default and the override are exercised.

The prefix is how the member shell is reached, for every tenant, with no alternative
mechanism.

## Still to build

- [ ] **`/{prefix}/_framework/dotnet.js` served with an empty MIME type** (S2, a bug; diagnosed
  after the display and operations work). Under a tenant addressed by path prefix the Blazor
  runtime file comes back with no content type, so the browser refuses it and the admin shell
  does not boot; the workflows isolation check signs in through `api/crest/auth/login` instead.
  The cause is unknown: something other than the static-asset endpoint answers (the per-tenant
  static files, the host's `UseStaticFiles`, or a fingerprint mismatch); `App.razor` also has no
  `<ImportMap/>`. Diagnose by comparing response headers on prefixed and unprefixed URLs, in
  development and published builds, then fix the actual cause; do not add `<ImportMap/>` and a
  `_framework/{**path}` tenant endpoint blind.
- [ ] **A live check of the per-shell theme boot path** after the repack.
- [ ] **Stable organization slugs**: a stored slug, owned by Parties, so renaming an
  organization does not change its paths.
- [ ] **The device account list across subdomains** of one site (a cookie on the parent
  domain).
- [ ] **The theme system reworked as structure plus design system** (blazor-display.md ›
  decision 8), a separate plan after the display system. The theme registry and the selection
  UI are reworked so the admin, site and member themes change only by the selections inside
  them, without losing anything of this document's shell injection model.
- [ ] **The page registry** (above): route table, route gate and admin menu derived from one
  entry per page.

## Later

- **The super tenant.** Cross-tenant read access for the Default tenant's users is a later
  design; the sanctioned mechanism is `IShellHost.GetScopeAsync` into the target tenant's shell
  (never shared-database queries), behind an opt-in per-type contract registry, fail-closed,
  read-only first, audited. Nothing depends on it.

## Open questions

- **Shells on their own hostnames, and on their own servers.** Today a tenant's admin, site
  and member shells share one hostname and one server, told apart by path. Open:
  - **Hostnames per shell** — a subdomain or custom domain for the public site, another for
    the member portal, another for the admin (e.g. `www.`, `members.`, `admin.`), alongside
    the tenant's own custom domains and subdomains.
  - **Separate systems per shell** — the admin, public and member shells on separate servers
    and back ends, still sharing data, permissions and users. The aim is isolation: an attack
    that takes down the public site must not take down the business's admin; and a business
    could run the admin on a local server sheltered from the internet without affecting the
    member portal or the public site.
  - What that requires of the shared pieces — the data store, the permission system (see
    [workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) › Decisions on running it on separate hardware), sign-in and sessions across
    hosts, and which shell owns which writes.
