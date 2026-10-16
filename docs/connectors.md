# Connectors: how Grove talks to external services

Every call Grove makes to something outside itself goes through one connector engine. That
covers a REST API, a SOAP service, a GraphQL endpoint, an external database and a mail or SMS
service. Four kinds of module take part:

| Kind | Root | Owns |
|---|---|---|
| Protocol | `Grove.Data.Protocols.*` | A standard library for one protocol: parsing and generating it, describing its operations, classifying its failures. Stateless. |
| Connector engine | `Grove.Data.Connectors` + `.Abstractions` | Everything stateful about talking to the outside: connections, credentials, tokens, caching, retries, rate limits, execution. |
| Integration | `Grove.Integrations.<Vendor>` | Declarations for one vendor: its connection, its components, its permissions, its place in other systems. No protocol or HTTP code of its own. |
| Consumer | Workflows, Queries, Communications, Access, … | Calls through the engine; keeps none of its own connections. |

It follows the platform rule ([architecture.md](architecture.md) › Modules declare,
Grove.Infrastructure.Server registers and serves): integrations and tenants declare
connections, the engine registers and runs them, and consumers use them.

## Protocols

A protocol module is a library for one protocol. It holds no connections and no secrets, so it
is safe for anything to use. For its protocol it:
- parses and generates the wire format;
- reads a schema into operations (an OpenAPI document, a WSDL, a GraphQL schema), with their
  inputs, outputs and paging;
- tells the engine which failures are transient, and how the protocol signals rate limits
  (`429` and `Retry-After` for HTTP);
- describes the auth schemes the protocol carries.

| Module | Protocol |
|---|---|
| `Grove.Data.Protocols.Http` | HTTP: requests, responses, the guarded client the engine runs |
| `Grove.Data.Protocols.Rest` | REST described by OpenAPI |
| `Grove.Data.Protocols.Soap` | SOAP described by WSDL |
| `Grove.Data.Protocols.GraphQL`, `.GraphQL.Client` | GraphQL: Grove's own API, and the client side (a client for Grove's API, and the protocol for external endpoints: an introspection result or SDL read into one operation per root field) |
| `Grove.Data.Protocols.OAuth` | OAuth 2.0 and OpenID Connect client flows: authorization code with PKCE, client credentials, refresh, device code |
| `Grove.Data.Protocols.Webhooks` | Inbound signature schemes (HMAC) |
| `Grove.Data.Protocols.Sql` | External databases |
| `Grove.Data.Protocols.Smtp`, `.Imap`, `.Pop` | Mail ([communications.md](communications.md)) |

A protocol module may also serve Grove's own side of that protocol, for example the OpenAPI
document (`Grove.Data.Protocols.OpenApi`) and the GraphQL API. Serving and consuming share the
parser.

A protocol implements `IConnectorProtocol` (`Grove.Data.Connectors.Abstractions`, namespace
`Grove.Data.Connectors`) and is registered with `services.AddConnectorProtocol<T>()`: its id,
the auth schemes it carries, the schema formats it reads, `ReadOperationsAsync` (schema →
operations, base URL, auth scheme, warnings), `SendAsync` (one attempt through the
`IProtocolCallContext` the engine hands it: the guarded client, the path guard, the auth, the
capped reader) and `Classify` (an exception → transient, permanent, rate limited, unauthorized
or refused, with Retry-After and "provably not sent"). A protocol may also give default
operations (a mail protocol's fixed ones), the setting and credential fields its connections
take (the Connections screen renders them), and policy defaults (larger size caps for mail),
which a connection's own policy overrides. The engine ships HTTP, REST, SOAP, GraphQL, SQL and
the inbound webhook protocol; the mail protocols are Communications'.

## The connector engine

`Grove.Data.Connectors` runs every outgoing call.

### Connections

A **connection** is a handle on one external service. It holds:
- the protocol;
- the endpoints;
- the schema, which yields the operations;
- the auth scheme;
- the credential scopes it allows;
- its policy: retries and backoff, timeout, rate limit, quota, cache lifetime and size caps.

Connections are a registry with the platform's two layers:
- **Declared** connections come from integration modules in code, for example Facebook's Graph
  API connection.
- **Defined** connections are created by a tenant admin with no module behind them. The admin
  imports an OpenAPI file, a WSDL or a GraphQL schema, or enters a plain HTTP or SQL
  connection. They are made from the same building blocks as declared connections and behave
  the same.

A module declares a connection with `services.AddConnection(key, c => c.DisplayName(..)
.Protocol(..).BaseUrl(..).OpenApi(..).OAuth2AuthorizationCode(..).AllowScopes(..).Policy(..)
.Operation(..))` (`ConnectionBuilder`), or contributes a computed layer with an
`IConnectionSource`; a declared connection wins a key clash. Defined connections live in one
tenant document (`IDefinedConnectionStore`); deleting one deletes its credentials. The registry
(`IConnectionRegistry`) merges the layers per request and caches each connection's operations
per version (a URL schema for an hour). An operation says whether it is safe to retry, whether
the service takes an idempotency key, and how it pages. A change to a connection or its
credentials raises `IConnectionChangeHandler`.

The engine lists every connection's operations for consumers: the workflow palette, Queries'
sources and components that call a service.

### Credentials

Credentials are sealed with the tenant's data protection. No API ever returns them, and a
secret that fails to decrypt is an explicit error rather than a connection that silently looks
unauthorized.

Connectors serve staff users: the admin shell. Member shells use no connections of their own,
hold no credentials and have no Connected accounts page; the connection and credential APIs
refuse member-shell callers. A member-facing feature that needs an external service calls it
through a tenant connection on the server.

A credential belongs to a scope. Each connection declares which scopes it allows:

| Scope | Holds | Managed by |
|---|---|---|
| Tenant | A service credential shared by the tenant: an API key, client credentials, an account token (Twilio) | Admins with `ManageConnections`, on the Connections screen |
| User | A staff user's personal access token, or their own OAuth grant (delegated access) | The user, on their Connected accounts page in the admin shell; an admin can revoke it but never read it |

A scope is an open value (`CredentialScope`), each with an `ICredentialScopeHandler` that names
the caller's owner in it and says who may manage it; organization and member scopes are added
as handlers. Credentials live in one tenant document, sealed under the data-protection purpose
`Connectors.Credentials` (`ICredentialStore`); the API reports only which values are set.

A call runs as its caller (`ICredentialResolver`): staff users, the system actor and machine
identities (an application, a connection's webhook caller) may call; the latter two use tenant
credentials only. On a per-user connection, the engine uses the caller's own credential
and never falls back to the tenant's unless the connection's policy allows it. Where the source
applies its own row-level security, results are cached per credential and never shared across
users.

### OAuth

The engine owns the outgoing OAuth lifecycle through `Grove.Data.Protocols.OAuth`:
- authorize and callback for authorization code, with a sealed state naming the tenant, the
  connection, the scope and the user;
- refreshing tokens before they expire, and revocation;
- client credentials and device code where the service uses them.

The OAuth client authenticates at the token, refresh and revoke endpoints with
`client_secret_post`, or HTTP Basic when the connection's auth settings say
`tokenEndpointAuth = client_secret_basic`. The state is sealed and time-limited (purpose
`Connectors.OAuthState`, `Connectors:OAuthStateLifetime`), carries the PKCE verifier, and the
callback refuses it from another tenant or another user. The callback is served on the admin
shell's path (`{admin}/api/grove/connections/oauth/callback`), the redirect URI each provider
app registers.

OAuth has three roles in Grove, and each belongs to one owner:

| Direction | What | Owner |
|---|---|---|
| Incoming | Grove is the authorization server: it issues tokens to apps we register, with their scopes and permissions | `Grove.Access.OpenId` (Server, Validation and Management features) |
| Outgoing, sign-in | A user signs in with an outside provider (Facebook, Google, Microsoft, any OpenID Connect server) | Access: its login providers and OpenId's Client feature |
| Outgoing, API access | Grove calls an outside API as the tenant or as a user | The connector engine |

Both outgoing roles use the OAuth protocol library. A vendor whose app serves both sign-in and
API access is set up once: its integration declares one set of app credentials (tenant scope)
that the sign-in provider and the connection share. Per-user grants on top of it are user
credentials.

### Execution

Every call goes through one pipeline:
- the guarded client refuses loopback, private, link-local and CGNAT addresses after DNS,
  unless the shell allows private networks (a host's dev setup does);
- responses are cached for the connection's cache lifetime, scoped to the credential;
- transient failures are retried with backoff, honouring `Retry-After` (429 included) and the
  connection's retry count;
- the connection's rate limits and quotas are enforced;
- response sizes are capped, redirects are not followed, and paths cannot leave the connection's
  base URL.

A long wait is never slept through on a worker. The engine answers "retry after T" and the
caller reschedules; a workflow resumes its activity at T. The attempts made are reported back
to the caller, so run history shows them.

In detail (`IConnectorEngine.ExecuteAsync`, `RunSdkAsync` for a vendor SDK):
- a safe operation (an HTTP GET, HEAD or OPTIONS, a mail FETCH, a SQL QUERY, or an operation
  marked safe) is retried on any transient failure and is the only kind cached; an unsafe one
  is retried only when the failure proves nothing was sent (a refused or unresolvable
  connection) or when the call carries an idempotency key the operation accepts;
- waits up to the connection's longest inline wait (5 seconds by default, lowered per call) are
  slept; a longer backoff, `Retry-After`, rate-limit window or quota period is answered as
  "retry after T";
- a refused OAuth token is refreshed once and the attempt repeated;
- rate limits are fixed windows per connection and credential owner; quotas are fixed periods
  per connection, counted on each node;
- a connection may pin its TLS server (`tls:certificateFingerprint`, SHA-256 fingerprints of
  the leaf certificates it accepts, with `es:certificateFingerprint` honoured as an alias):
  a matching certificate is accepted even when its chain is not trusted, any other refused;
- the guard's private-network switch and the ceilings are the shell's `Connectors` options
  (`AllowPrivateNetworks`, `MaxResponseBytes`, `MaxWebhookBytes`).

### Screens and permissions

- **Connections** (`/connections`, Settings): one screen for every tenant connection, declared
  and defined. It defines and imports connections, manages tenant credentials and app
  credentials, authorizes tenant grants, revokes users' credentials and shows each
  connection's operations. It requires `ManageConnections`.
- **Connected accounts** (`/connected-accounts`): each staff user's page, in the admin shell,
  for their own credentials and OAuth grants (authorize, device code, personal tokens, revoke).
  It requires `ConnectOwnAccounts`.

Both belong to the engine (`Grove.Data.Connectors.BlazorWasm`), not to any consumer, and both
call `api/grove/connections` (`ConnectionsApi`). The permissions are `ManageConnections`,
`UseConnections` (run a connection's operations from a flow, a query or a component; implied
by `ManageConnections`) and `ConnectOwnAccounts` (implied by `ManageConnections`).

## Integrations

An integration declares, and the engine and the other registries do the work. Take
`Grove.Integrations.Facebook`:
- **Its connection:** the Graph API over the REST protocol, its schema (an OpenAPI document,
  bundled or fetched, where the vendor publishes one small enough; otherwise the operations the
  integration uses, declared in code), OAuth 2 authorization code with its endpoints and scopes,
  its retry and rate policy, and the credential scopes it allows.
- **Its app credentials:** one set, used by both the sign-in provider and the connection.
- **Its components:** settings components for the display tree (the admin-settings zone), and
  chat components.
- **Its permissions.**
- **Its place in other systems:** a sign-in provider for Access, and a provider for
  Communications' chat channel.

An integration with several areas registers each as its own feature with its own manifest
category, so a tenant enables one area without the others (communications.md › Integrations).
The feature that declares the connection and the app credentials is the one the others depend
on, so the credentials are on the Connections screen whenever any area is enabled.

### App credentials

A vendor's app credentials are keyed like the connection that declares them
(`ConnectionAuth.AppCredentialsKey`). The Connections screen stores them; the
`IAppCredentialsProvider` reads them, falling back to the tenant credential of the connection
with that key. The sign-in handler takes its client id and secret from the provider when its
options are built; the integration's settings keep only what is not a secret (callback path,
store tokens, the Meta SDK's options, the Entra ID tenant). A change to the credentials or the
connection reloads the tenant, so the handler is rebuilt with them. A recipe step that carries
an app id and secret stores them as the connection's tenant credential, and each integration's
migration moved the secret its settings used to hold into the same place (X's OAuth 1.0a keys
do not carry over to an OAuth 2.0 client and were dropped).

### The shipped integrations

| Integration | Connection (protocol, auth) | App credentials | Operations | Features (category) |
|---|---|---|---|---|
| `Grove.Integrations.Facebook` | `facebook-graph` (REST, OAuth 2 code with PKCE; tenant and user) and `meta-conversions` (REST, the dataset's access token as the `access_token` query key; tenant) | `facebook-graph` | `getMe`; `sendEvents` | Meta (Connectors, dependency only: the connection, the app credentials, the JavaScript SDK whose app id is the client id), Meta Login (Access), Meta Pixel (Meta: the pixel, and the Conversions API connection behind `Send Meta conversion event`) |
| `Grove.Integrations.Google` | `google` (REST on `www.googleapis.com`, OAuth 2 code with PKCE, offline access through `authorize:` settings; tenant and user) | `google` | `getUserInfo` | Google (Connectors), Google Authentication (Access); Analytics and Tag Manager keep no connection |
| `Grove.Integrations.GitHub` | `github` (REST, OAuth 2 code with PKCE; tenant and user) | `github` | `getAuthenticatedUser`, `listRepositories` (Link paging) | GitHub (Connectors), GitHub Authentication (Access) |
| `Grove.Integrations.Microsoft` | `microsoft-graph` (REST on Graph v1.0, OAuth 2 code with PKCE on the Entra ID tenant's authority, `common` without one; tenant and user) | `microsoft-graph`, shared by Microsoft account and Entra ID sign-in | `getMe`, `listUsers` (`@odata.nextLink`), `sendMail` | Microsoft Graph (Connectors), Microsoft Account and Entra ID Authentication (Access) |
| `Grove.Integrations.Twitter` | `x` (REST on the X API v2, OAuth 2 code with PKCE, client secret sent as HTTP Basic; tenant and user) | `x` | `createPost`, `getMe` | X (Connectors: the connection and `Update X status`), Sign in with X (Access, OAuth 2.0) |

The Graph connection is computed (`MicrosoftGraphConnectionSource`, an `IConnectionSource` of
the declared layer) because its authority follows the Entra ID tenant in the settings; its
version carries the tenant, so a new tenant id is a new version. None of these vendors
publishes an OpenAPI document under a connection's response cap (Graph's and GitHub's are tens
of megabytes, Google describes its APIs with discovery documents), so each declares the
operations it uses; a tenant that needs more defines a connection of its own.

The workflow activities an integration ships call the engine as the flow's caller when it may
use connectors, else as the tenant's system actor, on the tenant credential: `Update X status`
runs `createPost` with no engine retry (a post is not idempotent), and `Send Meta conversion
event` runs `sendEvents` with the event id as its idempotency key.

### Search engines

Search engines outside Grove run their vendor SDKs on a connection the tenant defines (protocol
`sdk`); the engine opens its definition and tenant credential through `RunSdkAsync`, as the
tenant's system actor, when the SDK client is built.

| Engine | Connection | Definition | Credential |
|---|---|---|---|
| Azure AI Search | named by the Azure AI Search settings, `azure-ai-search` by default | base URL = the service endpoint; `azure:identityClientId` or `azure:authentication = ManagedIdentity` without a key | `apiKey` |
| Elasticsearch | named by the host's `Grove_Elasticsearch:Connection`, `elasticsearch` by default | base URL = the node (scheme, host, port); `es:connectionType`, `es:ports`, `es:cloudId`, `es:certificateFingerprint`, `es:enableDebugMode`, `es:enableHttpCompression` | `apiKey`, `base64ApiKey`, `keyId` and `key`, or `username` and `password` |

The clients send through the engine: their transport is a handler whose every request is one
`RunSdkAsync` call on the connection (the guarded client, the connection's rate limit, quota,
retries with `Retry-After` and response cap), and the SDKs' own retries are off so attempts never
multiply. A final HTTP answer reaches the SDK as it came; one still failing after the retries
reaches it as its last answer. Elasticsearch's transport neither sniffs nor pings (a sniffing
pool is a static one), and its certificate fingerprint pins the guarded client to the
cluster's certificate (the engine reads `es:certificateFingerprint` as the pin).

Index prefixes, analyzers and token filters stay the host's configuration. A change to Azure AI
Search's connection refreshes its options in place; a change to Elasticsearch's reloads the
tenant (its client is built once per tenant). Lucene is local and has no connection.

## Consumers

- **Workflows** keeps its activities as consumers. `Call connector` and `Poll connector` run a
  connection's operations, and the palette offers one preset per operation. The inbound webhook
  trigger verifies signatures through a narrow verifier and never reads the secret. Connector
  activities default to no engine retry, so attempts never multiply.
- **Queries** runs its sources through connections ([queries.md](queries.md) › Connections). An
  external database is a connection over the SQL protocol. The tenant's own database stays the
  data layer's, and the SQL source runs against it the same way.
- **Communications** keeps its channel registries; every e-mail and SMS provider sends through
  a connection, with SMTP, IMAP and POP run by the engine ([communications.md](communications.md)
  › Providers run on the connector engine).
- **Search engines** that live outside Grove connect through the engine and register their
  query source with Queries.
- **Access** uses the OAuth protocol library for sign-in, sharing a vendor's app credentials
  with that vendor's connection.

## Code that still has to change

- The Azure SDK's identity credentials (managed identity, `DefaultAzureCredential`) fetch their
  Entra ID tokens on their own transport, outside the engine.
- Quotas are counted per node in memory; a farm shares no count.
- The token, refresh and revoke requests and URL schema downloads go through the guarded
  client without the connection's certificate pins.
- SOAP connections send SOAP 1.1 envelopes only; a GraphQL schema given as SDL selects only
  `__typename` on object results (an introspection result selects the scalar fields).

## Later

- Member users and member organizations as credential scopes. The scope is an extensible value,
  so adding them does not change the contract.
