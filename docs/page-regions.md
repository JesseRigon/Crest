# Page regions — UI injection across modules

How a downstream module puts UI on a page owned by a module it must not be referenced by.

A downstream module often must appear on screens it does not own: a tax module's per-line
tax and document summary on a transaction editor, an exemption panel on the party editor.
The dependency rule applies — **the page's module must not reference the downstream module
to render its contribution** — so the UI contribution is injected the way a downstream
module's content parts are. `RenderFragment` slots do not do this: they are caller-supplied,
letting a page pass content into a component it uses, not letting a foreign module push
content into a page.

**The seam:** pages declare named regions, downstream modules register components against
region keys with an order, the shell resolves at render. Nothing registered renders nothing,
which is the correct disabled behaviour. Region keys belong to the module owning the page,
in its `Domain`.

- `IPageRegionContributor` lives in `Grove.Display.Management.Abstractions`; the
  `<GrovePageRegion>` component in Grove.Display.Components.
- The component registry collects the contributors with the components. A region key is an
  open zone key, declared by the owning library with `[assembly: ZoneKey(...)]`: a page region
  is a *slot with a contribution key* in the display model
  ([blazor-display.md](blazor-display.md) decision 6).
- Grove.Parties declares regions on its party-type panes (`PartiesPageRegions.Pane(...)`,
  `PartiesPageRegions.Detail(...)`) for other modules to contribute to; the general settings
  page opens `SettingsPageRegions.General` (Grove.Display.Components) for modules' own tenant-setting
  sections (Parties' is the first).

It is a platform concern, since every module contributing UI to another's screen needs it.
Whole pages are not regions: a module declares those in the page registry
([architecture.md › Modules declare, Grove.Infrastructure.Server registers and serves](architecture.md#modules-declare-groveserver-registers-and-serves)).
