# Permissions: per-part access, and approvals

Two deferred pieces of the permission picture: part-level access within a content item, and
a shared approvals system for actions that need someone else's agreement before they happen.
Both sit on the one access machinery ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery)).

## Per-part access for the content-items API

**Deferred future work.** Not implemented. Item-level permission checks (`CommonPermissions.ViewContent`/`EditContent`) are what's actually enforced today. This document exists so the gap is a deliberate, tracked decision instead of a silent limitation someone rediscovers later. The content-items API that enforces item-level checks today is in [content-items.md](content-items.md).

> **Ruling 2026-10-10:** per-part permissions become **scope providers** on content fields in
> `Crest.Access` ([workflows.md › Operations](workflows.md#operations-one-registry-one-request-path-four-pipelines-one-access-machinery) › Decisions): the seam in step 1, the first
> rule when a type needs it. The provider and controller changes below describe the stock
> shape and are superseded by that.

### Per-part permissions

OrchardCore's built-in permission model is **content-type/item level**, not part level. `Crest.Contents.Security.ContentTypePermissions` (`Crest.Contents/Security/ContentTypePermissions.cs`) generates permissions per content type via `ContentTypePermissionsHelper.PermissionTemplates` (e.g. `Editable_<ContentType>`, `Viewable_<ContentType>`) — there is no native concept of "can view `CrestBlazorComponentPart` on this item but not `TitlePart` on the same item." A role either can or can't act on the whole item.

This surfaced while building `CrestContentItemsController`'s new `?parts=`-filtered read endpoint (see `Crest.Server/Controllers/ContentItemsController.cs`), which is meant to return only the requested fields/parts that the caller is actually permitted to see — for a public Site visitor, a logged-in member, or an admin, from the same endpoint, with the response shape driven entirely by permission rather than by which controller was called. The current implementation checks the *same item-level `ViewContent` permission* for every requested part — meaning "permitted" today effectively means "can view this item at all," not yet differentiated per part. The code path is deliberately structured so a real per-part check slots in later (each requested field's permission check is already an isolated step, not inlined into one big condition) — but the check itself doesn't discriminate between parts yet.

Why deferred: this is real new infrastructure (a permission provider + admin UI verification + a default-grant migration decision), not a small addition, and nothing currently consuming the content-items API needs finer-than-item-level granularity yet — the initial consumer (`BlazorCounter.razor`, a public/anonymous read of a Widget-stereotype content item) only ever needs "can this caller view this item," which item-level `ViewContent` already answers correctly. Build this when a real feature needs to differentiate access within a single content item (e.g. a content type mixing a publicly-visible part with an internal-only part), not speculatively ahead of that need.

What true per-part permissions would require:

- [ ] **Add a per-part permission provider.** A new `IPermissionProvider` (a Crest-specific one, e.g. `CrestPartPermissions`) that generates a permission per `(ContentType, PartName)` pair, mirroring how `ContentTypePermissionsHelper` generates per-content-type permissions today.
- [ ] **Verify admin UI exposure.** Admin UI exposure so these generated permissions are assignable to roles (Orchard's Roles admin page already lists dynamically-provided permissions from any registered `IPermissionProvider`, so this is mostly "define the provider correctly," not new UI work — but needs verification once actually built).
- [ ] **Swap the per-field check.** `CrestContentItemsController`'s per-field permission check swaps from `CommonPermissions.ViewContent` to the new per-part permission, keyed by the specific part name being evaluated in that loop iteration.

### Decisions needed

- [ ] **Default grants.** A decision on default grants: does an existing role that already has `Editable_<ContentType>` implicitly get every part's permission (backward-compatible default), or does nothing get granted until explicitly assigned (safer, but a breaking change for any tenant already relying on item-level grants)?

## Approvals

**Not started.** One Crest approvals system that any feature can use when an action needs
someone else's agreement before it happens, not only workflows. In the operations model an
approval is a pause on an action: the unit of work bookmarks, the request is recorded, and
the decision resumes the action as its own unit ([workflows.md](workflows.md) › Units of
work).

`Crest.Workflows` already has approvals ([workflows.md](workflows.md) › Approvals): the
`Request approval` activity records an `ApprovalTask` for a role or permission, the
`api/crest/workflows/approvals` endpoints list and decide them, and the page
`/workflows/approvals` shows them. It is reachable only from inside a workflow.

- [ ] **Move approvals to a shared place.** The general system grows out of that rather than
  beside it: the task, the decision and the queue move to a shared place, and the workflow
  activity becomes one way of requesting an approval.

### Known needs

- [ ] **Personal drive access when a notice fails.** Today the access proceeds and the
  failure is recorded ([media.md](media.md), notices); with approvals, a tenant could
  require one instead.
- [ ] **Overriding the personal drive notice delay**, as an alternative to entering a reason.
- [ ] **Downstream uses.** Request/approval forms and optional approval of generated
  purchase orders; until the shared system exists they use the `Request approval` activity.
- [ ] **Later uses.** Making content public, granting sensitive permissions, publishing
  changes to shipped definitions.

### Decisions needed

- [ ] **Who can decide.** A role, a permission, a named user, the owner of the thing affected.
- [ ] **How many approvers**, and in what order.
- [ ] **Expiry.** What happens to a request nobody decides.
- [ ] **Notifications.** How requesters and approvers are told (the platform's
  notifications, as personal drive notices are).
- [ ] **Waiting and resuming.** What the requester sees while waiting, and how a decision
  resumes the action.
- [ ] **Audit.** Every request and decision recorded in the audit trail ([audit.md](audit.md)).
