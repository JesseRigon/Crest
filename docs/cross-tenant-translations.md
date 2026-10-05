# Cross-tenant translation suggestions ("internal Crowdin")

Planning — nothing implemented. Future feature for a multi-tenant host, built as a
**Crest feature**. The resolution chain it extends is described in
[docs/localization.md](localization.md).

## The idea

Any tenant that creates a translation for a **cross-tenant-available module string**
gets that translation surfaced as a *suggestion* in the Default tenant,
where the Default tenant's operator can review and approve it — and an approved translation becomes available
to **every** tenant. Tenant-generated content types and content items are explicitly
out of scope: their keys are tenant data and mean nothing in another tenant.

This is the internal analogue of upstream's Crowdin pipeline (suggest → proofread →
distribute), with none of the external dependency — which also fits the air-gap
ruling: the host becomes its own translation platform.

**Why this works now (and didn't before).** The invariant-literal key ruling is the enabler: module-string keys
(`msgctxt "Crest.Admin.Client"` literals, stock msgids) are **tenant-independent by
construction**, so a translation authored in tenant A is semantically valid in
tenant B. Under the old slug/caption-keyed world this feature was impossible.

## 1. Eligibility filter (at save time)

- [ ] **Classify each saved translation's (context, key).** When a tenant saves a translation via the Crest Translations editor
  (`CrestTranslationsController.SaveAsync`), classify its (context, key):
  - **Eligible**: module literals — context `Crest.Admin.Client`, or a key that exists
    as a stock msgid in the shipped catalogs. The audit classification machinery
    (`CrestPoTranslationLookup` index + template membership, see
    [docs/localization.md](localization.md) "three populations") already answers this.
  - **Excluded**: tenant-authored contexts — `Admin Menus:{menu}`, `Content Types`,
    content items, anything keyed on tenant data.

## 2. Suggestion inbox (Default tenant)

- [ ] **Emit eligible saves as suggestion records.** Eligible saves emit a suggestion record into a Default-shell document (via shell-scope
  switching; Crest already performs cross-tenant operations for the Tenants page):

  ```csharp
  class TranslationSuggestion
  {
      string Culture;         // "es"
      string Context;         // "Crest.Admin.Client" or stock msgctxt
      string Key;             // invariant literal
      string Value;           // the suggested translation
      string SourceTenant;    // provenance
      DateTime SuggestedUtc;
      SuggestionStatus Status; // Pending | Approved | Rejected | Superseded
      string? ReviewedBy; DateTime? ReviewedUtc;
  }
  ```

  Dedupe on (culture, context, key, value); a differing value for the same key becomes a
  competing suggestion, not an overwrite. Deferred-document write discipline applies
  (same one-behind trap every other Crest document writer handles).

## 3. Review surface (Default tenant operator page)

- [ ] **Build the review page.** A Crest page on the Default tenant listing pending suggestions grouped by
  culture/context/key, showing the **current resolution at each layer** for comparison
  (the Fallback-placeholder machinery already computes exactly this), with
  approve / edit-then-approve / reject, batch operations included.
- [ ] **Optional ML review pass.** The same two-pass pattern explored for Crowdin
  (suggestions → independent model review → batch approve) pointed inward — an
  auto-reviewer that screens suggestions (bad-faith content, placeholder fidelity,
  terminology consistency) before or instead of manual review. Same code, different
  target.

## 4. Distribution layer (new rung in the resolution chain)

- [ ] **Add the host approved catalog as a rung in the resolution chain.** Approved suggestions land in a **host-level approved catalog** (Default-shell
  document, or a host-owned generated `.po` — decide during implementation; document
  favored for runtime updates without redeploy). The resolution chain gains one rung,
  for every consumer of the chain (menu captions, strings endpoint, editor fallbacks):

  ```
  tenant store edit  →  HOST APPROVED CATALOG  →  PO (mirror + host files)  →  literal
  ```

  - Each tenant's own store still overrides locally (a tenant that dislikes the global
    translation keeps its own).
  - Delete-walks-down extends naturally: delete a tenant override → reveal the host
    approval → delete/reject that → reveal PO → literal.
  - Pin-to-literal keeps working at every level.

## Decisions needed

For implementation time:

- [ ] **Transport for shell→Default writes:** direct shell-scope switch at save time vs. a
  queued/background sync (save-path latency, failure isolation).
- [ ] **Approved-catalog storage:** Default-shell document read cross-shell per request
  (caching!) vs. materialized host `.po` regenerated on approval (needs no cross-shell
  read but adds a file-write pipeline). Cache invalidation across tenants either way.
- [ ] **Provenance display:** whether per-tenant Translations-editor UI should show "suggested to platform" /
  "approved platform-wide" badges (provenance display).
- [ ] **Culture governance:** which cultures accept suggestions (tie into
  `LocalizationSettings.SupportedCultures` of the Default tenant?).
- [ ] **The seam in Crest's resolvers:** does the chain-rung insertion need code in Crest's resolvers (CrestMenuCaptionResolver,
  CrestLocalizationController, CrestPoTranslationLookup)? Almost certainly yes — the
  feature contributes a provider the resolvers consume; keep the seam an interface.

## Related

- [docs/localization.md](localization.md) — resolution chain, Crowdin supply chain,
  mirror vs. private lane, literal fidelity rule.
- [docs/localization.mmd](localization.mmd) — the chain diagram this feature inserts a
  rung into.
- The Crowdin two-pass exploration (MT suggestions + ML review approvals via API)
  that inspired the review model — 2026-08-22 session.
