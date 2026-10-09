# Audit

Not started. Impersonation sessions already carry both identities (docs/members.md,
"Staff support access = impersonation"); nothing records them yet. This plan is what
records them. What is built for impersonation is in [members.md](members.md).

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

- [ ] **1. Enable and attribute.** Enable AuditTrail and its Contents feature in
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
- [ ] **2. Sessions and impersonation events.** Sign-in, sign-out, failed sign-in,
  impersonation start and stop. Test: one impersonation session produces a start
  event, its actions and a stop event, all sharing a correlation id.
  - **Sessions**: sign-in and sign-out on the staff and member surfaces, failed sign-ins,
    and impersonation start and stop (staff user, member, organization). Impersonation
    start and stop are Crest events, since Orchard has no impersonation.
- [ ] **3. Non-content writes.** Crest events for the state-changing APIs listed below.
  Test: each API's write produces exactly one event with the right actor.
  - **Non-content writes**: Crest APIs that change state without going through the
    content manager (users, roles, members and bindings, settings, features, themes,
    workflows definitions and runs). Each records a Crest event through
    `IAuditTrailManager`, and the actor handler applies to them like any other event.
- [ ] **Machine actors.** An API client acting without a user is recorded with its client
  id as the actor (docs/machine-actors.md).
- [ ] **4. Admin surface.** The audit page, the record history tab and the
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

- [ ] **5. Activity feeds are filtered views of the trail** (ruling 2026-10-06). One backend:
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

## Decisions needed

- [ ] **Change AuditTrail's save path?** The index ruling holds unless diff storage or
  taking over the workflow engine's journal needs changes to the save path. Since the hard
  fork that is an in-place change to `src/`, not a module fork; to be discussed with both in
  view.

- [ ] **Organization-scoped view.** Does a member admin get an organization-scoped audit view?
- [ ] **Telling the member.** Should a member be told after the fact that staff impersonated
  them, for example a notice in their portal or an email?
