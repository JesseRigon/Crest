# Content engine: types, fields, field sets, workflows

What Grove's content is made of and how behaviour attaches to it. This doc is the content
model. [content-items.md](content-items.md) covers the content screens and APIs built on it,
and [workflows.md](workflows.md) covers the engine that carries all behaviour.

Content works the way the display does. A screen is components holding data, and its
behaviour is workflows. Content is fields holding data, and its behaviour is workflows. There is
one behaviour system for both.

## The concepts

| Concept | What it is | Defined by |
|---|---|---|
| **Type** | A definition: its field sets and the types it nests | a tenant or a module |
| **Field** | One typed value with its editor and its display: text, number, date, price, media, an option picker, … | field types come from modules |
| **Field set** | A named group of fields, defined once and attached to any number of types | a tenant or a module |
| **Nested type** | Items of another type held inside an item: a product's variants, a page's blocks | a tenant or a module |
| **Content item** | One instance of a type: a document holding its field values and its nested items | users |
| **Taxonomy** | Categories: a tree of terms that content is filed under | a tenant or a module |
| **Enum** | A tenant-editable list of keyed values (honorifics, units, country codes) with groups and locks ([content-items.md](content-items.md)) | a tenant or a module |
| **Workflow** | Behaviour over fields: what happens when an item is created, updated, published or removed | a module (system flows) or a tenant |

Data and behaviour never share a concept. Fields and field sets hold data and have no
behaviour. Workflows are behaviour and hold no data of their own.

### Names: code and screens

Screens use words for people who are not developers. Code, APIs and docs use the code name.

| Code | Screens |
|---|---|
| Type | Content type |
| Field | Field |
| Field set | Field group |
| Nested type | Nested items |
| Content item | Content item |
| Taxonomy | Categories |
| Enum | Option list |
| Workflow | Workflow |

A screen never shows a code name, and code never uses a screen name for these concepts.

## Field sets

A field set is a named group of fields. It is defined once, in the definition store, and types
reference it.
- **Shared.** A field set attached to many types (an Address on Person, Organization and
  Location) is one definition. Adding a field to Address adds it on every type that uses it.
- **Attachable more than once.** A field set can be attached to one type several times under
  different names: an Order has a Billing address and a Shipping address, both the Address
  set. Each attachment has its own name, position and per-type settings.
- **Each type has its own field set**, created with the type and named after it. It holds the
  fields that belong to that type alone and is shown as the type's general group.
- **Field sets organise the editor.** The editor shows a type's fields under their set's name,
  in the set's order, so a type with forty fields reads as a few labelled groups rather than a
  flat list. The same grouping drives the display, the field pickers in workflows and queries,
  and the API's field listing.

## Nested types

A type can nest other types. Nested items are stored inside the parent item's document, are
edited in the parent's editor, and are created, versioned, published and deleted with it.
- **Only data is embedded.** A nested item stores its field values and the name of its type.
  Its structure comes from that type's shared definition every time it is read, so a field
  added to the nested type appears on every nested item at once (empty until filled), and
  nothing has to be synchronised.
- **Each parent owns its own nested items.** They are not shared between parents and are not
  standalone content items: they are not listed, routed or queried on their own.

A nesting is declared on the parent type:
- its name (each nesting is its own group in the editor);
- the types it allows;
- its minimum and maximum count;
- whether the order is meaningful.

Nesting can go several levels deep, up to a limit the type editor enforces.

**Nested items are not contained items.** Contained items are separate content items that refer
to a container, such as a blog and its posts. They have their own lifecycle, URLs and
permissions. Nested items live and die with their parent.

## Fields and field types

A field type is declared by the module that owns its domain:
- `PriceField` is declared by Grove.Data.Types.Money;
- `GeoPointField` by Grove.Data.Types.Spatial and `GeoStackField` by Grove.Data.Regions;
- `MediaField` by Grove.Media;
- `OptionPickerField` by Grove.Contents.OptionPicker;
- the generic types (text, number, date, boolean, link, …) by Grove.Contents.Fields.

**Grove.Contents.Fields is the registry.** Every field type registers there, with its editor
and display components, its settings, its indexing and its validation. The type editor offers
exactly what is registered, and a field type is usable in any field set.

### Picking is one field type

`OptionPickerField` is the one field for choosing a value. Each attachment names an option
source, and every kind of reference is a source:
- enums;
- content items;
- users;
- taxonomy terms;
- currencies, time zones and cultures;
- any source a module registers.

`TaxonomyField` is retired in favour of an option picker on a taxonomy source. The same goes
for `UserPickerField` and `LocalizationSetContentPickerField`, which become option sources.

### Taxonomies and enums are different things

| | Taxonomy | Enum |
|---|---|---|
| Purpose | Filing content under categories | A value a field holds and code switches on |
| Shape | A tree of terms | Keyed options with groups, an optional `Parent` level, a value |
| Pages | Term pages list the content filed under a term | None |
| Locks | None | Data lock and edit lock (module or tenant) |

Both are option sources for the option picker, and neither replaces the other.

An enum option keeps a `Category` property: the dimension code evaluates options against. It is
unrelated to taxonomies, and screens label it "Group" so it is never confused with Categories.

## Behaviour is workflows

Everything content *does* is a workflow. That covers setting the display text from a title,
generating a URL, scheduling a publish, keeping a container's order and filing a translation.
- **Modules ship system flows.** A module that provides a behaviour ships it as system flows
  (locked, upgraded by code) together with the field set holding its data. For example, a
  module ships a Route field set (a Path field) and the flows that generate and register the
  path on publish.
- **One step to attach.** Attaching a module's field set to a type attaches its system flows
  with it, and detaching the set detaches them. The tenant picks "URL" once.
- **Each flow chooses how it runs.** A flow's execution mode is a setting on the flow: compiled
  (a fast path built from the published definition) or interpreted by the workflow engine.
  System flows on hot paths ship compiled. Changing a flow's mode means a rebuild, so it goes
  through the deployment system's change scheduling.
- **Flows attach to content events** through hook slots on the type: created, updated,
  publishing, published, removed ([workflows.md](workflows.md) › Hook slots). A flow attached
  to a "before" slot runs inside the saving request's unit of work, so a behaviour that must
  hold before the item is stored holds. Flows on "after" slots run as their own units.
- **Flows declare the fields they depend on** ([workflows.md](workflows.md) › Field
  dependencies). Each dependency is a field path with a required marker and a read or write
  marker. At publish, a type that lacks a field a flow requires gets a warning; at run time a
  missing required value fails the step with the field named.
- **Required fields are locked.** A field a system flow requires is locked on the type, so it
  cannot be made optional or removed while the flow is attached. The lock is the field
  counterpart of enum locks: None, Tenant or Module, with module locks unliftable in the
  tenant.
- **Tenants add their own flows** to the same slots, beside the system flows, within each
  slot's policy.

All behaviour data lives in fields, so it is visible, indexable, editable and usable by
queries, the API and other flows like any other data.

What is not workflow behaviour:
- **Indexing** is the data layer's: each field type declares how it is indexed, and the store
  maintains the indexes on save. No flow takes part.
- **Reading** a derived value (an item's display text, its URL) is a query, compiled like
  every query for fast reads ([queries.md](queries.md)). Flows write the fields; reads never
  run a flow.

## Content items

A content item is a document holding its field values grouped by field set, and its nested
items. Workflows act on it through the fields they declare.

## Today: content parts

The engine runs on OrchardCore's content parts, kept until field sets and workflows replace
them. A part plays several roles at once:
- **a part made only of fields** is a field set under another name. This includes each type's
  own part, which OrchardCore creates because it can attach fields only to parts, and the
  part's "Reusable" setting, which allows attaching it to one type more than once;
- **`BagPart`** is type nesting implemented as a part: its value is a list of full content
  items serialized inside the parent's document;
- **code parts** hold data as part properties and run behaviour in handlers. Their data
  becomes fields in a field set and their behaviour becomes system flows:

| Code part | Behaviour | Data it keeps as part properties today |
|---|---|---|
| `TitlePart` | The item's title and display text | `Title` |
| `AutoroutePart` | The item's URL | `Path`, the pattern settings |
| `AliasPart` | A stable alias | `Alias` |
| `PublishLaterPart`, `ArchiveLaterPart` | Scheduled publishing and archiving | the scheduled dates |
| `ListPart` / `ContainedPart` | Contained items | the container reference, the order |
| `LocalizationPart` | Translations | the culture, the localization set |
| `SeoMetaPart`, `SitemapPart` | Search metadata, sitemap entry | their settings and values |
| `TaxonomyPart` / `TermPart` | Being a taxonomy or a term | the terms |

## Code that still has to change

- Parts renamed to field sets in code, in the definition store and in every screen, with the
  same function: shared definitions, attachable more than once per type, each type's own set.
- `BagPart` replaced by type nesting declared on the parent type.
- Each code part in the table above split into a field set holding its data and system flows
  carrying its behaviour, attached through the type's content event slots; its handlers
  deleted.
- Content event hook slots on types, with "before" slots running inside the saving unit of
  work, and field locks for fields that system flows require.
- A field set and its system flows attach and detach together.
- A flow's execution mode (compiled or interpreted) as a flow setting, changed through
  deployment scheduling; a compiled fast path for published flows.
- Option lists are enums in code. Today's names: the `Grove.Contents.PartLists` feature,
  `ContentPartList` and `GroveContentPartListPart`, `Option` and `GroveOptionPart`, the
  `api/grove/content-part-lists` routes, the `*ContentPartLists` permissions and
  `Grove.Contents.PartLists.BlazorWasm`. Screens say "Option lists".
- Taxonomies are labelled "Categories" in every screen.
- `TaxonomyField`, `UserPickerField` and `LocalizationSetContentPickerField` replaced by
  option-picker sources, with their stored values converted.
- The field types that do not register through Grove.Contents.Fields' registry yet do so.
- Type names that carry the project name, which the naming rule forbids
  ([architecture.md](architecture.md) › Naming): `GroveContentPartListPart`,
  `GroveOptionPart`, `GroveRegionalProfilePart`, `GroveRegionalProfileReferencePart`,
  `GroveOrganizationPart`, `GroveContentGroupsDocument`.

## Open questions

- [ ] **Grove.Contents.Flows into the engine.** Nesting is an engine concept, with versioning
  and publishing shared with its parent; which of the module's pieces fold into Grove.Contents
  and which are retired is undecided.
