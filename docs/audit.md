# Audit

Steps 1–5 built (2026-10-13), on Crest.AuditTrail and its `IAuditTrailManager` /
`IAuditTrailEventHandler` seams; see [What landed](#what-landed-2026-10-13). Open: audit feeds
at volume (A4), the organization-scoped view and telling the member.

Who did what, to which record, when, from where, and on whose behalf. It is
append-only: an event is never edited or deleted through the application, only aged out
by retention.

## Two attributions

Every action has two answers to "who", and they are kept apart deliberately:

- **Functional attribution** is who the record belongs to. It drives ownership,
  scoping and what the UI shows. Under impersonation it is the member: an invoice a
  staff user creates while impersonating has the member as its owner and author, sits
  in the member's organization scope, and appears in the member's portal as theirs.
- **Audit attribution** is who actually acted. Under impersonation it is the staff
  user, always shown with the member they were impersonating: "created by <staff>
  impersonating <member> in <organization>". The audit trail never records an
  impersonated action as the member alone.

Without impersonation both answers are the same user.

## Build order

**A Crest index on the platform's AuditTrail (ruling 2026-10-06).** The extra
dimensions activity feeds filter on (record, parties, owner, organization, impersonated
member, side) are a Crest index provider on the stock `Audit` collection, and attribution
uses `IAuditTrailEventHandler`. The admin UI is rebuilt as Crest Blazor pages. Those pages,
and every Blazor component tied to whether a module is enabled, are kept isolated so they
move with the module when it is reworked ([architecture.md](architecture.md) › Rules that hold). Retention: Crest uses the stock
trimming, which deletes what has expired; per-category retention policies that set an
event's expiry are downstream.

**Content history is stored as diffs (ruling 2026-10-06).** Instead of a full JSON
snapshot on every save and publish (the stock Contents audit), each event stores what
changed. Periodic full snapshots are kept, compressed, as checkpoints so that rebuilding
any past version replays only a short run of diffs; the current published version is
always kept as an uncompressed full snapshot. This replaces the stock content audit
handler's snapshot behaviour, which bears on whether AuditTrail's save path must change (see
Decisions needed).

**Workflow history goes to the audit system (ruling 2026-10-06).** The engine keeps its
own run journal (execution logs, activity records); that history — runs, hook runs,
inbound webhook deliveries including rejected ones — moves onto the audit system as
audit events, so workflow insight pages and metrics are audit feeds and Workflows keeps
only the wiring ([workflows.md](workflows.md)).

Orchard's AuditTrail module stores events (`AuditTrailEvent`: event, category,
correlation id, user id and name, client IP, time) and lets handlers shape each event
before it is saved (`IAuditTrailEventHandler.CreateAsync` / `AlterAsync`). Its
Contents audit feature already records content created, updated, published,
unpublished, deleted and restored, with a snapshot of the item.

- [x] **1. Enable and attribute.** Enable AuditTrail and its Contents feature in
  Crest's recipes and every host's. Add the actor handler. Test: a content
  item created by staff while impersonating has the member as owner and author, and its
  audit event names the staff user, the member and the organization.

  Stock attribution is wrong under impersonation: the content handler takes the user from
  the request principal, which during impersonation is the member. So:

  - **One actor handler** in Crest.Members, an `IAuditTrailEventHandler`, runs for every
    event. When the session has the impersonator claim, it sets the event's user to the
    staff user and stores the impersonated member and the active organization on the
    event. It fixes every stock and Crest event in one place, so no individual write
    path needs to know about impersonation.
  - The impersonated member and organization are stored as event properties, so the audit
    UI can filter by "acted as this member" as well as "acted by this staff user".
  - **Content** (stock Contents audit): every content type, including Crest's types and
    downstream modules' types that are content items, such as parties and invoices.
- [x] **2. Sessions and impersonation events.** Sign-in, sign-out, failed sign-in,
  impersonation start and stop. Test: one impersonation session produces a start
  event, its actions and a stop event, all sharing a correlation id.
  - **Sessions**: sign-in and sign-out on the staff and member surfaces, failed sign-ins,
    and impersonation start and stop (staff user, member, organization). Impersonation
    start and stop are Crest events, since Orchard has no impersonation.
- [x] **3. Non-content writes.** Crest events for the state-changing APIs listed below.
  Test: each API's write produces exactly one event with the right actor.
  - **Non-content writes**: Crest APIs that change state without going through the
    content manager (users, roles, members and bindings, settings, features, themes,
    workflows definitions and runs). Each records a Crest event through
    `IAuditTrailManager`, and the actor handler applies to them like any other event.
- [x] **Machine actors.** An API client acting without a user is recorded with its client
  id as the actor (docs/machine-actors.md).
- [x] **4. Admin surface.** The audit page, the record history tab and the
  impersonation session view, behind the audit permission. Test: staff without the
  permission get 403; members never reach the page from the member shell.
  - An audit page in the admin shell: filter by actor, impersonated member,
    organization, category, record and date.
  - A record's history tab: every event for one content item, with impersonated actions
    marked.
  - Impersonation sessions are listed as sessions, with their events grouped under them
    through the correlation id.

  Who can read it:

  - Tenant staff with the audit permission see the tenant's trail in admin.
  - Members never see the audit trail. The member UI shows functional attribution only.
  - An organization's member admin sees nothing of the trail in the first cut. Whether
    they get an organization-scoped view is an open question.

- [ ] **5. Activity feeds are filtered views of the trail** (ruling 2026-10-06; partial, the
  data side is built, see What landed). One backend:
  the audit trail holds all the data, and an activity feed is a filter over it, not a
  second store. An invoice's activity is the trail filtered to that content item; a
  customer's communications activity is the communications category filtered to that
  customer. What a person sees is further limited by their permissions — only activity
  they own, or the record's full activity. Front ends differ by what they ask for; the
  data does not. Consequences:
  - **Events carry what feeds filter on**, indexed: the record, the parties it concerns,
    the category, the owner, the organization.
  - **Both attributions are stored, both first-class** (ruling 2026-10-06). Every event
    records who really acted (staff impersonating a member) and the functional attribution
    (the member). Neither is derived from the other, and no audience is tied to one: each
    feed chooses what it displays. "Members never see the audit trail" still holds for the
    trail itself.
  - **The audit API is broad but strictly secured.** Every read is filtered by the
    caller's permissions and context in the query, never after paging — the same
    fail-closed rule as the file access index. Important and difficult; designed as its
    own step.
  - **It leans on Crest's query system** ([queries.md](queries.md)), as reporting does: feed definitions are queries
    over the audit indexes, since the filters and criteria vary so much.
  - The likely start is reworking the platform's AuditTrail module in place, as the file
    system reworks Media; its storage structure decides how the indexes above are built.

## What landed (2026-10-13)

**Attribution.** Every event carries an `AuditTrailAttribution` (actor kind, client id, side,
organization, functional user, impersonated member, impersonation session) and an
`AuditTrailSubject` (record id and type, display text, owner, parties), both stored on the
event and set through `AuditTrailContext.Attribution` / `.Subject` by a recorder or by any
handler's `AlterAsync`. Two actor handlers fill them, each from what its module knows: Access's
`CallerAuditTrailEventHandler` (side, organization, machine actor from the gated caller) and
Members' `ImpersonationAuditTrailEventHandler`, the one that moves an impersonated event's user to
the staff user and keeps the member as the functional attribution. The manager then defaults
the functional attribution to the actor and re-normalizes the user name. The event's
`UserId` is always the audit attribution.

**Index.** `AuditTrailAttributionIndex` (one row per event: actor, functional user, impersonated
member, session, organization, side, record, record type, owner) and `AuditTrailPartyIndex` (one
row per party) on the stock `Audit` collection. The stock `CorrelationId` column is widened to
255 (workflow instance ids, connection keys, role names). Migrations were edited in place: a
tenant that enabled the audit trail before 2026-10-13 is reset.

**Sessions.** Sign-in, failed sign-in (now on by default), sign-out (`LoggedOut`, recorded before
the sign-out by the stock `LogOff` and by `api/crest/auth/logout`, so it carries the session)
and lock-out, on both surfaces; the side tells them apart. Impersonation start and stop are the
`Impersonation` category (`AuditTrailSessionEvents`, mandatory). The impersonation session id is
generated at start and kept in the session (`MemberSessionKeys.ImpersonationSessionId`); it is
the correlation id of the start and stop events and the `SessionId` of every event recorded
inside the session, which is how a session lists with its actions. (The actions keep their own
correlation id, the record's, so the record history still finds them.)

**Non-content writes.** Users: the Users audit feature (created, updated, deleted, enabled,
disabled, confirmed). Members: `BindingAdded`, `BindingRemoved` (member creation is a user
creation). Roles: created, updated, removed. Features: enabled, disabled. Settings: one event per
write naming the changed sections, never their values; site and admin theme changes are their
own `Themes` events. Settings and themes are recorded by one decorator of `ISiteService`
(`AuditingSiteService`), since every settings screen, the theme services and recipes write
through it. Nothing is recorded while the shell is being set up. Workflow definitions are
content items and are audited as content; workflow runs, see below.

**Content history as diffs.** `ContentHistory` replaces the stock snapshot per event: each
content event stores the `JsonTreeDiff` from the item's previous recorded event, the first and
every tenth event also keep a gzip checkpoint, and the current published version's event keeps
the full item uncompressed (the previous published snapshot is compressed into a checkpoint when
a newer version is published). A rebuild replays at most nine diffs. `ContentHistoryIndex`
holds each event's position. The stock detail, diff, view and restore screens rebuild the
version they show. Every content type is audited unless the tenant excludes it
(`ContentAuditTrailSettings.ExcludedContentTypes`, replacing the allow-list). Known limit:
retention trims oldest first, so a version whose checkpoint was trimmed answers "not
available" instead of a wrong version.

**Workflow history.** Category `Workflows`: each burst of a run (finished, suspended, faulted,
cancelled) with the burst's journal (the engine's execution log, capped at 200 entries) and its
incidents; every hook attachment run (`HookRan`, `HookFailed`); every inbound webhook delivery
(`WebhookReceived` as the connection's machine identity, `WebhookRejected` with the status and
reason, never the body). Failures are written in a child scope that commits on its own, as access
denials are. The engine's own stores still hold the per-activity records the designer reads.

**Admin surface.** `Crest.AuditTrail.BlazorWasm` (in `Crest.AuditTrail/blazor-wasm`, isolated
with the module): the audit page (`/Audit`: actor name, impersonated member, organization,
category, record, dates, impersonated only), a record's history (`/Audit/Records/{RecordId}`)
and the impersonation sessions (`/Audit/Sessions`, each with its events). API:
`api/crest/audit` (`events`, `events/{id}`, `records/{id}`, `sessions`, `categories`), every
call decided on `ViewAuditTrail` for the gated caller; the routes are gated on the same
permission. `ViewAuditTrail` and `ManageAuditTrailSettings` are under the member class ceiling.
The admin menu's Audit Trail entry opens the new page. The record history is a page, not yet
a tab of the content editor (the editor declares no region for it).

**Feeds (step 5).** The data side is built: events carry and index what feeds filter on (record,
parties, category, owner, organization, both attributions), and every filter runs in the query
before paging. What a module adds to the parties of its records goes in its own handler's
`AlterAsync` (Members adds the organization of a binding). Not built: feed definitions as saved
queries over these indexes and the caller-scoped feed API, which wait on A4.

## Decisions needed

- [x] **Change AuditTrail's save path?** Ruled (A0): **leave it as it is for now.** The
  Contents audit handler is replaced by one that writes diffs with compressed checkpoints,
  and workflow history is recorded through `IAuditTrailManager` as ordinary events; the save
  path itself changes only when a concrete need forces it (an in-place platform change since
  the hard fork, not a module fork).
- [ ] **Audit feeds at volume (A4).** Stock audit listing counts every page and pages by
  offset, which slows at volume, and it is admin-only. Feeds need keyset paging (by time and
  event id) and the caller scope no caller can skip. Options: (1) build the feed API on
  YesSql now with keyset paging and the `ScopeSet` applied, and move it onto the query
  pipeline as a saved query over the audit indexes; (2) wait and build feeds only on the
  query pipeline. Recommendation: 1.

- [ ] **Organization-scoped view.** Does a member admin get an organization-scoped audit view?
- [ ] **Telling the member.** Should a member be told after the fact that staff impersonated
  them, for example a notice in their portal or an email?
