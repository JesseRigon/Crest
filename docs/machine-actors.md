# Machine actors — API access for CLIs and external machines

An external machine (a CLI, an integration server) acts in a tenant with its own identity,
permissions and workflow usage, without borrowing a user account. Machine identities are an
application-layer concern and belong with Workflows and Access in Grove
([architecture.md](architecture.md), [workflows.md](workflows.md)): workflows have a permission
set and per-flow access lists a machine identity can hold.

The platform's per-tenant system for machine identities is `Grove.OpenId`. It has no opaque
API keys and no machine registry. Grove's APIs accept bearer tokens through the access gate.
Points below not marked as built are from knowledge of the module and are confirmed at
design time.

## What the platform provides: `Grove.OpenId`

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

## Build order

- [ ] **1. OpenId client credentials.** Enable per tenant, accept the `Api` scheme on the Grove
  controllers and the engine gate, carry the client id into the workflow actor.
  - [x] **Enable the features.** The host's setup recipe enables the OpenId server, validation
    and management features, turns on the token endpoint with only the client-credentials flow,
    and validates against the tenant's own server (empty validation settings); Grove's sample
    recipes enable them too. Not yet verified against a running tenant.
  - [x] **Accept bearer tokens on our APIs.** The access gate authenticates the `Api` scheme for every request carrying an `Authorization` header, so
    the Grove controllers and the workflow engine gate see the machine's caller; the
    forwarder also takes additional schemes (`ApiAuthorizationOptions.AdditionalSchemes`).
  - [x] **Skip antiforgery for bearer requests** (safe: a browser never sends a bearer token on
    its own). The tenant's `IAntiforgery` is wrapped by `BearerRequestAntiforgery`: a request
    with `Authorization: Bearer …` is valid; anything else (Basic included, which browsers
    re-send) validates as before. One seam covers the MVC filters, the endpoint middleware and
    the workflow API gate.
  - [x] **Workflow attribution.** A machine-triggered run has no user; the actor snapshot
    carries the client id (`WorkflowUserContext.ClientId`, set from an application caller's
    name identifier) so journals and approvals show which machine acted. Rebuilding a machine actor's rights on resume is the separate
    "check workflow permissions" item.
  - [ ] **Check workflow permissions.** Workflow permissions fit as they are. A machine whose
    application holds `WorkflowViewer` or `WorkflowEditor` would get exactly those engine
    grants from the gate. Per-flow access lists (`Grove.Access.Edit` / `Run`) would work by
    role name. Naming a machine directly in a list compares against the caller's user name,
    which for a client-credentials token is the application's display name
    (`WorkflowDefinitionAccessCeiling`).
- [ ] **2. Opaque API keys**, only if CLI users find the token exchange a burden. The pattern
  exists: the remote deployment key is an opaque-key scheme behind the `Api`
  forwarder, with a contributor granting its caller exactly one permission.
- [ ] **3. A machine registry** (ids, last-seen, per-machine revocation), only if operating many
  machines makes deleting applications too blunt.

## Decided

- **Class.** A machine identity has its own caller class, `application`
  (`CallerClasses.Application`); it is not member-class, so the member ceiling does not apply.
  Whether an application acting for an organization takes the ceiling is access.md's open
  question.
- **Principal name.** The application's display name: the client-credentials flow adds it as
  the `name` claim; the name identifier is the client id.
- **Applications admin page.** Grove's native OpenID Connect pages manage applications
  ([admin-routes.md](admin-routes.md)).

## Open questions

- **Device flow.** Does the OpenId admin expose the device flow, or only OpenIddict underneath?
- **Where the OpenID server runs.** One OpenID server per tenant, or a central one on the
  default tenant that the others validate against? Per tenant keeps identities and secrets
  inside the tenant's database.
- **License check** on anything added beyond the platform's own module (OpenIddict is
  Apache-2.0).
