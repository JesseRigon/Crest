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

## Decisions needed

- [ ] **Retention.** How long events are kept per tenant, and whether some categories
  (impersonation, permission changes) are kept longer.
- [ ] **Organization-scoped view.** Does a member admin get an organization-scoped audit view?
- [ ] **Telling the member.** Should a member be told after the fact that staff impersonated
  them, for example a notice in their portal or an email?
