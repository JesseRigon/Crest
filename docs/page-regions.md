# Page regions — UI injection across modules

How a downstream module puts UI on a page owned by a module it must not be referenced by.

Implemented as `IPageRegionContributor` / `PageRegionRegistry` / `<CrestPageRegion>` in
`OrchardCore.Crest.Components/Regions`, discovered over the module assemblies in
`AdminRoutes.razor`. Crest.Parties declares regions on its party-type panes
(`PartiesPageRegions.Pane(...)`, `PartiesPageRegions.Detail(...)`) for other modules to
contribute to. The reasoning that produced it:

A downstream module often must appear on screens it does not own: a tax module's per-line
tax and document summary on a transaction editor, an exemption panel on the party editor.
The dependency rule applies — **the page's module must not reference the downstream module
to render its contribution** — so the UI contribution has to be injected the way a
downstream module's content parts are.

**No seam existed for this.** `RenderFragment` slots across `OrchardCore.Crest.Components`
are *caller-supplied*: they let a page pass content into a component it uses, not a foreign
module push content into a page. `IRouteComponentTableProvider` is the right *shape* —
modules declare their own contributions, the host discovers by convention — but it registers
whole routes, not regions within a page.

The seam is its page-region analogue: pages declare named regions, downstream modules
register components against region keys with an order, the shell resolves at render. Nothing
registered renders nothing, which is the correct disabled behaviour. Region keys belong to
the module owning the page, in its `Domain`.

It is a **Crest platform concern**, since every module contributing UI to another's screen
hits it, which is why it lives in Crest.Components.
