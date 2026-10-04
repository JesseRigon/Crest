# Published vs public content

## Status

Not started. Sequenced after the member login (plans/shells-and-themes.md,
plans/members.md). Media visibility is out of scope here and parked; see "Media"
below.

## Two different things

- **Published** is a version state. An item has a draft and/or a published version;
  publishing makes the published version the one readers get. It says nothing about
  *who* the readers are.
- **Public** is an authorization fact: the Anonymous role may view this item.
  Public content is reachable by anyone with the link, signed in or not, from the
  public site, the member shell, or a plain `<img src>` / `fetch` on any page.

Every public item is published. Most published items are not public: accounting
records, parties, internal pages and member-only content are published so staff
and members can read them, and an anonymous caller must get nothing.

## Where public content is served

Public links are served by the tenant's server endpoints (the content view
endpoint `api/crest/content-items/{id}/view`, Orchard's own display routes,
Fruitful.Content's publishable links), never by a shell's pages. Site and member
pages, and any external consumer, read the same URL. Because the URL stays the
same, an item edited and republished in admin updates everywhere it is linked.

A public read grants exactly one item's published fields. It does not authenticate
the caller, expose any tenant, user or organization API, or reveal drafts. Every
other endpoint checks permissions the Anonymous role does not hold.

## Today's problem: published means public

When the Contents feature installs, Orchard seeds the Anonymous role with
`ViewContent` from the feature's default role stereotypes. `ViewContent` covers
every content type, and the Crest site recipe grants it to Anonymous explicitly.
So any published item of any type can be read anonymously by ID through the view
endpoint.

Public must be an explicit opt-in, never the default.

## The model

Anonymous holds no blanket `ViewContent`. An item is public when either:

1. **Its type is public.** The type is *Securable*, which gives it its own
   `View_{Type}` permission, and Anonymous is granted `View_{Type}` for that type
   only. This is Orchard's built-in per-type permission and needs no new code: the
   view endpoint's `ViewContent` check already goes through Orchard's
   `ContentTypeAuthorizationHandler`, which maps it to `View_{Type}` for securable
   types.
2. **The item is marked public.** Orchard has no per-item permission, so Crest adds
   one: a content part carrying the public flag, plus an `IAuthorizationHandler`
   that grants `ViewContent` on the item to an anonymous caller when the published
   version has the flag set. This is Orchard's intended extension point, so the
   rule stays in Crest (open source, no Fruitful dependency), and Fruitful.Content's
   publishable links mark their items through it.

In both cases only the published version is served. A draft is never public, even
on a public type or an item marked public.

## Admin surface

- A content type's settings show whether the type is public (Securable plus the
  Anonymous grant), as one toggle that sets both.
- An item editor shows a "Public" switch when the type allows per-item public
  marking, along with the item's public link.
- The content list shows a public badge so staff can see exposure at a glance.

## Media

Media files are public by URL as soon as they are uploaded, whatever the content
permissions say, unless Orchard's Secure Media feature is enabled. Making media
follow the same public-vs-published model (Secure Media plus a designated public
folder or per-asset flag) is a separate piece of work, parked until after this
plan.

## Open questions

- Is per-type public enough for the first cut, or does Fruitful.Content need
  per-item marking from the start?
- Should an item marked public in a non-public type need a separate permission
  (`PublishPublicContent`) so not every editor can expose content?

## Phases

- [ ] **1. Close the default.** Remove `ViewContent` from Anonymous in the Crest
  site recipe and in Fruitful, Crest.Host and Venti setup recipes, and stop the
  Contents stereotype from re-granting it on feature install. Test: an anonymous
  `GET .../view` on a published, non-public item returns 403/404.
- [ ] **2. Public types.** Securable plus Anonymous `View_{Type}` through recipe
  steps and the type-settings toggle. Test: an anonymous read of a public type's
  published item succeeds, its draft does not, and other types stay closed.
- [ ] **3. Public items.** The public part and its authorization handler, the item
  editor switch and the list badge. Test: the flag exposes exactly that item's
  published version anonymously and nothing else of its type.
- [ ] **4. Consumers.** The site and member shells read public content through the
  same endpoints with no shell-specific path. Fruitful.Content's publishable links
  mark their items public. Test: a site page and a member page render the same
  public item anonymously, and both update after an admin republish.
