# Audit

Who did what, to which record, when, from where, and on whose behalf. It is
append-only: an event is never edited or deleted through the application, only aged out
by retention. It is built on Grove.AuditTrail and its `IAuditTrailManager` /
`IAuditTrailEventHandler` seams; see [Implementation](#implementation). Still to build: feed
definitions and the caller-scoped feed API; open: feeds at volume (A4), the organization-scoped
view and telling the member ([Open questions](#open-questions)).

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

Without impersonation both answers are the same user. Both are stored on every event, both
first-class: neither is derived from the other, and no audience is tied to one; each feed
chooses what it displays.

## Design

- **A Grove index on the platform's AuditTrail.** The extra dimensions activity feeds filter on
  (record, parties, owner, organization, impersonated member, side) are a Grove index provider
  on the stock `Audit` collection; attribution uses `IAuditTrailEventHandler`. The admin UI is
  Grove Blazor pages, kept isolated with the module so they move with it
  ([architecture.md](architecture.md) › Rules that hold). Retention uses the stock trimming,
  which deletes what has expired; per-category retention policies that set an event's expiry
  are downstream.
- **AuditTrail's save path is unchanged.** The Contents audit handler is replaced by one that
  writes diffs, and workflow history is recorded through `IAuditTrailManager` as ordinary
  events; the save path itself changes only when a concrete need forces it.
- **Content history is stored as diffs.** Each event stores what changed, not a full JSON
  snapshot. Periodic full snapshots are kept, compressed, as checkpoints so that rebuilding any
  past version replays only a short run of diffs; the current published version is always kept
  as an uncompressed full snapshot.
- **Workflow history goes to the audit system.** The engine keeps its own run journal
  (execution logs, activity records); runs, hook runs and inbound webhook deliveries (including
  rejected ones) are audit events, so workflow insight pages and metrics are audit feeds and
  Workflows keeps only the wiring ([workflows.md](workflows.md)).
- **One actor handler per module that knows something.** Stock attribution takes the user from
  the request principal, which during impersonation is the member. Members' handler, run for
  every event, moves the event's user to the staff user and stores the impersonated member and
  the organization, so no individual write path needs to know about impersonation.
- **Machine actors.** An API client acting without a user is recorded with its client id as
  the actor ([machine-actors.md](machine-actors.md)).

### Who can read it

- Tenant staff with the audit permission see the tenant's trail in admin.
- Members never see the audit trail. The member UI shows functional attribution only.
- An organization's member admin sees nothing of the trail (whether they get an
  organization-scoped view is open).

### Activity feeds are filtered views of the trail

One backend: the audit trail holds all the data, and an activity feed is a filter over it, not a
second store. An invoice's activity is the trail filtered to that content item; a customer's
communications activity is the communications category filtered to that customer. What a person
sees is further limited by their permissions (only activity they own, or the record's full
activity). Front ends differ by what they ask for; the data does not.

- **Events carry what feeds filter on**, indexed: the record, the parties it concerns, the
  category, the owner, the organization, both attributions.
- **The audit API is broad but strictly secured.** Every read is filtered by the caller's
  permissions and context in the query, never after paging (the same fail-closed rule as the
  file access index).
- **Feed definitions are queries** over the audit indexes ([queries.md](queries.md)), as
  reporting is, since the filters and criteria vary so much.
- **Feeds at volume.** Stock audit listing counts every page and pages by offset, which
  slows at volume, and it is admin-only. Feeds need keyset paging (by time and event id) and the
  caller scope no caller can skip (where to build it first is open, A4).

## Implementation

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

**The trail starts after setup.** `AuditTrailManager` records nothing while the shell is
initializing, whatever the recorder (content, users, roles, settings, features). Setup migrates
every enabled feature in one pass, in dependency order, so a write audited during a migration
that runs before AuditTrail's own would flush an event into audit tables that do not exist yet
and roll back the setup's whole schema transaction.

**Index.** `AuditTrailAttributionIndex` (one row per event: actor, functional user, impersonated
member, session, organization, side, record, record type, owner) and `AuditTrailPartyIndex` (one
row per party) on the stock `Audit` collection. The stock `CorrelationId` column is 255 wide
(workflow instance ids, connection keys, role names).

**Sessions.** Sign-in, failed sign-in (on by default), sign-out (`LoggedOut`, recorded before
the sign-out by the stock `LogOff` and by `api/grove/auth/logout`, so it carries the session)
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
through it. Workflow definitions are
content items and are audited as content.

**Content history.** `ContentHistory` replaces the stock snapshot per event: each content event
stores the `JsonTreeDiff` from the item's previous recorded event, the first and every tenth
event also keep a gzip checkpoint, and the current published version's event keeps the full item
uncompressed (the previous published snapshot is compressed into a checkpoint when a newer
version is published). A rebuild replays at most nine diffs. `ContentHistoryIndex` holds each
event's position. The stock detail, diff, view and restore screens rebuild the version they show.
Every content type is audited unless the tenant excludes it
(`ContentAuditTrailSettings.ExcludedContentTypes`). Known limit: retention trims oldest first,
so a version whose checkpoint was trimmed answers "not available" instead of a wrong version.

**Workflow history.** Category `Workflows`: each burst of a run (finished, suspended, faulted,
cancelled) with the burst's journal (the engine's execution log, capped at 200 entries) and its
incidents; every hook attachment run (`HookRan`, `HookFailed`); every inbound webhook delivery
(`WebhookReceived` as the connection's machine identity, `WebhookRejected` with the status and
reason, never the body). Failures are written in a child scope that commits on its own, as access
denials are. The engine's own stores still hold the per-activity records the designer reads.

**Admin surface.** `Grove.AuditTrail.BlazorWasm` (in `Grove.AuditTrail/blazor-wasm`, isolated
with the module): the audit page (`/Audit`: actor name, impersonated member, organization,
category, record, dates, impersonated only), a record's history (`/Audit/Records/{RecordId}`)
and the impersonation sessions (`/Audit/Sessions`, each with its events). API:
`api/grove/audit` (`events`, `events/{id}`, `records/{id}`, `sessions`, `categories`), every
call decided on `ViewAuditTrail` for the gated caller; the routes are gated on the same
permission. `ViewAuditTrail` and `ManageAuditTrailSettings` are under the member class ceiling.
The admin menu's Audit Trail entry opens the new page. Staff without the permission get 403;
members never reach the page from the member shell.

**Feeds.** The data side is built: events carry and index what feeds filter on, and every
filter runs in the query before paging. What a module adds to the parties of its records goes in
its own handler's `AlterAsync` (Members adds the organization of a binding).

## Backlog

- [ ] **Feed API.** Feed definitions as saved queries over the audit indexes and the
  caller-scoped feed API with keyset paging (waits on A4).
- [ ] **Record history as an editor tab.** The record history is a page today, not a tab of the
  content editor (the editor declares no region for it).

## Open questions

- **Audit feeds at volume (A4).** (1) Build the feed API on YesSql now with keyset paging and
  the `ScopeSet` applied, and move it onto the query pipeline later as a saved query over the
  audit indexes; or (2) build feeds only on the query pipeline. Recommendation: 1.

- **Organization-scoped view.** Does a member admin get an organization-scoped audit view?
- **Telling the member.** Should a member be told after the fact that staff impersonated
  them, for example a notice in their portal or an email?
