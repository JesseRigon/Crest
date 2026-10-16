# Content system — items, types, parts, fields, lists

The content model itself (types, fields, field sets, nesting, taxonomies, enums and how workflows carry behaviour)
is [content-engine.md](content-engine.md). This doc covers the screens and APIs built on it.

How Grove surfaces Orchard's content system: the definition editors, the content
items surface, per-type menu presence, and the Content Part Lists enum system with
its option-picker machinery. This is an explanatory document — what exists, how it
works, and what will bite you. Open work is under [Still to build](#still-to-build).

## Architecture: Orchard owns the model, Grove adapts it

Everything is a content item. Content TYPES are runtime-editable definitions
(parts + fields) stored per tenant; module migrations declare types via
`IContentDefinitionManager`, and tenants extend them through the type editor
without code. Code works against stable PARTS (typed accessors), never against
runtime-added fields — that is what makes runtime extension safe.

Grove adds no parallel content framework. The content APIs are thin JSON surfaces
(`api/grove/content-items`, `api/grove/content-types`, `api/grove/content-part-lists`,
`api/grove/option-sources`, …) over the platform's own services, each declared by the module
that owns the surface and registered with Grove.Infrastructure.Server's API registry
([server.md](server.md)): content items by `Grove.Contents`; content definitions (types, parts,
field visibility, definition locks, content groups) by `Grove.Contents.Types`; content part lists
by the `Grove.Contents.PartLists` feature of Grove.Contents; option pickers by Grove.Contents.OptionPicker.
Today the item and definition APIs are still hosted in
Grove.Infrastructure.Server; server.md tracks the move. Every surface authorizes the real request principal
with the native permission before touching a service (`IRequestAccess`).
Tenant scope, feature gates, validation and lifecycle are Orchard's; Grove must
not bypass, broaden, or persist parallel state.

**Contained items** are the recurring storage pattern: when child records have no
independent lifecycle, they live INSIDE the parent's document (one read, atomic
writes, frozen with the parent). Content Part Lists store their options this way
(`Options` on the list part), and consumers use Orchard's BagPart the same way for
things like transaction lines. The trade-off is deliberate: contained data is
document-only (no free SQL columns), so anything that must be queried across
documents needs an explicit YesSql map index.

## Item editing: the editor tree

Items are created, edited and displayed on the native admin pages (`Contents/ContentItems`,
`…/{id}/Edit`, `…/{id}/Display`, `Contents/ContentTypes/{type}/Create`); there is no MVC content
editor. The editor is the server's editor tree ([blazor-display.md](blazor-display.md) § 7): a
section per part, the editor component each part and field declares, saved whole through
`api/grove/content-items`. Contained data is edited where it lives: a bag's items and a
taxonomy's terms through their types' editor trees inside the parent's editor, a list's
children as items of their own (`ListPartEditor` lists them over `api/grove/lists/{id}/items`; a
child is created on the create page with `?containerId=`, and the server completes its
`ContainedPart`). The module that owns a part answers what its editor needs:
`api/grove/taxonomies/{id}/terms` and `/tags`, `api/grove/lists/{id}/items` and `/order`,
`api/grove/content-localization/{id}` and `/localize`, `api/grove/option-sources` (every picker's
rows, including content items) and `api/grove/content-fields/localization-sets` (the localization
set picker's candidates). The editor's preview renders the unsaved
item through `api/grove/content-preview` (Grove.Contents.Preview). Every save is validated by the
content handlers first and refused with the errors keyed by editor path
([blazor-display.md](blazor-display.md) § 7); a content event's changes are shown in the audit
trail with `ContentDiff`.

## Definition editing: two screens, split by ownership

- The **Content Parts** page owns field DEFINITIONS — field type (conversion
  between types preserves every custom settings section; only the rebuilt
  `ContentPartFieldSettings` is replaced), add-field, the full option-picker
  attachment editor, and per-field visibility conditions. It owns them because a
  part is SHARED: a field change lands on every type carrying that part.
- The **Content Types** page owns which types carry which parts, the ATTACHMENT's
  settings (display name, description, position), type-level settings (Creatable,
  Securable, …), and the per-type Content-menu toggle.

Backed by `api/grove/content-types/*` and `GET/PUT/DELETE
api/grove/option-picker/attachment` (all `EditContentTypes`-gated). Settings
writes are FULL-REPLACE, never JSON-merge — a merge would resurrect deleted rows.
There are deliberately NO per-user instance overrides: what the admin configures
is the behaviour for everyone.

### Definition locks

A module can freeze the parts, fields and attachments a system process downstream depends
on (`Grove.Infrastructure.Settings.GroveDefinitionLockSettings`, stored as a settings section on the
definition; the first consumer is the ERP host's transaction part, whose posting
flows read its fields). Same shape as the list locks - **one mechanism, two authorities**:
`Module` locks are declared by the owning migration (`.Locked()` on a field, part or
attachment builder), re-asserted on every migration run and unliftable in-tenant; `Tenant`
locks are placed and lifted through `PUT api/grove/content-types/locks` by holders of
`LockContentDefinitions` (security-critical, Administrator; deliberately NOT implied by
`EditContentTypes`). A lock freezes the MACHINE surface only:

- a locked **field** keeps its existence and SHAPE: it cannot be removed, retyped
  (converting text to a picker or a picker to text is a retype), pointed at another picker
  source, made optional, or given a visibility condition; every settings section but
  `ContentPartFieldSettings` (display name, description, position, editor, display mode)
  is frozen. Its VALUES are not the lock's business: a locked picker's list is edited as
  ever, and the data in items is untouched;
- a locked **part** cannot be deleted and every field it has is locked as if individually;
  new fields may still be added to it (the tenant's, unlocked - unless the part lock covers
  them, which it does while it stands);
- a locked **attachment** cannot be detached and its type cannot be deleted; its display
  surface (`ContentTypePartSettings`) stays editable.

The API reports the effective lock on every part (`lock`), field (`lock`, its own or the
part's) and attachment (`lock`); the Content Parts and Content Types pages show a badge and
disable the frozen controls.

> **Gotcha — enforcement is for TENANT writes only, and lives in two places.** Grove's
> definition controllers ask `DefinitionLockGuard` before altering (409 with the
> reason, through `DefinitionLockedExceptionMapper`, the API registry's error contract); the stock content-types module's
> `IContentDefinitionService` and `IContentDefinitionDisplayManager` are decorated
> (`LockedContentDefinitionServices`, registered last) so its admin obeys the same locks if
> a tenant enables it (it is not in the host recipe; the decorators are unit-tested,
> not exercised live). `IContentDefinitionManager` itself is NOT guarded: migrations
> legitimately write what tenants must not, and re-adding a field through the manager
> (the list attach conversion, the field-type editor) must carry the lock settings across
> - both do. Any new tenant-facing write path over the manager must add its own guard call.
> Recipe imports run through the manager and are not guarded.

> **Gotcha — read-after-write:** `AlterTypeDefinitionAsync` /
> `AlterPartDefinitionAsync` changes are NOT visible to reads in the same request
> until the session commits. Anything that must react to a definition change
> (e.g. the menu sync) must run as a DEFERRED shell-scope task
> (`ShellScope.AddDeferredTask`), never inline — inline code reads the
> pre-change definitions.

## Content items surface

`GET api/grove/content-items` lists; the stock Orchard Contents REST endpoints
handle item CRUD (Grove does not duplicate them). When the list is filtered to a
type (`?contentType=X`), authorization runs `ListContent` against a stand-in item
of that type, so Securable types enforce the dynamic `ListContent_{Type}`
permission through Orchard's own `ContentTypeAuthorizationHandler`; the
unfiltered list checks global `ListContent`.

The admin ContentItems page accepts a `?type=` query parameter
(`SupplyParameterFromQuery`) that pre-applies the filter and swaps the heading to
the type's display name. It is a QUERY parameter, not a route segment, on
purpose: Grove's route-permission matching is path-segment-only
(query-invisible), so the existing `/Contents/ContentItems` registration and its
gate cover the filtered view with no new route template.

**Field visibility conditions** apply to ANY field type:
`GroveFieldVisibilitySettings { Path, Operator: Any|Equals|In, Keys }` on the
field definition. The editor shows/hides live and exempts hidden fields from
required-validation; the server CLEARS a hidden field's value on save
(Grove.Contents.Types' `FieldVisibilityEnforcer`, a fixpoint over chained conditions; a picker
parent's ids resolve to keys through Grove.Contents.OptionPicker's `IFieldComparisonValueResolver`) — hidden
values are never kept dormant. Configured per field via `GET/PUT
api/grove/field-visibility` (wholesale replace; blank Path clears) and the
Visibility editor on the Content Parts screen. Keys follow the identity rule
below: picker parents resolve stored ids to KEYS before comparison, on both
sides.

## Per-type Content menu presence

A tenant-created type (a Blog, an FAQ) can appear in the admin Content menu
without any module declaring a page for it. `GroveContentTypeMenuSettings
{ ShowInContentMenu }` is a type-level settings POCO stored on the definition via
`MergeSettings<T>` — the same open settings bag every stock setting uses, so it
rides the ContentDefinition recipe step/deployment for free. Toggled on the
Content Types screen via `PUT api/grove/content-types/{type}/menu`
(`EditContentTypes`).

`ContentTypePages` (a dynamic page registry source, read per request) emits one entry per
flagged type under the "Content" section: caption = the type's DisplayName, URL = the generic
content-items page filtered by `?type=`, permission = the dynamic `ListContent_{Type}` when the
type is Securable (with plain `ListContent`, which implies it, as the alternative), plain
`ListContent` otherwise. The entry id is `content-type-{name}`, so the tenant's menu edits
follow the type through a DisplayName change; a flag change shows on the next request.

Every flagged type lands on the generic content-items surface; a designed per-type page
can replace that per type without touching the menu mechanism.

Module-declared pages (e.g. Parties › Customers) are unaffected — this is the additive path
for types no module owns ([admin-menu.md › The page registry](admin-menu.md#the-page-registry)).

## Content groups — grouping by KIND of content, across modules

Orchard's `ContentItem` has no category, and `ContentTypeSettings.Category` is a
single per-type string only the Flows widget picker reads. Grove's grouping is
therefore a property of the content SOURCE, never the item: a group is a named set
of entries, each `{ kind, key }` — `type:Person`, `list:global.honorifics` — and
what an entry covers is resolved at read time.

- **Modules declare groups** through `IContentGroupProvider` (`Grove.ContentGroups`),
  exactly like admin-menu providers feed one section: several modules may
  `builder.Group("members")` and the entries merge; the first display name and
  the lowest position win. Parties declares `parties` (Person, Organization, the
  party vocabularies and the global name lists); Members declares `members`.
- **Entry kinds are pluggable.** `IContentGroupEntryResolver` maps a kind to
  content types and/or content-item ids. `type` ships in Grove.Infrastructure.Server; `list`
  ships with the `Grove.Contents.PartLists` feature (the resolver seam is what lets a
  feature teach the group system a new kind). An
  entry whose key does not exist on the tenant stays in the group as
  `resolved: false` rather than vanishing.
- **The tenant reshapes, never redefines.** `GroveContentGroupsDocument`
  (IDocumentManager) stores per-group overrides: display name, position, hidden,
  entries added, provider entries removed, and wholly tenant-owned groups. Provider
  defaults are re-derived from code on every read, so a module's later changes
  still surface. Deleting a module-declared group only hides it (its key is the
  module's contract); deleting a tenant-owned group removes it.
- **API:** `api/grove/content-groups` (GET, `ListContent`), `POST` / `PUT {key}` /
  `DELETE {key}` / `POST {key}/entries` / `DELETE {key}/entries/{kind}/{entryKey}`
  and `GET|PUT settings` (`EditContentTypes`). `api/grove/content-items?group=<key>`
  expands the group to `type IN (..) OR id IN (..)` (unknown group → 404; known
  but empty → no rows). The All Content Items page carries a group dropdown and
  honours `?group=` from the menu.
- **Per-group pages are a tenant setting.** `GroveContentGroupsSettings
  { AutoMenuPages }` (site settings; "Content group pages in menu" on Admin
  settings). When on, `ContentGroupsMenuNavigationProvider` emits one Content
  entry per visible group beside the per-type entries, so a tenant can work by
  type, by group, or both. Same materialized-menu caveats as the per-type entries:
  writes re-sync as a deferred task, and a group's display name is its sync key.

## Content Part Lists — the tenant-editable enum system

Orchard has no central enum store. `TextFieldPredefinedListEditorSettings` stores
options inside ONE field's settings (copies drift, no provenance, no multi-column
display); Taxonomies have the right data shape but are a CMS categorization
feature whose routing apparatus (AliasPart/AutoroutePart, term pages) is noise
for configuration data. So Grove defines its own system — the
**`Grove.Contents.PartLists` feature of Grove.Contents** (it ships with the content module and
the rest of Grove relies on it, [architecture.md](architecture.md) › Modules, features and how a
module reaches the host), opt-in per tenant, with no dependency on `Grove.Contents.Taxonomies`. Its
startups, controllers (`api/grove/content-part-lists`, `api/grove/global/lists`), migration,
permissions (`ViewContentPartLists`, `ManageContentPartLists`, `LockContentPartLists`), option
source, content group resolver and Liquid filters are gated on the feature; its page is the
client library `Grove.Contents.PartLists.BlazorWasm` (a library named for a feature is served
only while that feature is enabled). Stored names are unchanged (`GroveContentPartListMigrations`,
the part names, the global schema's record). Consumers declare the feature in their manifests.

- **`ContentPartList`**: `TitlePart` + `GroveContentPartListPart` (`Key` — the
  set's stable logical key like `pricing.modifier-kind`; `Source`;
  `OptionContentType`; `DataLock`/`EditLock`; contained `Options`).
- **`Option`**: `TitlePart` + `GroveOptionPart` (`Key` — what CODE switches on;
  `Source` = Module|Tenant; `Hidden`; `Position`; `Category`;
  `DisplayTextPlural`; `Value`; a back-reference to its list).
- **`OptionPickerField`** (Grove.Contents.OptionPicker) is the reference mechanism, for list options and
  every other source, content items included. Stock `UserPickerField` stays in use for plain
  user references.

**The identity rule (load-bearing — everything else depends on it):** content
stores `ContentItemId`, code compares `Key`, humans read `DisplayText`, `Source`
says who put it there. `ContentItemId` is GENERATED per tenant — the same seeded
option has a different id in every tenant, so it can never be what code switches
on. `DisplayText` is freely renamable without breaking logic.

**The option surface beyond key + label:** `Category` (non-null, flat, one per
option — the dimension logic evaluates against, which is why the data lock
freezes it); `DisplayTextPlural` (null = fall back to singular; blank on update
CLEARS); `Value` (an optional second machine datum — a dial code on `US`, a
regex on a postal format; null for pure enums whose key IS their value);
`Position` (the list's only intrinsic order — everything else about ordering is
a per-attachment parameter). `Parent` (the key of the option above this one, for lists
arranged in levels such as NAICS; null at the top and in flat lists; machine surface like
`Category`).

**Locks — one mechanism, two authorities**, each valued None|Tenant|Module:
`DataLock` freezes the MACHINE surface (categories, the category vocabulary,
`Value`, data-locked custom fields) while labels, visibility and order stay
editable and tenant adds are allowed within existing categories; `EditLock` makes
the list read-only outright. Module locks are seed-declared, re-asserted on
reseed, and unliftable in-tenant; tenant locks need the security-critical
`LockContentPartLists` permission (deliberately NOT implied by
`ManageContentPartLists`; Administrator-only in practice).

> **Gotcha — lock enforcement lives at the CONTROLLER (409), not the service.**
> The service trusts module code, because migrations legitimately edit what
> tenants must not. Any new endpoint over the service must add its own lock
> check.

**Custom fields on option types.** Tenants add fields to option parts at runtime;
each list stores an `OptionContentType` (types carrying `GroveOptionPart` are
eligible; assignable from the list header; retargeting a POPULATED list is
refused because existing option items keep their stored type and would orphan).
Values travel stringified in a `Fields` bag (null = untouched, blank = clears,
unknown names 400); `OptionFieldAccessor` is the ONE place that knows the
document JSON, and Numeric/Boolean fields store typed values — unparseable input
is refused, not stringified. Each field carries a per-field lock designation
(`GroveOptionFieldSettings.DataLocked`): machine surface (frozen with the data
lock) or display surface (default). The grid renders Boolean fields as switches
and Numeric fields as numeric inputs; the wire format stays strings and the
server owns typing.

**Global lists** are seeded by the `Grove.Contents.PartLists` feature for any enabling tenant:
country codes, UoM (with a separate `global.uom-rec20` mapping list — key = unit
key, Value = UN/CEFACT Rec 20 code; package units are deliberately absent, Rec 21
territory), phone country codes, postal-format regexes, languages, US area codes,
subdivisions, honorifics/suffixes. All module-data-locked except the
personal-name pair. What is deliberately NOT a list: currencies, time zones and
cultures are option-source PROVIDERS over the services that already own them
(Grove.Data.Types.Money's `ICurrencyProvider`, `TimeZoneInfo`, `CultureInfo`) — never a
parallel seeded copy.

> **Gotcha — seeding is DEFERRED.** List seeding runs as a deferred shell-scope
> task, never inline in a data migration: content creation during first-time
> tenant setup fires other features' handlers against index tables that do not
> exist yet. Consequently a consumer migration that attaches a picker to a type
> must ALSO defer its attach — an inline attach at first-time setup runs before
> the seeds exist and aborts provisioning.

**Deletion is asymmetric by design.** Tenant-owned lists delete
(options are contained, nothing orphans); module-seeded lists refuse deletion —
their keys are the module's contract — and can only be hidden. Individual
options only hide, for history.

> **Gotcha — don't convert a big list to a content type "for indexing".**
> Option data would still be document-only, the conversion costs single-document
> atomicity, the lock model's home, and cheap reorder/reseed. If ONE list
> outgrows enum scale (thousands of options), promote THAT list alone: new
> content type + flip the attachment's `SourceKey` to `contentitem:<Type>` — the
> provider seam makes it a per-attachment swap.

## Option pickers: projection, providers, filtering

The option picker is its own module, **Grove.Contents.OptionPicker** (feature `Grove.Contents.OptionPicker`,
declared by the features that bind fields to option sources), with its contracts and DTOs in a
WebAssembly-safe Grove.Contents.OptionPicker.Abstractions; its API is `api/grove/option-sources` and
`api/grove/option-picker/attachment`, its UI the module's client library
([server.md](server.md) § 3). It is the **one field type for picking**: content items are one
option source among the others, so there is no separate content picker field.

A dropdown stores ONE id but displays N columns of the referenced record. The
columns are per-ATTACHMENT settings (the same target renders differently in
different dropdowns), never part of the stored data:
`OptionPickerFieldSettings { SourceKey, Multiple, Required, Hint, Placeholder, Columns,
DisplayTemplate, TitlePattern, SearchColumns, SortColumns, Filters }`. Stored data stays tiny:
`{ SourceKey, SelectedIds }`; a value without its source key reads the attachment's.

**Content items as a source.** `contentitem:` names the allowed types (`contentitem:Customer,Vendor`),
stereotypes (`contentitem:@Widget`) or every type without a stereotype (`contentitem:*`). The
picker searches by title; `TitlePattern` (Liquid, `Model.ContentItem` bound) labels the rows; the
selected items are listed in their stored order with an edit link where the user may edit them
and up/down for a multiple field; a create link is offered per allowed creatable type the user may
create (new tab, so the item being edited stays open). On save the guard refuses a target of
another type (Picker relationship guards below). Code declares such a field with
`AsContentItemPicker([types], multiple, required)` and reads it with
`Get<OptionPickerField>(name).SelectedIds`; GraphQL exposes `selectedIds` and `contentItems`;
Liquid reads `SelectedIds` on the field and `Items` on its display model. Tenants that had the
platform's content picker field were converted by Grove.Contents.OptionPicker's data migration (type
definitions, then every item's stored ids, contained items included), and old recipes convert on
import ([server.md](server.md) § 3).

**The provider seam** (`IOptionSourceProvider`: `Key`, `DescribeColumnsAsync`,
`QueryAsync`, batched `GetByIdsAsync` and its `OptionSourceLookup` overload for a title pattern
and edit links; `IOptionSourceCreateLinks` for sources whose records can be created from the
picker) keeps the field ignorant of what it lists.
Registration is DI-only. Built-in providers: `contentpartlist` (tenant
configuration — always fully in memory, one document), `users` (multi-column,
incl. `Property:<dotted.path>` into user JSON), `contentitem` (entity records,
real SQL pushdown, title pattern, edit and create links), `currency`, `timezone`, `culture`.
The API is read with `QueryOptionSources` (Administrator, Editor, Author, Contributor); each
source decides which rows the caller sees. Column `Path` is
provider-interpreted (each provider advertises its own paths); custom-field
columns follow the `Field:<Name>` convention. Contract for all providers:
`GetByIdsAsync` returns rows that are ABSENT when unresolvable, never hollow.

**Sort is an instance parameter, not list data.** Per-attachment or per-query
`SortColumns`, applied by every provider through the shared `OptionRowSorter`
("categorized" is just `["Category", "DisplayText"]`). The manual order
(`Position`) is the only order the list itself owns.

**Filtering — three axes, composed with AND:**

1. **Static filters** (`OptionFilter.Value`) — literals fixed at configuration
   time ("only active customers").
2. **Dependent filters** (`OptionFilter.ValueFrom`) — the value is read from
   another field on the item being edited, which is how cascading dropdowns fall
   out of the same mechanism (Country → Region). A picker parent's stored ids
   resolve through its OWN source and project to a configured column
   (`ValueFromColumn`; null/"Key" = the option's technical Key). Resolution
   fails CLOSED: an unresolvable parent yields empty, never "matches
   everything". `RequireParentValue` makes an empty parent mean an empty child;
   `OnParentChange` is `WarnThenClear|SilentClear`; re-query fires when the
   parent COMMITS, not per keystroke.
3. Caller-supplied narrowing is deferred (see the plan doc); if ever built it
   must be narrowing-only.

> **Warning — filters are not authorization.** A filter says what a dropdown
> DISPLAYS; scope says what a USER may see. The query endpoint accepts NO
> filters from the client — the picker sends `ContentType` + `FieldName` +
> `EditorState`, and the server loads that attachment's filters itself. A client
> that could post filters could query any source with any predicate.

**Data scope** builds on Orchard's own authorization (`ViewContent` /
`ViewOwnContent` via `AuthorizeContentTypeAsync`), bucketing any/own like
stock's admin list filter, fail-closed (`Where(x => false)` when nothing is
viewable; "may see nothing" is DISTINCT from "unrestricted"). Content part
lists are unrestricted: they are tenant configuration. **Assignment** is a
separate model from ownership (`Owner` = who created it; assignment = who
handles it): `GroveAssignmentPart` holds N assignments per item, indexed one row
per assignment, and scope-layering only ever NARROWS the permission result.
Unresolvable references make the REFERENCING item out of scope too (transitive,
depth-budgeted, budget exhaustion DENIES) — a hollow record is worse than an
absent one.

**Snapshot rule:** `OptionRow` is for editing and display only. When an option
lands on a posted document, the projected display text is FROZEN onto that
document; history never re-reads the live record.

## Indexing

`OptionPickerFieldIndex` is partitioned PER CONTENT TYPE (declaration-based —
a new partition is a migration, not code), one row per selected id, with
`SelectedKey` carried so code can query by key with no id→key join. Display
columns are never indexed — they are projections of the referenced record.

The `contentitem` provider pushes sort and filter into SQL where it can
(`PlanSqlSort`, `PlanFieldFilterPromotion` over the stock
Text/Numeric/BooleanFieldIndex tables — Grove's Server manifest depends on
`Grove.Contents.Fields.Indexing.SQL` so those rows exist). Known,
ACCEPTED boundaries:

- Text filters deliberately never promote to SQL: the in-memory matcher is
  case-insensitive, SQL equality follows DB collation — semantics must not
  change with the execution path.
- Pushed sorts use SQL collation, not the request culture (matches every stock
  Orchard list).
- A field-index join excludes items last saved before the field existed (stock
  caveat — re-save or rebuild the index).
- One join per index type per query (YesSql merges same-type joins).

## Quick gotcha checklist

- Definition writes are invisible until commit → react in DEFERRED shell tasks.
- List seeding and picker attaches in migrations must both be deferred.
- Lock enforcement is at the controller; the service trusts callers.
- Blank-on-update CLEARS (plural, Value, custom fields); null means untouched.
- Renaming a type's DisplayName re-keys its synced menu entry.
- Unregistered permission names are silently dropped from menu items — only use
  dynamic `…_{Type}` names for Securable types.
- Settings writes are full-replace; JSON-merge resurrects deleted rows.
- The query endpoint never accepts client filters; filters ≠ authorization.
- Providers must return absent (not hollow) rows for unresolvable ids; parent
  resolution fails closed.
- `ContentItemId` differs per tenant; code compares `Key`, always.
- Unsafe (`POST`/`PUT`/`DELETE`) Grove endpoints require the Orchard antiforgery
  double-token; a missing header is a 400, not a 403.

## Picker relationship guards

Hierarchies built from content item pickers (parent organization, parent location, parent account)
are built in many different ways, so the picker cannot know how to walk every graph. The owner
of a picker field registers its own handler instead, and it runs on every save path, so generic
editors cannot bypass it. `IContentSaveGuard` in the platform's content manager: guards run on
every create and update after the handlers, and a refusal throws
`ContentSaveRefusedException` (an `InvalidOperationException`; the content items API answers
400) instead of being swallowed like a handler exception. `ContentPickerRelationshipGuard`
(Grove.Contents.OptionPicker) walks the item and its contained items, enforces each content item picker's
allowed types and stereotypes (its source's qualifier) on the server, and runs the handlers registered with
`AddContentPickerRelationshipHandler<T>(part, field)` (`IContentPickerRelationshipHandler`),
such as refusing cycles. First user: Parties' parent organization.

Hierarchical lists (full NAICS, [parties.md](parties.md) › Industry classification) use an
option's `Parent`, validated against strangers and cycles (`ContentPartListRules.ValidateParent`),
refused on standard global rows and frozen under a data lock. Pickers get `Parent`, `Path`
(ancestor labels, searchable) and `Level` columns, filter and search columns are computed even
when not displayed, and `GET api/grove/content-part-lists/{key}/children?parent=` browses one
level. `Category` stays the flat label.

## Liquid over content

Templates are Liquid that emits component nodes ([blazor-display.md](blazor-display.md) § 5);
admin and ERP screens are Blazor pages with no Liquid path in.

- **Liquid filters over the services the API wraps.** Liquid renders server-side against
  objects in scope; it never calls the `api/grove/*` endpoints. The `Grove.Contents.PartLists`
  feature registers `option_list` and `option_label` when `Grove.Display.Liquid` is on:
  `{{ "list-key" | option_list }}` gives the selectable options; `{{ value | option_label }}`
  gives the label for a stored ContentItemId, a list of them or a field with
  `TermContentItemIds`, and `option_label: "list-key"` also accepts an option key. Content
  stores `ContentItemId`, so without a label filter template authors would hardcode id literals.
  No authorization check on these two: options are labels, and a template that needs a
  permission says so (`has_permission`).
- **Filters that reach the stores go through the caller's access decision**, because a filter
  calling a service directly bypasses controller authorization and templates render for
  anonymous visitors too: `content_item_id` returns only items the caller may view,
  `users_by_id` and `users_by_name` the caller's own account and others only with `View Users`;
  with no caller nothing loads. Fluid's member access is an allow-list of registered types.
  Templates are bounded by `MaxSteps` (1,000,000) and `MaxRecursion` (100).

## Still to build

Nothing here is scheduled unless stated.

- [ ] **Per-type designed pages.** Every menu-flagged type lands on the generic
  content-items surface. A designed page ([blazor-display.md](blazor-display.md) § 6a) is the
  replacement, per type, mounting at the same menu entry.
- [ ] **Guided creation of option content types.** Lists can be ASSIGNED an option
  type (dropdown on the list header), but creating one still means visiting the
  Content Types screen, creating a type carrying `GroveOptionPart`, and coming
  back. A create-inline flow is a UX nicety, not machinery.
- [ ] **Worldwide subdivisions** (~5,000 ISO 3166-2 entries) are deliberately not
  seeded. If a tenant needs them at scale, that is the worked example for
  promoting one list to a `contentitem` source (see the promotion gotcha).
- [ ] **NANP-wide area codes** (Canada/Caribbean): the seeded list is US +
  territories by decision; widening is a separate decision. Overlay codes are
  added a few times a year — adds-only reseeds are the update path.
- [ ] **Per-tenant/per-user preferred-unit settings.**
  `MeasurementPreferenceService` + `uom/preferences` resolve culture → units;
  the per-tenant/per-user adaptation layers on that seam later.
- [ ] **The `query` filter's slot binding** in templates, with the query work
  ([workflows.md](workflows.md)).

### Known transients / test-suite notes

- The once-per-shell provider-menu sync can race checks run immediately after
  fresh provisioning (a dashboard-screenshot diff, a menu fetch error). Settled
  re-runs pass. Do not rebaseline a screenshot for a first-run diff.
- `content-type-menu-toggle` flips a real setting through the API; a mid-check
  crash can leave the flag set. Inherent to testing real state; the check is
  API-driven to keep the window small.

### Deliberately not built

Not built by decision; do not build without a new one:

- [ ] **Caller-supplied query narrowing** (the third filter axis): deferred because
  static filters cover the motivating cases. If ever built it must be
  NARROWING-ONLY — intersected with the attachment's configured filters, never
  able to widen them (the query endpoint's no-client-filters rule stands).
- [ ] **`ListsAdminNode`-style item-per-container-instance menus** (each Blog as its
  own entry): out of scope. If wanted, it is a SECOND dynamic page source, not a
  change to `ContentTypePages`.
- [ ] **Real cross-table partitioning by year/open-closed** for picker indexes:
  YesSql will not route queries across partitions, so the query layer would have
  to. The snapshot rule (posted documents are closed records) keeps the door
  open.
- [ ] **Text-filter SQL promotion**: accepted boundary — in-memory matching is
  case-insensitive, SQL equality follows DB collation; the bounded-oversample
  residual path stays until someone accepts a semantics change.
