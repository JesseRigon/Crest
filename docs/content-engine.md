# Content engine: types, fields, field sets, workflows

What Grove's content is made of, how behaviour attaches to it, and the screens and APIs built
on it. [workflows.md](workflows.md) covers the engine that carries all behaviour.

Content works the way the display does. A screen is components holding data, and its
behaviour is workflows. Content is fields holding data, and its behaviour is workflows. There is
one behaviour system for both.

## The concepts

| Concept | What it is | Defined by |
|---|---|---|
| **Type** | A definition: its field sets and the embedded content items it allows | a tenant or a module |
| **Field** | One typed value with its editor and its display: text, number, date, price, media, an option list field, … | field types come from modules |
| **Field set** | A named group of fields, defined once and attached to any number of types | a tenant or a module |
| **Embedded content items** | Instances of other types held inside an item: a product's variants, a page's blocks. The parent type declares which content types may be embedded | a tenant or a module |
| **Content item** | One instance of a type: a document holding its field values and its embedded content items | users |
| **Taxonomy** | Categories: a tree of terms that content is filed under | a tenant or a module |
| **Option list** | A tenant-editable list of keyed options (honorifics, units, country codes) with groups and locks ([Option lists](#option-lists)) | a tenant or a module |
| **Workflow** | Behaviour over fields: what happens when an item is created, updated, published or removed | a module (system flows) or a tenant |

Data and behaviour never share a concept. Fields and field sets hold data and have no
behaviour. Workflows are behaviour and hold no data of their own.

Types are runtime-editable definitions stored per tenant. Module migrations declare types
through the definition manager, and tenants extend them in the type editor without code.

### Names: code and screens

Screens use words for people who are not developers. Code, APIs and docs use the code name.

| Code | Classes | Screens |
|---|---|---|
| Type | `ContentTypeDefinition` | Content type |
| Field | `ContentFieldDefinition`, `ContentField` | Field |
| Field set | `ContentFieldSetDefinition`, `ContentFieldSet` | Field group |
| Embedded content items | `EmbeddedContentDefinition`, `EmbeddedContentItems` | Embedded content |
| Content item | `ContentItem` | Content item |
| Taxonomy | `Taxonomy` | Categories |
| Option list | `OptionList`, `Option` | Option list |
| Workflow | the workflow engine's definitions | Workflow |

The pages follow the screen names: Content types, Field groups, Categories, Option lists.

A screen never shows a code name, and code never uses a screen name for these concepts.

### Definitions and data in code

Every concept has a definition side (the schema, edited on the type) and a data side (what an
item holds). The names pair up, and none of them says "part":

| Definition (schema) | Data (item) |
|---|---|
| `ContentTypeDefinition` | `ContentItem` |
| `ContentFieldSetDefinition` | `ContentFieldSet` |
| `ContentTypeFieldSetDefinition`: a field set attached to a type, with its per-type name, position and settings | — |
| `ContentFieldDefinition` | `ContentField` |
| `EmbeddedContentDefinition`: the embedding's name, allowed content types or stereotypes, count limits, order and display | `EmbeddedContentItems` |

The definition model is the metaschema: the language types are written in. A content type is
a schema written in it, and a content item is a document holding data in that schema.

### Type settings and stereotypes

A type's settings say how its items behave in the content screens: **creatable** (offered on
the create page), **listable** (shown in the content list), **draftable** (saved as a draft
before publishing), **versionable** (keeps its history) and **securable** (has per-type
permissions).

A **stereotype** is a role a type declares (`Widget`, `MenuItem`, `CustomSettings`). Anything
that accepts types by role names the stereotype instead of listing types, so a new type with
that stereotype is accepted at once. An embedding allowing `@Widget` accepts every widget
type; the option list field's `contentitem:@Widget` source lists their items.

## Field sets

A field set is a named group of fields. It is defined once, in the definition store, and types
reference it.
- **Shared.** A field set attached to many types (an Address on Person, Organization and
  Location) is one definition. Adding a field to Address adds it on every type that uses it.
- **Attachable more than once.** A field set can be attached to one type several times under
  different names: an Order has a Billing address and a Shipping address, both the Address
  set. Each attachment has its own name, position and per-type settings.
- **Each type has its own field set**, created with the type and named after it. It holds the
  fields that belong to that type alone, is shown as the type's general group, and is edited
  on the type's page rather than on the Field groups page.
- **The attachment carries the per-type choices.** What differs between types that share a
  set (the attachment's name, position, editor, and whether culture affects a field) is set on
  the `ContentTypeFieldSetDefinition`, never on the shared field.
- **Field sets organise the editor.** The editor shows a type's fields under their set's name,
  in the set's order, so a type with forty fields reads as a few labelled groups rather than a
  flat list. The same grouping drives the display, the field pickers in workflows and queries,
  and the API's field listing.

## Embedded content items

A content type can allow other content types' items to be embedded in its items. Embedded
content items are stored inside the parent item's document, are edited in the parent's editor,
and are created, versioned, published and deleted with it.
- **Each embedded content item has a stable id.** It stores that id, the name of its type, its
  field values and its item properties (display text, owner). Indexes over embedded content
  items are keyed by the id, with the position as the order.
- **Each parent owns its own embedded content items.** They are not shared between parents and
  are not standalone content items: they have no lifecycle, list or permissions of their own.
- **Referenceable embeddings.** An embedding may declare its items referenceable: their ids are
  exposed to option sources and routing, so a field elsewhere can point at one and a route can
  resolve to one. Taxonomy terms and options are referenceable embedded content items of
  their taxonomy or option list.
- **One read, atomic writes.** Embedded data is read with its parent and written in one
  document. It is document-only: anything queried across documents needs an index.

### Live and snapshot embeddings

An embedding is **live** or **snapshot**.
- **Live.** An embedded content item's structure comes from its type's current definition every
  time it is read, so a field added to the embedded type appears on every embedded content item
  at once (empty until filled), and nothing has to be synchronised. A page's blocks are live.
- **Snapshot.** Each time the parent is published, its embedded content items freeze: every value is
  written as plain data, calculated fields included (a field calculated by its definition is no
  longer calculated in a frozen item), and the schema the items were written in (their field
  sets, fields, field types and settings) is stored with them, once per embedding. Later
  definition edits never change what a committed document says or how it reads. An invoice's
  customer details and transaction lines read as they were when the invoice was committed.
- **A snapshot carries its own schema.** The definition store keeps only current definitions;
  no document depends on an old definition version, so old versions are never kept. The
  display, workflows and the API read a frozen item through its stored schema.
- **A reference in a snapshot keeps both.** An option list field in a snapshot item stores the
  referenced item's id and a copy of the referenced values the embedding names, so the
  document keeps the link and what it said at the time.

An **embedding** is one named slot on a parent type that holds embedded content items. It is
declared on the parent type:
- its name (each embedding is its own group in the editor);
- the types it allows;
- its minimum and maximum count;
- whether the order is meaningful.

Embedding can go several levels deep, up to a limit the type editor enforces.

**What an embedding references is a type.** An embedded content item stores the name of its
type, and reading a live one loads that type's whole definition: its field sets, settings,
hooks, triggers and display. So an embedded content item can carry several field sets, run its type's workflows and be allowed by
stereotype (`@Widget`, the convention the option list field's `contentitem:@Widget` source uses).
Repeating one field set is a type with a single field set, or the field set attached more than
once.

How the engine carries an embedding:
- **The declaration** holds the allowed types or stereotypes, the display type the embedded content items
  render with, and whether the editor shows them collapsed.
- **The embedded-items accessor** tells the content system where an item's embedded content items are, so
  taxonomies, routing, the option list field's relationship guard and routing indexes find them.
- **Editor labels** derive from the embedding's name and the allowed types' names ("Add variant"),
  translated through data localization, never free text in the settings.
- **The display** is one component in the component registry that tree templates place by the
  embedding's name.
- **Indexes are declared where their domain lives** and registered with the indexing and search
  engines. Grove.Contents, which owns embedding, declares the embedded-item index (parent, embedding,
  type, position) and adds embedded content items' fields to the parent's search document under the
  embedding's path, so a search finds the parent by embedded content. A module keeps its own
  domain indexes over embedded content items in that module.
- **Storage** keeps today's shape: `{ "<EmbeddingName>": { "ContentItems": [ … ] } }` in the
  parent's document, with a snapshot embedding's schema beside its items.

**Versions.** Embedded content items have no versions of their own: they are versioned with
their parent, so a parent's draft holds draft embedded content items and its published version
the published ones. A publishing hook acts on the version being published.

**Embedded content items are not child content items.** Child content items
(Grove.Contents.Hierarchy) are separate content items that refer to a parent, such as a blog
and its posts. They have their own lifecycle, URLs and permissions. Embedded content items
live and die with their parent.

## Fields and field types

A field type is declared by the module that owns its domain:
- `PriceField` is declared by Grove.Data.Types.Money;
- `GeoPointField` by Grove.Data.Types.Spatial and `GeoStackField` by Grove.Data.Regions;
- `MediaField` by Grove.Media;
- `OptionListField` by Grove.Contents.OptionPicker (its class in
  Grove.Contents.Fields.Abstractions);
- the generic types (text, number, date, boolean, link, …) by Grove.Contents.Fields.

**Grove.Contents.Fields is the registry.** Every field type registers there, with its editor
and display components, its settings, its indexing and its validation. The type editor offers
exactly what is registered, and a field type is usable in any field set.

### Validation and conditions run on the one expression engine

Field validation, field visibility and hook conditions are expressions in the one expression
engine that templates and workflows use ([blazor-display.md](blazor-display.md) › One
expression engine): the same `Expression` model, the same predicates display bindings use, the
same Liquid.
- **A field type's checks** (required, length, range, format) are its registered validation,
  configured per attachment.
- **Rules across fields** are predicates over field paths (`Address.Country == "US"` makes
  `Address.State` required), declared on the type.
- **Visibility** is a predicate too, on any field type. The editor evaluates it as the tree
  re-renders and exempts a hidden field from required validation; the save evaluates the same
  predicate and clears a hidden field's value, so hidden values are never kept dormant.
  Conditions may chain (a field visible only when another visible field has a value), and the
  save evaluates them to a fixpoint. An option list field compares by option key: its stored
  ids resolve to keys on both sides of the comparison.
- **A before hook** may refuse a save with errors keyed by field path.

Every refusal uses one error contract: a map keyed by field path (`FieldSet.Field`, `FieldSet`,
the empty key for the item), answered as 400 and shown under each editor.

### Picking is one field type

`OptionListField` is the one field for choosing a value. Each attachment names an option
source, and every source produces an option list to pick from:
- option lists;
- content items;
- users;
- taxonomy terms;
- currencies, time zones and cultures;
- any source a module registers.

`TaxonomyField` is retired in favour of an option list field on a taxonomy source. The same goes
for `UserPickerField` and `LocalizationSetContentPickerField`, which become option sources.
[The option list field](#the-option-list-field) describes sources, columns and filters.

### Taxonomies and option lists are different things

| | Taxonomy | Option list |
|---|---|---|
| Purpose | Filing content under categories | A value a field holds and code switches on |
| Shape | A tree of terms | Keyed options with groups, an optional `Parent` level, a value |
| Pages | Term pages list the content filed under a term | None |
| Locks | None | Data lock and edit lock (module or tenant) |

Both are option sources for the option list field, and neither replaces the other.

An option keeps a `Category` property: the dimension code evaluates options against. It is
unrelated to taxonomies, and screens label it "Group" so it is never confused with Categories.

## Option lists

An option list is a tenant-editable set of keyed options that modules declare and consume by
key; tenants relabel, reorder and hide options at run time. It is the
`Grove.Contents.OptionLists` feature of Grove.Contents, opt-in per tenant, with no dependency on
taxonomies ([architecture.md](architecture.md) › Modules, features and how a module reaches the
host). Its APIs (`api/grove/option-lists`, `api/grove/global/lists`), migration, permissions
(`ViewOptionLists`, `ManageOptionLists`, `LockOptionLists`), option source, content group
resolver and Liquid filters are gated on the feature; its page is the client library
`Grove.Contents.OptionLists.BlazorWasm`, served only while the feature is enabled. Consumers
declare the feature in their manifests.

- **`OptionList`**: a title and the `OptionListFieldSet`: `Key` (the list's stable logical key,
  such as `pricing.modifier-kind`), `Source`, `OptionContentType`, `DataLock`, `EditLock`, and
  its options as referenceable embedded content items.
- **`Option`**: a title and the `OptionFieldSet`: `Key` (what code switches on), `Source`
  (Module or Tenant), `Hidden`, `Position`, `Category`, `DisplayTextPlural`, `Value`, `Parent`.

**The identity rule.** Content stores the option's id, code compares `Key`, people read the
display text, and `Source` says who put it there. Ids are generated per tenant: the same seeded
option has a different id in every tenant, so code never switches on it. Display text can be
renamed freely without breaking logic.

**The option beyond key and label:**
- `Category`: non-null, flat, one per option; the dimension logic evaluates against, which is
  why the data lock freezes it. Screens label it "Group".
- `DisplayTextPlural`: null falls back to the singular; blank on update clears it.
- `Value`: an optional second machine datum (a dial code on `US`, a regex on a postal format);
  null for pure enums whose key is their value.
- `Position`: the list's only intrinsic order. Every other order is a per-attachment setting.
- `Parent`: the key of the option above this one, for lists arranged in levels such as NAICS
  ([parties.md](parties.md) › Industry classification); null at the top and in flat lists.
  It is validated against unknown keys and cycles, refused on standard global rows and frozen
  under a data lock. Option sources over a levelled list offer `Parent`, `Path` (ancestor
  labels, searchable) and `Level` columns, and `GET api/grove/option-lists/{key}/children?parent=`
  browses one level.

**Locks: one mechanism, two authorities**, each valued None, Tenant or Module:
- `DataLock` freezes the machine surface (categories, the category vocabulary, `Value`,
  `Parent`, data-locked custom fields). Labels, visibility and order stay editable, and tenants
  may add options within existing categories.
- `EditLock` makes the list read-only.
- Module locks are declared in the seed, re-asserted on every reseed and cannot be lifted in
  the tenant. Tenant locks need `LockOptionLists`, which is security-critical, not implied by
  `ManageOptionLists`, and Administrator-only in practice.
- Locks are enforced at the API (409), not in the service: the service trusts module code,
  because migrations edit what tenants must not. Any new endpoint over the service adds its
  own lock check.

**Custom fields on options.** Tenants add fields to an option content type at run time. Each
list names its `OptionContentType` (any type carrying `OptionFieldSet`), assignable from the
list header; retargeting a list that has options is refused, because existing options keep
their stored type. Values travel as strings in a `Fields` map (null leaves a field untouched,
blank clears it, an unknown name is a 400); one accessor knows the document shape, and numeric
and boolean fields store typed values, refusing unparseable input. Each custom field is marked
machine surface (frozen with the data lock) or display surface (the default) through
`OptionFieldSettings`. The grid renders boolean fields as switches and numeric fields as
numeric inputs; the wire format stays strings and the server owns typing.

**Global lists** are seeded by the feature for every tenant that enables it: country codes,
units of measure (with a separate `global.uom-rec20` list mapping each unit key to its UN/CEFACT
Rec 20 code; package units are Rec 21 territory and absent), phone country codes, postal-format
regexes, languages, US area codes, subdivisions, honorifics and suffixes. All are module
data-locked except the personal-name pair. Currencies, time zones and cultures are not lists:
they are option sources over the services that own them (Grove.Data.Types.Money's
`ICurrencyProvider`, `TimeZoneInfo`, `CultureInfo`).

**Seeding is deferred.** Seeds run as a deferred shell-scope task after every feature's
migration ([feature-enablement.md](feature-enablement.md)), because during first-time setup
features migrated later have no index tables yet. A consumer migration that attaches an option
list field to a seeded list defers its attach too.

**Deletion is asymmetric.** A tenant-owned list deletes (its options are embedded, so nothing
is orphaned). A module-seeded list cannot be deleted, because its key is the module's contract;
it can only be hidden. Options only hide, for history.

**A list that outgrows enum scale** (thousands of options) becomes its own content type, and
the attachments that use it switch their source to `contentitem:<Type>`. Only that list moves;
keeping options in one document keeps atomic writes, the locks and cheap reorder and reseed.

## The option list field

`OptionListField` is declared by **Grove.Contents.OptionPicker** (feature
`Grove.Contents.OptionPicker`, declared by the features that bind fields to option sources), with
its contracts and DTOs in the WebAssembly-safe Grove.Contents.OptionPicker.Abstractions. Its
APIs are `api/grove/option-sources` and `api/grove/option-picker/attachment`, and its UI is the
module's client library ([server.md](server.md) › The option picker).

**Stored data is small; columns belong to the attachment.** A field stores one or more ids and
displays several columns of each referenced record. The columns are settings of the attachment
(the same target reads differently in different fields), never stored data:
`OptionListFieldSettings { SourceKey, Multiple, Required, Hint, Placeholder, Columns,
DisplayTemplate, TitlePattern, SearchColumns, SortColumns, Filters }`. The stored value is
`{ SourceKey, SelectedIds }`; a value without a source key reads the attachment's.

**Content items as a source.** `contentitem:` names the allowed types (`contentitem:Customer,Vendor`),
stereotypes (`contentitem:@Widget`) or every type without a stereotype (`contentitem:*`). The
field searches by title; `TitlePattern` (Liquid, `Model.ContentItem` bound) labels the rows; the
selected items are listed in their stored order with an edit link where the user may edit them
and up and down buttons on a multiple field; a create link opens, in a new tab, each allowed
creatable type the user may create. Code declares such a field with
`AsContentItemPicker([types], multiple, required)` and reads it through `SelectedIds`; GraphQL
exposes `selectedIds` and `contentItems`; Liquid reads `SelectedIds` on the field and `Items` on
its display model. Recipes carrying the platform's content picker field convert on import
([server.md](server.md) › The option picker).

**Option sources.** `IOptionSourceProvider` (`Key`, `DescribeColumnsAsync`, `QueryAsync`,
batched `GetByIdsAsync` and its `OptionSourceLookup` overload for a title pattern and edit
links) keeps the field ignorant of what it lists; `IOptionSourceCreateLinks` adds create links
for sources whose records can be created from the field. Providers register through DI. Built
in:

| Source | Lists |
|---|---|
| `optionlist` | Option lists: tenant configuration, always fully in memory, one document |
| `users` | Users, multi-column, including `Property:<dotted.path>` into the user document |
| `contentitem` | Content items, with SQL pushdown, the title pattern, edit and create links |
| `currency`, `timezone`, `culture` | The services that own them |

- The API is read with `QueryOptionSources` (Administrator, Editor, Author, Contributor), and
  each source decides which rows the caller sees.
- A column's `Path` is interpreted by its provider, which advertises its paths; custom fields
  are `Field:<Name>`. Filter and search columns are computed even when not displayed.
- `GetByIdsAsync` leaves out ids it cannot resolve; it never returns a hollow row.

**Sort is a setting of the attachment, not list data.** `SortColumns`, per attachment or per
query, applied by every provider through the shared `OptionRowSorter` ("by group" is
`["Category", "DisplayText"]`). `Position` is the only order a list owns.

**Filtering: two axes, combined with AND.**
1. **Static filters** (`OptionFilter.Value`): literals fixed in the settings ("only active
   customers").
2. **Dependent filters** (`OptionFilter.ValueFrom`): the value is read from another field on the
   item being edited, which is how cascading fields work (Country, then Region). An option list
   field parent's ids resolve through its own source and project to a configured column
   (`ValueFromColumn`; null or `Key` is the option's key). Resolution fails closed: an
   unresolvable parent yields no rows, never every row. `RequireParentValue` makes an empty
   parent mean an empty child; `OnParentChange` is `WarnThenClear` or `SilentClear`; the child
   re-queries when the parent's value is committed, not on each keystroke.

**Filters are not authorization.** A filter says what a field displays; scope says what a user
may see. The query endpoint accepts no filters from the client: the editor sends the content
type, the field name and the editor state, and the server loads that attachment's filters
itself.

**Data scope** builds on content permissions (`ViewContent`, `ViewOwnContent`), split into any
and own like the content list, and fails closed: when nothing is viewable the query matches
nothing, which is distinct from unrestricted. Option lists are unrestricted, because they are
tenant configuration. An item whose references cannot be resolved is out of scope too; the
check is transitive and depth-budgeted, and an exhausted budget denies.

**Posted documents keep what they showed.** `OptionRow` is for editing and display. When an
option lands on a posted document, its display text is written onto the document, and history
never re-reads the live record ([Live and snapshot embeddings](#live-and-snapshot-embeddings)).

### Indexing

`OptionListFieldIndex` is partitioned per content type (a new partition is a migration), one row
per selected id, with the selected key carried so code can query by key without joining ids to
keys. Display columns are never indexed: they are projections of the referenced record.

The `contentitem` source pushes sort and filter into SQL where it can, over the text, numeric and
boolean field indexes (Grove.Infrastructure.Server depends on `Grove.Contents.Fields.Indexing.SQL`
so those rows exist). Its boundaries:
- Text filters never move to SQL: the in-memory match is case-insensitive and SQL equality
  follows the database collation, and results must not change with the execution path.
- Pushed sorts use SQL collation, not the request culture.
- A field-index join leaves out items last saved before the field existed; re-saving them or
  rebuilding the index includes them.
- One join per index type per query.

### Relationship guard

Hierarchies built from content item fields (parent organization, parent location, parent
account) are built in many ways, so the field cannot know how to walk every graph. The owner of
the field registers a handler instead, and it runs on every save path, so generic editors cannot
bypass it.
- `IContentSaveGuard`: guards run on every create and update after the content handlers. A
  refusal throws `ContentSaveRefusedException`, which the content items API answers as 400,
  rather than being swallowed like a handler exception.
- `ContentPickerRelationshipGuard` (Grove.Contents.OptionPicker) walks the item and its embedded
  content items, enforces each content item field's allowed types and stereotypes (its source's
  qualifier) on the server, and runs the handlers registered with
  `AddContentPickerRelationshipHandler<T>(fieldSet, field)` (`IContentPickerRelationshipHandler`),
  such as refusing cycles. Parties' parent organization is one
  ([parties.md](parties.md)).

## Behaviour is workflows

Everything content *does* is a workflow. That covers setting the display text from a title,
generating a URL, scheduling a publish, keeping a container's order and filing a translation.
- **Modules ship system flows.** A module that provides a behaviour ships it as system flows
  (locked, upgraded by code) together with the field set holding its data. For example, a
  module ships a Route field set (a Path field) and the flows that generate and register the
  path on publish.
- **One step to attach.** Attaching a module's field set to a type attaches its system flows
  with it, and detaching the set detaches them. The tenant picks "URL" once.
- **Flows attach to a type's content lifecycle** as hooks or triggers
  ([workflows.md](workflows.md) › Hook slots):
  - a **hook** runs inside the saving request's unit of work, before the item is stored, so a
    behaviour that must hold when the item is stored holds, and a failing hook refuses the
    save;
  - a **trigger** runs after the unit commits, as its own unit.

  The lifecycle, per type, with a hook and a trigger at each point: creating, updating,
  validating, saving a draft, publishing, unpublishing, removing, cloning, importing and
  localizing. Each point's payload carries the item's old and new values.
- **Flows declare the fields they depend on** ([workflows.md](workflows.md) › Field
  dependencies). Each dependency is a field path with a required marker and a read or write
  marker. At publish, a type that lacks a field a flow requires gets a warning; at run time a
  missing required value fails the step with the field named.
- **Required fields are locked.** A field a system flow requires carries a definition lock
  ([Definition locks](#definition-locks)) on that attachment while the flow is attached, so it
  cannot be made optional, retyped or removed. A tenant's own flows place no lock: a type that
  lacks a field one of them requires gets the publish warning.
- **Tenants add their own flows** to the same hooks and triggers, beside the system flows,
  within each slot's policy.

- **Preview runs the before hooks without committing.** Previewing an unsaved item runs its
  hooks in a unit of work that is rolled back, so the preview shows the title, URL and other
  values the hooks would write.

All behaviour data lives in fields, so it is visible, indexable, editable and usable by
queries, the API and other flows like any other data.

What is not workflow behaviour:
- **Indexing** is the data layer's: each field type declares how it is indexed, and the store
  maintains the indexes on save. No flow takes part.
- **Reading** runs no flow. Flows run in the action pipelines; reads go through the read
  pipelines ([workflows.md](workflows.md) › The four pipelines). How read-time values (an
  item's URL, culture, rendered body) are produced is an open question below.

## Localization

Every translated name goes through the localization registry ([localization.md](localization.md)):
- **Definition labels** are localization sources: type names, field-set names, each
  attachment's name (Billing, Shipping), field labels and descriptions, and embedding names.
  The editor's labels ("Add variant") derive from them.
- **Picked values translate through their source.** An option list field shows the label its
  source gives in the current culture (an option's, a term's, an item's), so the
  registry holds each label once, never once per field that picks it.
- **Culture-formatted values** (amounts, quantities with units, dates) follow the culture
  settings, not a per-field translation. Whether culture affects a field is set on its
  attachment, because a shared field may be culture-dependent on one type and not on another.
- **Field values are not translated per culture.** Translating a string field's value is not
  built.

## Content items

A content item is a document holding its field values grouped by field set, and its embedded
content items. Workflows act on it through the fields they declare.

### Editing an item

Items are created, edited and displayed on the admin pages (`Contents/ContentItems`,
`…/{id}/Edit`, `…/{id}/Display`, `Contents/ContentTypes/{type}/Create`). The editor is the
server's editor tree ([blazor-display.md](blazor-display.md) › Content items): a group per
field set and per embedding, each with the editor component its fields declare, saved whole
through `api/grove/content-items`.
- **Embedded data is edited where it lives.** Embedded content items and a taxonomy's terms are
  edited through their types' editor trees inside the parent's editor.
- **Child content items are items of their own.** The child items editor lists them over
  `api/grove/lists/{id}/items`; a child is created on the create page with `?containerId=`,
  and the server fills its `ParentContentItemFieldSet`.
- **The module that owns a field set answers what its editor needs:**
  `api/grove/taxonomies/{id}/terms` and `/tags`, `api/grove/lists/{id}/items` and `/order`,
  `api/grove/content-localization/{id}` and `/localize`, `api/grove/option-sources` (every
  option list field's rows, content items included) and
  `api/grove/content-fields/localization-sets` (the localization set source's candidates).
- **Preview** renders the unsaved item through `api/grove/content-preview`
  (Grove.Contents.Preview).
- **Saving** validates first and refuses with the field-path error contract
  ([Validation and conditions](#validation-and-conditions-run-on-the-one-expression-engine)).
  A content event's changes show in the audit trail as a `ContentDiff`.
- **No per-user overrides.** What the administrator configures is the behaviour for everyone.

### The content items API

`api/grove/content-items` (Grove.Contents) lists, reads, creates, updates, publishes,
unpublishes and deletes items, and serves each item's editor and display trees. Like every
Grove API it is registered with the API registry ([server.md](server.md)) and authorizes the
real request principal with the content permission before touching a service (`IRequestAccess`).
- **Filtered by type** (`?contentType=X`), the list checks `ListContent` against a stand-in item
  of that type, so a securable type enforces its `ListContent_{Type}` permission; the unfiltered
  list checks `ListContent`.
- **Filtered by group** (`?group=<key>`), the list expands the content group to its types and
  item ids ([Content groups](#content-groups)); an unknown group is a 404, a known empty one
  returns no rows.
- The admin content items page takes `?type=`, which applies the filter and shows the type's
  name as the heading. It is a query parameter, not a route segment, because route permissions
  match path segments only, so the one `/Contents/ContentItems` registration and its gate cover
  the filtered view.

### Definition locks

A module freezes the field sets, fields and attachments a system process depends on with a
definition lock: a settings section on the definition, the same mechanism and the same two
authorities as option list locks. A system flow's required fields are locked this way, and a
module's posting process locks the fields it reads.
- **Module** locks are declared by the owning migration (`.Locked()` on a field, field set or
  attachment builder), re-asserted on every migration and cannot be lifted in the tenant.
- **Tenant** locks are placed and lifted through `PUT api/grove/content-types/locks` by holders of
  `LockContentDefinitions` (security-critical, Administrator, not implied by
  `EditContentTypes`).

A lock freezes the machine surface only:
- a locked **field** keeps its existence and shape: it cannot be removed, retyped (text to an
  option list field or back is a retype), pointed at another source, made optional or given a
  visibility condition. Its display settings (label, description, position, editor, display
  mode) stay editable and every other setting is frozen. Its values are not the lock's business:
  a locked field's option list is edited as ever, and the data in items is untouched;
- a locked **field set** cannot be deleted and locks every field it has, including fields added
  while the lock stands;
- a locked **attachment** cannot be detached and its type cannot be deleted; its display
  settings stay editable.

The API reports the effective lock on every field set, field (its own or its set's) and
attachment; the Field groups and Content types pages show a badge and disable the frozen
controls.

**Locks guard tenant writes, in two places.** Grove's definition APIs ask `DefinitionLockGuard`
before altering, and a refusal answers 409 through `DefinitionLockedExceptionMapper`, the API
registry's error contract. The content-types module's `IContentDefinitionService` and
`IContentDefinitionDisplayManager` are decorated (`LockedContentDefinitionServices`, registered
last), so its admin obeys the same locks if a tenant enables it. The definition manager itself
is not guarded: migrations write what tenants must not, and recipe imports run through it. A
write that re-adds a field through the manager (converting a field's type, attaching an option
list) carries the lock settings across, and any new tenant-facing write path over the manager
adds its own guard call.

### GraphQL

GraphQL is generated from the operations registry ([workflows.md](workflows.md) › Generate
the surfaces), like REST and RPC. The stock GraphQL content schema, with its per-part types, is
retired. A content type's object has a field per field set and per embedding, generated from
the definitions.

## Definition screens

Two pages edit definitions, split by ownership:
- **Field groups** owns field definitions: the field type (converting between types keeps every
  custom settings section and replaces only the field's display settings), adding fields, the
  option list field's attachment editor and each field's visibility condition. It owns them
  because a field set is shared: a field change lands on every type that carries the set.
- **Content types** owns which types carry which field sets and embeddings, each attachment's
  settings (name, description, position), the type settings and the type's Content menu entry.

They are backed by `api/grove/content-types/*`, `api/grove/field-visibility` and
`GET/PUT/DELETE api/grove/option-picker/attachment`, all gated by `EditContentTypes`
(Grove.Contents.Types). Settings writes replace the whole section, never merge JSON: a merge
would bring back deleted rows.

**Definition changes are visible after commit.** A change through the definition manager is not
visible to reads in the same request until the session commits, so anything that reacts to it
runs as a deferred shell-scope task (`ShellScope.AddDeferredTask`), never inline.

### A type in the Content menu

A tenant-created type (a Blog, an FAQ) can appear in the admin Content menu without a module
declaring a page for it. `ShowInContentMenu` is a type setting stored on the definition, so it
travels with the content definition recipe step and deployment. It is toggled on the Content
types page through `PUT api/grove/content-types/{type}/menu` (`EditContentTypes`).

`ContentTypePages`, a page registry source read per request, adds one entry per flagged type
under Content: the type's name as caption, the content items page filtered by `?type=` as URL,
and `ListContent_{Type}` as permission when the type is securable (with `ListContent`, which
implies it, as the alternative), otherwise `ListContent`. The entry id is
`content-type-{name}`, so the tenant's menu edits follow the type through a name change.

Module-declared pages (Parties › Customers) are separate; this is the path for types no module
owns ([admin-menu.md](admin-menu.md) › The page registry).

### Content groups

A content group groups content by kind, across modules. It is a property of the source, never
of the item: a group is a named set of entries, each `{ kind, key }` (`type:Person`,
`optionlist:global.honorifics`), and what an entry covers is resolved when read.
- **Modules declare groups** through `IContentGroupProvider` (Grove.Contents.Types). Several
  modules may declare the same group and the entries merge; the first display name and the
  lowest position win. Parties declares `parties` (Person, Organization, the party vocabularies
  and the global name lists).
- **Entry kinds are pluggable.** `IContentGroupEntryResolver` maps a kind to content types and
  item ids. `type` ships with Grove.Contents.Types and `list` with the option lists feature. An
  entry whose key does not exist in the tenant stays in the group as `resolved: false`.
- **The tenant reshapes, never redefines.** A tenant document stores per-group overrides
  (display name, position, hidden, entries added, provider entries removed) and wholly
  tenant-owned groups. Provider defaults are derived from code on every read, so a module's
  later changes still show. Deleting a module-declared group hides it, because its key is the
  module's contract; deleting a tenant-owned group removes it.
- **API:** `api/grove/content-groups`: `GET` (`ListContent`); `POST`, `PUT {key}`,
  `DELETE {key}`, `POST {key}/entries`, `DELETE {key}/entries/{kind}/{entryKey}` and
  `GET|PUT settings` (`EditContentTypes`). The All content items page has a group selector and
  honours `?group=` from the menu.
- **Per-group menu entries are a tenant setting** ("Content group pages in menu" on the admin
  settings). When on, `ContentGroupPages` adds one Content entry per visible group beside the
  per-type entries, so a tenant can work by type, by group, or both.

## Liquid over content

Templates are Liquid that emits component nodes ([blazor-display.md](blazor-display.md) ›
Templates); admin and ERP screens are Blazor pages with no Liquid path in.
- **Liquid filters call the services the API wraps.** Liquid renders on the server against
  objects in scope; it never calls the `api/grove/*` endpoints. The option lists feature
  registers `option_list` and `option_label` when `Grove.Display.Liquid` is on:
  `{{ "list-key" | option_list }}` gives the selectable options; `{{ value | option_label }}`
  gives the label for a stored id, a list of them or a field holding ids, and
  `option_label: "list-key"` also accepts an option key. Content stores ids, so without a label
  filter template authors would hardcode id literals. These two check no permission: options
  are labels, and a template that needs a permission says so (`has_permission`).
- **Filters that reach the stores go through the caller's access decision**, because a filter
  calling a service directly bypasses API authorization and templates render for anonymous
  visitors too: `content_item_id` returns only items the caller may view, `users_by_id` and
  `users_by_name` the caller's own account and others only with `View Users`; with no caller
  nothing loads.
- **Sandboxing.** Fluid's member access is an allow-list of registered types. Templates are
  bounded by `MaxSteps` (1,000,000) and `MaxRecursion` (100).

## Gotchas

- Definition changes are invisible until commit: react in deferred shell-scope tasks.
- Option list seeds and the attaches that use them run deferred in migrations.
- Option list locks are enforced at the API; the service trusts its callers. Definition locks
  are enforced by the guard and the decorators; the definition manager is unguarded.
- Blank on update clears (plural, `Value`, custom fields); null leaves a value untouched.
- Settings writes replace the whole section.
- The option source query never accepts filters from the client; filters are not authorization.
- Providers leave out unresolvable ids, never return hollow rows; parent resolution fails
  closed.
- Ids differ per tenant: code compares `Key`.
- A menu item's permission must be registered or it is dropped: use `…_{Type}` permissions only
  for securable types.
- Unsafe (`POST`, `PUT`, `DELETE`) Grove endpoints require the antiforgery double token; a
  missing header is a 400, not a 403.
- The page registry sync after fresh provisioning can race checks run at once (a dashboard
  screenshot diff, a menu fetch error); settled re-runs pass, so a first-run screenshot diff is
  not rebaselined. The `content-type-menu-toggle` check flips a real setting through the API,
  and a crash mid-check can leave it set.

## Today: content parts

The engine runs on OrchardCore's content parts, kept until field sets and workflows replace
them. A part plays several roles at once:
- **a part made only of fields** is a field set under another name. This includes each type's
  own part, which OrchardCore creates because it can attach fields only to parts, and the
  part's "Reusable" setting, which allows attaching it to one type more than once;
- **`BagPart`** is embedded content items implemented as a part: its value is a list of full
  content items serialized inside the parent's document;
- **code parts** hold data as part properties and run behaviour in handlers. Their data
  becomes fields in a field set and their behaviour becomes system flows:

| Code part | Behaviour | Data it keeps as part properties today |
|---|---|---|
| `TitlePart` | The item's title and display text | `Title` |
| `AutoroutePart` | The item's URL | `Path`, the pattern settings |
| `AliasPart` | A stable alias | `Alias` |
| `PublishLaterPart`, `ArchiveLaterPart` | Scheduled publishing and archiving | the scheduled dates |
| `ListPart` / `ContainedPart` | Child content items under a parent (a blog's posts) | the parent reference, the order |
| `LocalizationPart` | Translations | the culture, the localization set |
| `SeoMetaPart`, `SitemapPart` | Search metadata, sitemap entry | their settings and values |
| `TaxonomyPart` / `TermPart` | Being a taxonomy or a term | the terms |
| `HtmlBodyPart`, `MarkdownBodyPart` | The item's body: sanitized on save, rendered for the body aspect | the source |
| `DisplayTreePart` | The item's page body as a display tree, indexed as text | the tree |
| `PreviewPart` | The preview URL | its pattern setting |
| `CommonPart` | Editing the owner and dates | none (item properties) |
| `AuditTrailPart` | A comment recorded with the audit entry | the comment |
| `ContentShareFolderPart` | Filing the item in a sharing folder | the folder kind and id |
| `MenuPart`, `MenuItemsListPart` | A menu and its items | the items (embedded content items) |
| `LinkMenuItemPart`, `HtmlMenuItemPart`, `ContentMenuItemPart`, `MenuItemPermissionPart` | A menu item's target and visibility | URL, target, markup, permissions |
| `TemplatePart`, `DesignedPagePart` | A template and a designed page | the bound name, the source |
| `UserNotificationPreferencesPart` | A user's notification channels (on users, not content) | the methods, the opt-outs |
| `WorkflowDefinitionPart` | A stored workflow definition | the definition ids and version |

Each part's **settings** (a route pattern, the types a list allows) become settings on the
field set's attachment; its **data** becomes fields. Parts on users and on settings become field
sets the same way. `WorkflowDefinitionPart` keeps its behaviour in code: a workflow cannot run
the engine that loads it.

## Code that still has to change

- Parts renamed to field sets in code, in the definition store and in every screen, with the
  same function: shared definitions, attachable more than once per type, each type's own set.
  `ContentPartDefinition` becomes `ContentFieldSetDefinition`, `ContentTypePartDefinition`
  becomes `ContentTypeFieldSetDefinition`, `ContentPartFieldDefinition` becomes
  `ContentFieldDefinition` and `ContentPart` becomes `ContentFieldSet`, with their builders,
  records, events and handlers (`AlterPartDefinitionAsync`, `ContentPartFieldSettings`,
  `ContentTypePartSettings` included).
- `BagPart` replaced by embedded content items declared on the parent type. From Grove.Contents.BagParts:
  - `BagPart`, `BagPartSettings` and `BagPartSettingType` become `EmbeddedContentDefinition` on the type (the allowed content types or stereotypes);
  - `BagPartHandler`'s contained-items aspect becomes the embedded-items accessor;
  - `BagPartBlocksEditorSettings`' free-text labels are dropped for derived, translated labels;
  - the `BagPart` display shape and its view model become the one display component for `EmbeddedContentItems`;
  - the Liquid registration of `BagPartViewModel` is dropped;
  - the two indexing handlers become Grove.Contents' embedded-item index and search contribution;
  - the migration's two OrchardCore upgrade steps are deleted.
- Each code part in the table above split into a field set holding its data and system flows
  carrying its behaviour, attached as hooks and triggers on the type's lifecycle; its handlers
  deleted.
- Per-type content lifecycle slots: a hook (inside the saving unit of work) and a trigger
  (after commit) at every lifecycle point, with old and new values in the payload. Today
  workflow slots are global and content events reach workflows only after commit.
- Field locks for fields that system flows require.
- A field set and its system flows attach and detach together.
- Validation, visibility and hook conditions on the one expression engine, with the field-path
  error contract answered as 400. Today validation is part and field handlers keyed
  `Part.Field`, and visibility is `GroveFieldVisibilitySettings { Path, Operator: Any|Equals|In,
  Keys }` on the field definition, enforced by Grove.Contents.Types' `FieldVisibilityEnforcer`
  with Grove.Contents.OptionPicker's `IFieldComparisonValueResolver` turning ids into keys.
- Embedded content items keep stable ids and are indexed by them; referenceable embeddings;
  live and snapshot embeddings, with a snapshot's schema stored beside its items.
- Field paths renamed from `Part.Field` to `FieldSet.Field` in field dependencies, `Copy
  fields` mappings, the error contract and templates; permission scopes from
  `ContentPart:{type}.{part}` to `ContentFieldSet:{type}.{fieldSet}`; localization keys from
  `Content Fields:{PartName}` to `Content Fields:{FieldSetAttachment}`.
- The "Content Parts" page renamed "Field groups".
- Stored keys keep their names except part-derived ones, which drop "Part": `TitlePart` is
  stored as `Title`, `AutoroutePart` as `Autoroute`. A type's own set and embedding names are
  unchanged.
- Definition labels (field-set, attachment, field and embedding names) registered as
  localization sources; a culture-affected setting per field on the attachment.
- Preview runs before hooks in a rolled-back unit of work.
- Content part lists renamed to option lists: the `Grove.Contents.PartLists` feature becomes
  `Grove.Contents.OptionLists`; `ContentPartList` and `GroveContentPartListPart` become
  `OptionList` and `OptionListFieldSet`; `GroveOptionPart` becomes `OptionFieldSet` (the type
  stays `Option`); `GroveOptionFieldSettings` becomes `OptionFieldSettings`; the
  `api/grove/content-part-lists` routes become `api/grove/option-lists`; the
  `*ContentPartLists` permissions become `*OptionLists`; the `list:` source prefix, the `list`
  content-group kind and the `contentpartlist` provider become `optionlist`;
  `GroveContentPartListMigrations` becomes `OptionListMigrations`;
  `Grove.Contents.PartLists.BlazorWasm` becomes `Grove.Contents.OptionLists.BlazorWasm`.
- Grove.Contents.Lists renamed Grove.Contents.Hierarchy: `ListPart` becomes
  `ChildContentItemsFieldSet` on the parent type (the allowed child types, page size, manual
  order, feed) and `ContainedPart` becomes `ParentContentItemFieldSet` on each child (the
  parent's id, the position). Screens say "Child items" and "Parent".
- `OptionPickerField` renamed `OptionListField`, with its settings (`OptionPickerFieldSettings`),
  editor, display, index (`OptionPickerFieldIndex`) and GraphQL type.
- Taxonomies are labelled "Categories" in every screen.
- `TaxonomyField`, `UserPickerField` and `LocalizationSetContentPickerField` replaced by
  option list field sources, with their stored values converted.
- The field types that do not register through Grove.Contents.Fields' registry yet do so.
- Type names that carry the project name, which the naming rule forbids
  ([architecture.md](architecture.md) › Naming): `GroveContentPartListPart`,
  `GroveOptionPart`, `GroveRegionalProfilePart`, `GroveRegionalProfileReferencePart`,
  `GroveOrganizationPart`, `GroveContentGroupsDocument`.

## Still to build

Nothing here is scheduled unless stated.

- [ ] **Per-type designed pages.** Every menu-flagged type lands on the generic content items
  page. A designed page ([blazor-display.md](blazor-display.md) › Pages stay a distinct thing)
  replaces it per type, at the same menu entry.
- [ ] **Creating an option content type from the list.** A list's option type is chosen on the
  list header, but creating one means visiting the Content types page; an inline create is a
  convenience, not machinery.
- [ ] **Worldwide subdivisions** (about 5,000 ISO 3166-2 entries) are not seeded. A tenant that
  needs them at scale is the worked example for moving one list to a `contentitem` source.
- [ ] **NANP-wide area codes** (Canada and the Caribbean): the seeded list is the US and its
  territories. Overlay codes are added a few times a year; add-only reseeds are the update path.
- [ ] **Per-tenant and per-user preferred units**, layered on `MeasurementPreferenceService`
  and `uom/preferences`, which resolve units from the culture.
- [ ] **The `query` filter's slot binding** in templates, with the query work
  ([workflows.md](workflows.md)).

### Not built by decision

- **Caller-supplied query narrowing** (a third filter axis): static filters cover the cases. If
  built, it only narrows: intersected with the attachment's filters, never widening them.
- **A menu entry per container item** (each Blog as its own entry): that is a second page
  registry source, not a change to `ContentTypePages`.
- **Partitioning option list field indexes by year or open and closed**: YesSql does not route
  queries across partitions, so the query layer would have to. Posted documents keeping what
  they showed leaves the door open.
- **Moving text filters to SQL**: it would change matching from case-insensitive to the
  database collation.

## Open questions

- [ ] **Read-time values.** Some values are produced when an item is read, not stored: its URL
  and route values, its culture, a body rendered from Markdown, HTML or Liquid, a preview URL,
  the embedded-items accessor. Derived caches (route entries, localization state) live outside
  fields too. Whether each is a declared read projection, a stored field a hook writes, or a
  module's cache, placed on the read pipelines and their compiled or definition-based paths.
- [ ] **Recalculating around a snapshot.** Only embeddings are snapshot, never a whole type: a
  parent's calculated fields (subtotal, tax, total) stay calculated by their hooks. Whether
  those hooks read the rules effective on the document's date, and how a published document is
  corrected once its rules have changed.
- [ ] **Stored-data upgrade.** Item JSON is keyed by part names (`"TitlePart": { "Title": … }`),
  and so are the definition store, recipes, indexes over part properties, audit diffs,
  templates and localization keys. Development tenants are reset; an upgrade step that
  rewrites them is needed before data that must be kept exists.
