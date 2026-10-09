# Machine actors — API access for CLIs and external machines

**Notes only, 2026-09-30. Not designed, not started; to be implemented later.** Written
after workflows phase 5 ([workflows.md](workflows.md)), which gave workflows a permission
set and per-flow access lists that a machine identity should be able to hold. Machine
identities are an application-layer concern, so this plan belongs with Workflows in Crest
([application-layer.md](architecture.md)).

The question: can an external machine (a CLI, an integration server) act in a tenant with
its own identity, permissions and workflow usage, or must it borrow a user account?

Short answer: the platform has a per-tenant system for machine identities
(`Crest.OpenId`), so no user account is needed. It has no opaque API keys and no
machine registry. Crest's own APIs are cookie-only today and would need to accept
bearer tokens.

**How much of this is verified:** that `Crest.OpenId` is in the platform and that the host's setup recipe does
not enable it. Everything else below is from knowledge of the module and has not been
re-read in the source; confirm each point at design time.

## What the platform provides: `Crest.OpenId`

- **Applications are machine identities, per tenant.** Each tenant can run its own OpenID
  Connect server. An "application" registered there has a client id and secret and is
  stored in that tenant's database.
- **The client-credentials flow needs no user.** A CLI posts its id and secret to the
  tenant's `/connect/token` and gets a short-lived bearer token.
- **Permissions go through roles.** For client-credentials applications, Orchard roles are
  assigned to the application itself. The token carries them as role claims and the normal
  authorization pipeline evaluates them.
- **APIs accept the token through the `Api` authentication scheme**, which the OpenId
  validation feature backs. Controllers opt in with
  `[Authorize(AuthenticationSchemes = "Api")]`.
- **Device flow, for a CLI acting as a signed-in person** ("open this URL and enter the
  code"): the underlying OpenIddict library supports it. Whether Orchard's admin UI exposes
  it is unconfirmed.

## What it does not provide

- **Opaque API keys.** No static `sk_...` key with an admin screen to create, scope and
  revoke it. The nearest built-in is the client id and secret exchanged for a token.
  Paste-a-key simplicity would be a small custom authentication handler plus a key store,
  mapping each key to roles.
- **A machine or device registry.** Nothing tracks machine ids, last-seen, or per-machine
  revocation beyond deleting the application.

## Build order, when this is picked up

- [ ] **1. OpenId client credentials.** Enable per tenant, accept the `Api` scheme on the Crest
  controllers and the engine gate, carry the client id into the workflow actor.
  - [ ] **Enable the features.** The OpenId server and validation features are not in the setup
    recipe.
  - [ ] **Accept bearer tokens on our APIs.** The Crest controllers (`api/crest/...`) and the
    workflow engine gate (`CrestWorkflowsApiSecurityMiddleware`) authenticate with the
    Orchard cookie and require an antiforgery token on writes. For machines they would accept
    the `Api` scheme as well and skip antiforgery for bearer requests - safe, because a
    browser never sends a bearer token on its own.
  - [ ] **Workflow attribution.** A machine-triggered run has no user. The actor snapshot
    (`WorkflowUserContext`, workflow input `Actor`) would carry the client id so journals and
    approvals show which machine acted.
  - [ ] **Check workflow permissions.** Workflow permissions fit as they are. A machine whose
    application holds `WorkflowViewer` or `WorkflowEditor` would get exactly those engine
    grants from the gate. Per-flow access lists (`Crest.Access.Edit` / `Run`) would work by
    role name. Naming a machine directly in a list is uncertain:
    `WorkflowDefinitionAccessHandler` compares entries against the principal's name, and how
    an application principal populates it is unchecked.
- [ ] **2. Opaque API keys**, only if CLI users find the token exchange a burden.
- [ ] **3. A machine registry** (ids, last-seen, per-machine revocation), only if operating many
  machines makes deleting applications too blunt.

## Decisions needed

- [ ] **Member ceiling.** Decide whether a machine identity is staff-class or gets its own
  class in the member permission ceiling (`Crest.Members`).
- [ ] **Device flow.** Does Orchard's OpenId admin expose the device flow, or only OpenIddict
  underneath?
- [ ] **Principal name.** What is `Identity.Name` on a client-credentials principal (needed for
  per-flow lists and for the actor snapshot)?
- [ ] **Where the OpenID server runs.** One OpenID server per tenant, or a central one on the
  default tenant that the others validate against? Per tenant keeps identities and secrets
  inside the tenant's database.
- [ ] **Applications admin page.** Does Crest get an admin page for applications, or does the
  stock Orchard one stay?
- [ ] **License check** on anything added beyond Orchard's own module (OpenIddict is Apache-2.0).
