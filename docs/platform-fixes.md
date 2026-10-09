# Platform fixes

Gaps and inefficiencies found in the platform (forked from OrchardCore) while building
Crest. Before the hard fork (2026-10-09) these were candidates to propose upstream, and each
was worked around in Crest. Now the platform is Crest's own code, so each entry is a fix to
make in the platform directly, after which the Crest-side workaround it names is removed.
"Upstream" in an entry means the platform's code as it came from Crest.

Each entry: what was found, why it matters, and (if relevant) what a fix might look like.

## Candidates

- [ ] **File #1: `IConfigureOptions<T>` registration ordering is non-deterministic across independent modules.**

  Found and fixed in Crest's localization work (see [docs/localization.md](localization.md)) — root cause of "anonymous front-end visitors ignored `Accept-Language`."

  Two independent OrchardCore modules (stock `Crest.Localization`'s `AdminCookieCultureProvider` configurator, and Crest's own `CrestCultureCookieOptionsConfiguration`) each registered an `IConfigureOptions<RequestLocalizationOptions>` that called `Insert(0, ...)` on the same `RequestCultureProviders` list. ASP.NET Core does not guarantee `IConfigureOptions<T>` ordering across independently-registered configurators for the same options type — whichever one happened to run last (determined by Orchard's feature load order, not by either module) won the front slot. This produced silent, hard-to-diagnose behavior (culture resolution "randomly" not working depending on feature registration order) rather than a startup error.

  Crest's fix was to move its own registration to `IPostConfigureOptions<T>` (guaranteed to run after every `IConfigureOptions<T>` for the same type), which sidesteps the race but only from Crest's side — any other module doing the same `IConfigureOptions<T>.Insert(0, ...)` pattern against `RequestLocalizationOptions` (or any other shared options type) is still exposed to the same non-determinism against *other* modules that haven't taken the same precaution.

  Possible platform fixes: (a) document this ordering hazard explicitly wherever OrchardCore's own modules mutate shared, order-sensitive content part lists like `RequestCultureProviders`, so other module authors know to use `IPostConfigureOptions<T>` for anything order-sensitive; (b) consider whether OrchardCore's own `AdminCookieCultureProvider` registration should itself be more defensive about ordering, since it's stock code every tenant gets by default.

  **Status:** understood and worked around internally; not yet written up for an actual OrchardCore issue/PR.

- [ ] **File #2: Admin node data-localization providers enumerate top-level nodes only.**

  Found 2026-08-18 while seeding the tenant translation store for Crest's provider-menu import.

  `LinkAdminNodeDataLocalizationProvider` / `PlaceholderAdminNodeDataLocalizationProvider` build
  their descriptor lists from `menu.MenuItems.OfType<...>()` — the menu's root level, never
  recursing into `node.Items`. Every child node's caption is therefore invisible to the
  Translations editor: it can't be discovered or translated there at all, even though
  `IDataLocalizer` would happily resolve a store entry for it at render time.

  Fix shape: recurse the node tree in both providers (a shared walk over `AdminNode.Items`).
  Crest works around it with its own `ILocalizationDataProvider` that enumerates the imported
  menu's non-root captions (see [docs/admin-menu.md](admin-menu.md)), but the gap applies to every hand-built
  admin menu on every stock install.

  **Status:** worked around in Crest for the imported menu; a platform fix would cover all menus.

- [ ] **File #3: The Translations editor's Save silently deletes non-enumerated entries.**

  Same session as #2, and multiplied by it. `DataLocalization`'s `AdminController.Save` replaces a
  culture's entire translation list with what the client posted, and the Vue editor posts what
  `GetStrings` enumerated — so any stored translation whose descriptor is not currently enumerated
  (a child-node caption per #2, an entry for a temporarily disabled feature's provider, anything
  seeded/imported out-of-band) is silently deleted by any save of that culture. Data loss with no
  warning, no diff, and no way to notice until a caption reverts.

  Fix shape: either merge instead of replace (delete only keys the editor actually displayed), or
  have `GetStrings` include stored-but-unenumerated entries so a round-trip preserves them.

  Related fragility, same controller: `GetTranslatableStringsAsync` builds
  `translations.ToDictionary("{Context}|{Key}", OrdinalIgnoreCase)` over the *stored* entries — if
  the store ever holds two entries for the same context+key (e.g. two `ILocalizationDataProvider`s
  enumerating the same caption, whose duplicate rows a save then persists twice), every
  `GetStrings` request 500s from then on, taking the whole Translations editor down until the
  store is repaired by hand. A tolerant lookup (`GroupBy` first-wins, or `TryAdd`) would degrade
  gracefully instead.

  **Status:** worked around in Crest — `CrestAdminMenuChildCaptionDataLocalizationProvider`
  enumerates all below-root admin menu captions (deduped against roots, so Save round-trips
  losslessly and never double-stores); hand-entered deep translations lost before that fix stay
  lost.

- [ ] **File #4: Translation-store cache can be re-primed stale between eviction and document commit.**

  Found 2026-08-18 chasing a "saved translation doesn't render" flake. `TranslationsManager.
  UpdateTranslationAsync` evicts the `DataCultureDictionary-{culture}` memory cache immediately,
  but the document write commits after the request's response (deferred document session). A read
  in that window rebuilds the cache from the *old* document, and nothing evicts it again — the
  stale caption then serves indefinitely, until the next save happens to evict. Upstream's own
  admin renders through the same path, so stock installs can show a just-saved translation as
  unapplied until an unrelated save.

  Fix shape: evict after commit (e.g. `DocumentStore.AfterCommitSuccess`), or version the cache
  key on the document identifier.

  **Status:** worked around in Crest's tests (settle-then-resave); no product-side workaround.

- [ ] **File #5: The "New" menu's content type captions are untranslatable from the UI.**

  Found 2026-08-18. Content type display names are translatable data ("Content Types" context:
  enumerated by `ContentTypeDataLocalizationProvider`, applied by the content-editing surfaces).
  But the New admin menu renders those same names through the nav pipeline, which resolves
  captions under the `AdminMenu(MenuName)` context — for the New branch's provider-built children
  that is the generic "Admin Menus" context, which no provider populates with type names. Net
  effect on stock installs: a type name translated in the Translations editor shows translated on
  every content surface except the very menu that creates content, and there is no UI path to
  translate it there (`DataLocalizer` does no cross-context fallback).

  Fix shape: either enumerate New-menu captions under an admin-menus context, or let the nav
  caption lookup fall back to the "Content Types" translation the way Crest now does
  (`NavigationItem.From`: ownerless caption misses "Admin Menus" → try "Content Types").

  **Status:** fixed locally in Crest (additive fallback); upstream unaffected surfaces remain.

- [ ] **File #6: No events for admin menu mutations; translations orphan on deletion.**

  Found 2026-08-19. Content definitions raise `IContentDefinitionEventHandler` events (which
  Crest uses to delete a removed content type's display-name translations), but admin menus have
  no equivalent - `IAdminMenuService.SaveAsync`/`DeleteAsync` fire nothing. Crest cleans up
  translations when nodes/menus are deleted through its own endpoints, but a deletion made
  through Orchard's stock admin-menu UI (reachable via the legacy frame) bypasses that and leaves
  the deleted node's translations orphaned in the store. Upstream itself also never cleans the
  translation store on node/menu/type deletion, so stock installs accumulate orphans
  indefinitely (which #3's editor then can't even display).

  Fix shape: admin menu mutation events (created/updated/removed, menu and node level), plus
  translation-store cleanup subscribed to them and to `ContentTypeRemoved`.

  **Status:** cleanup implemented in Crest for Crest-driven deletions and content type removal.
  The legacy-UI gap is deliberately not a Crest concern: Crest hosts don't use the stock admin
  menu screens, and the plan is for every stock admin screen to become a Crest page - this entry
  exists only because *upstream* installs orphan translations on every deletion path.

- [ ] **File #7: NavigationManager.Merge drops MenuName, breaking data-localized captions.**

  Found 2026-08-19. When `Merge` folds two same-caption menu items into one, the higher-priority
  side's values are copied onto the survivor - but the copy list (`NavigationManager.cs`,
  `Merge`) omits `MenuName` (it copies Text, Id, Href, Position, Culture, RouteValues, Url,
  Target, Permissions, Classes). The *surviving instance* is whichever item came first in the
  built list, i.e. provider DI registration order. So when an admin menu node merges with a
  provider item contributed by a module registered before Crest.AdminMenu, the merged item
  carries the node's caption and Id but a null `MenuName` - and TheAdmin's
  `NavigationItemText.cshtml` then looks the caption up under the generic "Admin Menus" context
  instead of `"Admin Menus:{menu}"`, missing the stored translation. Which captions break is
  decided per module by registration order: on a stock install with an admin menu shadowing
  provider items, some captions translate and their siblings don't, with no visible pattern.

  Fix shape: add `source.MenuName = cursor.MenuName;` to both copy blocks in `Merge`.

  **Status:** fixed permanently in Crest (`CrestMenuCaptionResolver` restores the owning menu
  from the surviving `Id`, which equals the node's `UniqueId`, before resolving; it also adds
  hierarchical context fallback - exact menu context, then parent contexts by `':'` segment,
  then the culture's best entry for the caption anywhere - so a culture that translates a
  caption never renders the invariant literal). **Framing ruling** (from when this was an
  upstream proposal): NOT a data localization bug others are hitting - stock installs sit at
  uniform default priority, where the copy block effectively never runs, and only collide if an
  admin hand-builds a DB node shadowing a provider caption; the mass-scale symptom is created
  by Crest's import (which manufactures a same-caption node per provider item and writes it at
  provider-priority-plus-one, forcing the copy path). Pitch it as completing Merge's own
  invariant instead: the copy block exists so the surviving instance is unobservable - it
  copies eleven properties of the authoritative side, and `MenuName` is the omitted twelfth,
  the one property where survivorship (an accident of provider registration order) still leaks
  into merged output. Natural companion to the AdminNode UniqueId PR, which relies on the same
  copy list carrying `Id`.
