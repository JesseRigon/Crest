# Admin menu layout

The admin menu is a view over the page registry (§ The page registry): its default tree is the
admin pages modules declare, the tenant's edits are a layer over it, and each user can hide
items for themselves. The tenant's edits made in the Admin Menus page (renames, hides, moves,
icons, custom items and links, separators) are stored in the tenant document store as layout
overrides, keyed by each item's stable entry id.

Primary navigation component settings, such as collapse behavior, tier spacing, generated separators, and tier backgrounds, are tenant site settings. They are edited from the same Admin Menus page for UX, but they are not part of the menu layout overlay JSON.

## Recipe import

Hosts can import primary navigation overrides from a recipe with the `GroveAdminMenuLayout` step:

```json
{
  "name": "GroveAdminMenuLayout",
  "file": "grove-admin-menu-layout.json"
}
```

The `file` value is resolved relative to the recipe file. Grove registers this recipe step, but it does not add the step to any host recipe automatically.

## UI export

The Admin Menus page shows an `Export JSON` button on the built-in Primary Navigation. The button exports the current tenant layout to:

```text
<host content root>/recipes/grove-admin-menu-layout.json
```

In a normal source checkout, the host content root is the host app repository folder, so the default export lands in the host app's `recipes` folder and can be versioned with the host recipe.

The export endpoint is enabled automatically in `Development`. In other environments, the host must opt in:

```json
{
  "Grove": {
    "AdminMenuLayoutExport": {
      "Enabled": true
    }
  }
}
```

The endpoint still requires the current user to have Orchard's admin menu management permission.

## The page registry

Part of the tenet
[architecture.md › Modules declare, Grove.Server registers and serves](architecture.md#modules-declare-groveserver-registers-and-serves).
Each page declares once its routes, its shell, its permissions and its menu placement (parent,
caption, position, icon). The route table, the route gate and the admin menu's default tree
are all read from that one entry; there is no other list of pages.

**Contracts** (`Grove.Server.Abstractions`, `Routing`): `PageEntry` (a stable `Id`, the
`Shell`, the shell-relative `Routes`, the `Component` that renders it, `Permissions` any one of
which admits a caller, `ClientOnly`, `AllowsAnonymous`, the `Menu` placement as a `PageMenu`),
`IPageSource` (declared entries from module code), `IDynamicPageSource` (entries generated from
tenant data), `IPageRegistry` and `PageSet` (lookup by id, route matching), `KindPageSource<TKind>`
(a kind registry's pages) and the `PageEntries` shorthands. `RouteTemplates` is the one template
grammar the table and the gate match with. Registration: `services.AddPageSource<T>()`,
`services.AddDynamicPageSource<T>()`.

**How a page declares itself.**

- A native page carries `[PageEntry(...)]` next to its `@page`
  (`Grove.DisplayManagement.Components`, read by the same catalog scan that reads its routes):
  `[PageEntry("ManageFeatures", Id = "features", Parent = "tools", Caption = "Features", Icon = "@iconify:mdi:shape-outline")]`.
  The routes are the component's own `@page` templates and the shell is its assembly's bucket,
  never re-typed. A component whose routes are distinct pages carries one attribute per page,
  each naming its `Route` (the e-mail settings and the e-mail test page share one editor).
  `ClientOnly` renders the page's document InteractiveWebAssembly (the Workflows Studio pages);
  `DefaultTenantOnly` keeps a page to the Default tenant (Tenants, addressing maps).
- A kind registry's pages come from a `KindPageSource<TKind>` (Parties; Fruitful's Assets and
  Accounting): the section, its "All" page and one page per kind, each kind gated at its
  concrete route. The generic page component declares `[PageEntry(DeclaredBySource = true)]`,
  so its template admits nobody on its own.
- Menu groups (Content, Design, Tools, Settings and their shared sub-sections) are entries with
  no page, declared by `AdminMenuGroups` in Grove.Server; a module's own section (Workflows,
  Audit Trail, Taxes) by that module. A group no visible child is under is not shown.
- Legacy MVC pages are entries pointing at their legacy route (`PageEntries.Legacy`,
  `PageEntries.LegacySettings`: route values the tenant's link generator resolves), a small
  `AdminPages` source per module until the page is ported. MVC authorizes those pages itself;
  the registry puts them in the menu with the same permission.
- Dynamic sources are entries generated from tenant data, read per request: New › *content
  type* (`NewContentPages`, a locked branch, each item gated at its concrete create route by
  the type's own permission), the content-type and content-group pages of the Content menu,
  one item per custom settings type, and designed pages (`DesignedPageSource`, the site's
  defined pages).

**Two layers.** The **declared** layer is the scanned components plus every `IPageSource`,
built when the tenant shell builds, following feature enablement (a disabled module's client
library and its sources are absent) and the active admin theme (admin pages are served only
while the Blazor admin theme is), cached for the shell's lifetime and never stored. The
**defined** layer is tenant data: the dynamic entries (each source caches itself and is
invalidated when its data changes, designed pages on publish) and the tenant's menu edits
below.

**The route gate and the menu read the same permission.** `IRouteAuthorization` (Access)
admits an admin path when the caller holds any one of the permissions of the page whose
template matches it most specifically; a path no page admits is denied, and so is an admin page
that declares none (the shell build names each such page in a warning, and an unknown
permission name too). The menu builder asks the same service for every item, so an item is
shown exactly when its page would open; a section stays while any of its children is shown.
Who may open a page is changed through the access model (roles, permission sets, groups,
overrides), never by re-pointing the page's permission.

**The menu is a view.** `AdminMenuBuilder` builds the registry's admin entries into the
default tree (ordered by position, then caption), resolves captions (§ Captions), then applies
the tenant's layout (renames per culture, hides, moves, icons, custom items and links,
separators, all by entry id) and last the user's own hidden items (§ Two hide layers). The New
branch is locked: the tenant's edits cannot move items into or out of it. Admin menu documents
(Grove.AdminMenu) remain for the other placements, the user profile menu among them; the
sidebar takes no admin menu documents, its custom items and links are the tenant's layout.

- ☐ **Captions declared on `[PageEntry]` and in page sources are not extracted into PO
  catalogs** (the extractor reads `S["..."]`). They translate through the tenant translation
  store, and through a PO catalog only where one already carries the caption.

## Captions

A caption is the invariant literal its entry declares. The sidebar and the app manifest
resolve it per request culture through `MenuCaptionResolver`, under the menu's translation
context `Admin Menus:Primary Navigation`, where the Translations editor lists every declared
caption (`PageMenuLocalizationDataProvider`). Data captions (New › *content type*, the
content-type and content-group pages, custom settings types) are resolved ownerless, so a
content type translated under "Content Types" is translated wherever its name appears.

**No default context.** Contexts are strict namespaces: a translation stored under one is
invisible to a lookup under any other, and upstream falls back to the invariant literal even
when the culture translates the same caption elsewhere. The resolver walks outward instead —
the exact menu context, then parent contexts by stripping `':'` segments
(`Admin Menus:Primary Navigation` → `Admin Menus`), and finally the culture's best entry for
the caption anywhere in the store (for a menu caption, contexts under "Admin Menus" preferred;
for an ownerless one, the others; then the most common value, ordinally tie-broken). An entry
in the exact context always wins, so pinning a caption in the Translations editor overrides
every fallback. Each step checks the specific culture before its parents (`es-ES`, then `es`).

**The PO layer.** Below the store sits the shipped PO catalog: a caption no store entry
covers resolves against the culture's PO catalogs (`PoTranslationLookup`, shared with the
Translations editor), so the full hierarchy is **store edit → PO translation → invariant
literal**, and *deleting* a stored entry reverts the caption to the shipped PO value rather
than the raw literal. To force the literal over a shipped translation, save the caption itself
as the value. Matching tiers: (0) the `msgctxt` of the type that declared the entry
(`PageEntry.DeclaredBy`), (1) any `msgctxt` ending `.AdminMenu` (where the platform modules'
menu captions were extracted), (2) the most common value, ordinally tie-broken. Empty and
identity entries never count. `docs/localization.mmd` diagrams the chain.

**The Grove translations editor.** `/Admin/DataLocalization` (and `/Index`) render Grove's own
Blazor translations page instead of the stock one — the routes shadow the stock URLs, so the
Localization page's "Edit translations" button lands on it unchanged. Same functionality as
the stock page (per-culture, grouped, permission-aware: `ManageTranslations` or the per-culture
permission to edit, `ViewDynamicTranslations` to view), with two deliberate differences backed
by `api/grove/translations`: reads include **orphaned** entries (stored translations no
provider currently enumerates — a disabled feature's strings, an old key after a source string
changed — flagged "stored only", editable and deletable), and the save **merges**: only the
rows the page displayed are replaced (blank deletes), everything else in the store is carried
over untouched. Rows without a stored value show what would render (the PO fallback), with the
declaring types of each menu caption as its first-choice contexts.

**The platform's Translations editor saves without loss.** The admin node localization
providers walk the whole tree of the remaining admin menu documents
(`AdminNodeDataLocalizationProvider.AllNodes`, one row per distinct caption); Save merges
(only the entries the editor displayed are replaced; an emptied value removes one); the
editor's lookups tolerate a duplicate stored row; the translation cache is evicted again after
the document commits. `AdminMenuTranslationCleanup` (in `Grove.DataLocalization`) drops the
entries a saved menu document no longer carries and a deleted menu's whole context;
`ContentTypeTranslationCleanup` does the same for a removed content type's display name.

## Item identity

Layout overrides and each user's hidden items are stored against an item's key, never against
its caption — a caption is translated, so keying on it would orphan every override as soon as
the admin's culture changed. The key (`NavigationItem.Key`) is the page registry entry's `Id`:
declared in code (`features`, `settings-security`, `parties-customers`), derived from data for
a dynamic entry (`new-article`, `content-type-blog`), or the tenant's own `custom-…` key for a
custom item. An entry id is stable across releases: renaming one orphans the tenant edits made
against it.

## Two hide layers: the tenant's and the user's

The layout overlay above is tenant-wide: an item hidden there is gone for everyone. Under it
sits a per-user layer, `GroveUserMenuPreferences.HiddenItemKeys` on `User.Properties` (the
same place OrchardCore keeps a user's culture), edited only by its owner through
`GET`/`PUT api/grove/navigation/me/hidden`. `AdminMenuBuilder` assembles the served
tree once for every consumer — the sidebar endpoint, the app manifest, and any page that
lists what the menu lists — and applies the two layers in that order, both keyed by the served
item key above. A page that mirrors part of the menu (Grove's All Parties lists the Parties
branch's children as its panes) reads the tree with the user layer left off, so it can flag
the user's own hidden items and offer them back, while an item the tenant hid is simply absent.

## Renames are per culture

A rename is a translation of one caption, not a change of identity, so it is recorded against
the culture the admin was viewing when they typed it (`DisplayTextByCulture`). Renaming an item
under `es-ES` leaves every other culture's caption alone (each keeps its own translation, or
the invariant literal when none exists), and resolution falls back through the parent culture
(`es-ES`, then `es`) before the declared caption is used.

A rename recorded this way applies only inside Grove's sidebar. The **Save as this tenant's
translation** action next to a renamed item promotes it into the tenant's translation store —
the store `IDataLocalizer` reads at render time — so Orchard's own Razor admin renders the same
caption instead of disagreeing with Grove. Because that changes what every user of the tenant
sees in that culture, promotion additionally requires `ManageTranslations` (Administrator by
default) on top of the admin menu permission. The translation is keyed on the item's original
caption, under the menu's translation context (`Admin Menus:Primary Navigation`; for an item
of another admin menu document, that menu's context, the one Orchard's own admin looks up).
Promoting after clearing a rename removes the translation again.

Promotion applies to every declared item. Custom items (whose text is the tenant's own) and data
captions (New › *content type* and the other generated items, translated under their data's
context) are refused; renaming them still works and still applies inside Grove's sidebar.
