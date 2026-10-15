# Shortcodes

Status: **retiring.** Shortcodes are removed; what they do becomes Liquid templates and
components ([blazor-display.md](blazor-display.md)). Until the code is gone, this file records
how they work and the media-reference work on them. Shortcodes are the tags editors type into
content text (`[image]…[/image]`, `[asset_url]`, `[locale en]…[/locale]`, and template
shortcodes an admin defines in Liquid).

## How they work today

- **Parsing**: the `Shortcodes` library (Sébastien Ros) parses `[name args]content[/name]` and
  self-closing `[name args]`.
- **Processing**: `IShortcodeService.ProcessAsync(html)` (`Grove.Shortcodes`) runs over rendered
  content text at display time: the Markdown body and field drivers, the HTML body, the Markdown
  GraphQL output, the Razor `Markdownify` helper. Each tag goes to the `IShortcodeProvider`s; the
  first that recognises the name produces the output.
- **Providers**:

  | Shortcode | Module | Does |
  | --- | --- | --- |
  | `[image]` | Media | an `<img>` for a media file, with resize arguments |
  | `[asset_url]` | Media | the file's URL |
  | `[locale]` | Shortcodes | its content only for the current culture |
  | template shortcodes | Shortcodes (Templates feature) | defined in the admin, body written in Liquid |
  | options shortcodes | Shortcodes | registered in code |

- **Liquid's side**: the `shortcode` Liquid filter (`Grove.Display.Liquid`, when Shortcodes is on)
  expands shortcodes inside a string from a template; template shortcodes are themselves Liquid.

## Media references: ids and paths ([media.md](media.md))

Media is stored by placement id; a path is a placement in the library drive. A shortcode must work
with both, because content written before ids existed holds paths, and new content should hold ids
so it survives renames and moves.

Forms the media shortcodes take:

| Form | Example | Resolution |
| --- | --- | --- |
| Body, guessed | `[image]products/chair.jpg[/image]`, `[image]01hx…[/image]` | a 26-character Crockford id is tried as a placement id, else as a library path; `/files/…` and `/media/…` URLs are understood; `~/…` and absolute URLs pass through |
| Explicit id | `[image id="01hx…"]` | the placement; nothing when it is gone or not visible to the reader |
| Explicit path | `[asset_url path="products/chair.jpg"]` | the library path |
| Both | `[image id="01hx…" path="products/chair.jpg"]` | the id first, the path as the fallback |

Every form resolves through `IMediaReferenceResolver`: the access gate decides, a public file
gets its (CDN) URL, a private one a short-lived signed URL, and a fragment holding a signed URL is
never cached for everyone (docs/media.md › Serving and CDNs).

### Tasks

- [x] **Resolve ids and paths** in `[image]` and `[asset_url]`.
- [x] **Explicit `id` and `path` arguments**, id first with the path as fallback
  (`Grove.Media/Shortcodes/MediaShortcodeReference.cs`; compiled, not yet tested).
- [ ] **Tests** for the explicit forms (id, path, both, id gone with path fallback, private file
  signed, invisible file rendering nothing).
- [ ] **Write ids on save**: a content handler that rewrites path-form media shortcodes in body
  text to the both-form (`id` plus the readable `path`), as `MediaFieldRewriter` does for fields, so
  stored text survives a rename; and the same for imported content.
- [ ] **The editor inserts ids**: the media picker in the HTML and Markdown editors inserts the
  both-form, not a bare path.
- [ ] **Search indexing**: body text is indexed with its shortcodes; index the resolved file name
  or title, not the raw id.
- [ ] **Other providers**: none besides Media's reference files today; template shortcodes reach
  media through Liquid `asset_url`, which already takes ids and paths.

## Liquid and shortcodes

Shortcodes do not stay a separate system: everything they do is Liquid templates and
components (blazor-display.md). Template shortcodes are already Liquid, and media reaches
Liquid through `asset_url`, which takes ids and paths.
