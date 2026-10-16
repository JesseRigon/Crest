# Communications: capabilities, protocols and integrations

How Grove sends a message to someone, and later receives one. Three kinds of module take
part. Each one plugs into the next through a registry:

| Kind | Root | Answers | Examples |
|---|---|---|---|
| Capability | `Grove.Communications.*` | What the app wants done: send this person a message | Email, SMS, notifications; later push and chat |
| Protocol | `Grove.Data.Protocols.*` | How the bytes travel: an open standard that no vendor owns | SMTP (send), IMAP and POP3 (receive); SMPP only if Grove ever talks to a carrier directly |
| Integration | `Grove.Integrations.*` | Whose service carries it: exactly one vendor per module | Microsoft, Twilio, Google, Twitter |

Protocol modules and integration modules plug into a capability the same way. A provider for
an open standard and a provider for a vendor's API are peers in the same registry: SMTP and
Microsoft Graph are both e-mail providers. A capability never names a vendor or a protocol.

Workflows, Fruitful and every other downstream module use only the capabilities, through the
API surfaces those capabilities register ([architecture.md](architecture.md) › Modules
declare, Grove.Infrastructure.Server registers and serves).

## Grove.Communications

`Grove.Communications` and `Grove.Communications.Abstractions` are the home of
communications as a whole. They hold:

- **The communications screens** (feature `Grove.Communications.Screens`, which every channel
  requires): the admin's Communications menu section. Its overview page shows each enabled
  channel's card: the send and receive providers, the connection each uses, the defaults, and
  links to the channel's settings and test send. A channel contributes its card to the
  `Communications.Channels` page region from its own WASM library ([page-regions.md](page-regions.md)),
  so Communications never references a channel. The channels' settings groups and test pages
  sit in the same menu section.
- **The delivery log.** Every send through a channel is recorded: channel, provider,
  connection, recipient, subject, outcome (sent, failed, deferred, refused, not authorized),
  the connector engine's attempts, the error and the time. The channel services write it
  (`IDeliveryLog`, resolved optionally); a provider over a connection returns a
  `DeliveryResult` built from the engine's `ConnectorResult`, and any other result is logged
  with its success alone. Each record is written in a unit of its own, so it is kept when the
  caller's unit fails. The Delivery Log page lists it newest first, filtered by channel,
  outcome and recipient, with paging, behind `ViewDeliveryLog`
  (`api/grove/communications/deliveries`).
- **Notifications.** A notification goes to a *user* or a group of users, never to an address,
  and is kept on record. Notifications are covered below.

## Channels

Each channel is a capability module pair. `Grove.Communications.Email` and
`Grove.Communications.Sms` exist today:

| Module | Holds |
|---|---|
| `Grove.Communications.<Channel>.Abstractions` | The message model (`MailMessage`, `SmsMessage`), the send service (`IEmailService`, `ISmsService`), the provider contract (`IEmailProvider`, `ISmsProvider`), the provider registry (`IEmailProviderResolver`, `ISmsProviderResolver`), the channel's settings and permissions; for e-mail also the receive side (`IEmailReceiveProvider`, `IEmailReceiveProviderResolver`, `IEmailReceiveService`, `ReceivedMailMessage`) |
| `Grove.Communications.<Channel>` | The send service, the registry, message validation, the channel's settings screens, a test send, the channel's health check, the send API surface, the `Send <channel>` workflow activity, and a log provider for development (it writes messages to the log instead of sending them) |

A channel module is vendor-neutral. A type that only one provider uses, such as its options,
settings or response model, lives in that provider's module, never in the channel.

Each channel keeps its own registry because each channel has its own message shape. A new
channel, such as push or chat, follows the same pattern. Receiving is a second registry in
the same channel, next to sending; e-mail's receive providers are IMAP, POP3 and vendor
mailboxes.

## Protocols

A protocol module implements an open standard as a provider for one or more channels:
- `Grove.Data.Protocols.Smtp` sends e-mail through any SMTP server.
- `Grove.Data.Protocols.Imap` and `Grove.Data.Protocols.Pop` receive it.

A protocol module knows the channel it serves, but nothing about notifications or about any
vendor.

## Providers run on the connector engine

Every provider, from a protocol or from an integration, sends through a connection in the
connector engine ([connectors.md](connectors.md)). The channel's registry and contract stay as
they are: the tenant picks a default provider per channel, and notifications send through
`IEmailService` and `ISmsService`. Underneath:
- **The provider is thin.** It turns the channel's message into an operation of its connection
  (Twilio's "create message", an SMTP send) and asks the engine to run it. It holds no
  credentials and no HTTP or SDK retry code.
- **Credentials live on the Connections screen**, sealed with every other external credential.
  The communications screens pick which connection each channel's provider uses; they hold no
  secrets.
- **The engine does retries, rate limits and attempt history.** Sending is not safe to repeat,
  so each send operation declares itself unsafe. The engine retries it only on errors that prove
  nothing was sent, or with an idempotency key where the service supports one.
- **A vendor SDK runs inside the engine** through an adapter that wraps the SDK call in the
  connection's credentials and policy, so working SDK code (Azure Communication Services) is not
  rewritten against REST.
- **One connection, many uses.** The Twilio connection that sends SMS also serves Workflows'
  calls to Twilio's other operations and Queries' reads of message history, with no second set
  of credentials.
- **Receiving** is a receive connection on the same channel: IMAP or POP3 over the SMTP
  account's server, or a vendor mailbox.
- **Providers run as the tenant.** A provider calls the engine as the tenant's system actor, so
  a message a member's action triggers (a password reset in the member shell) still goes out
  through a tenant connection, and member shells hold no connections of their own.

### The providers and their connections

| Provider | Module | Connection | Credential (Connections screen) |
|---|---|---|---|
| SMTP (send) | `Grove.Data.Protocols.Smtp` | a defined `smtp` connection the tenant picks | user name and password; none for an open relay |
| IMAP (receive) | `Grove.Data.Protocols.Imap` | a defined `imap` connection | user name and password |
| POP3 (receive) | `Grove.Data.Protocols.Pop` | a defined `pop` connection | user name and password |
| Twilio (SMS) | `Grove.Integrations.Twilio` | the declared `twilio` connection (REST, basic auth) | account SID or API key SID, and its auth token or secret |
| Azure Communication Services (e-mail, SMS) | `Grove.Integrations.Microsoft` | the declared `azure-communication-email` and `azure-communication-sms` connections (`sdk`) | the resource's connection string |
| Microsoft Graph (send, receive) | `Grove.Integrations.Microsoft` | the declared `microsoft-graph` connection (REST, OAuth 2.0) | the tenant's grant: the mailbox account an admin connected |

- **A mail server connection** (protocols `smtp`, `imap` and `pop`) carries its server in the
  connection's settings: `host`, `port` (SMTP 587, IMAP 993 and POP3 995 by default), `security`
  (`auto`, `none`, `ssl` or `starttls`), `acceptInvalidCertificate`, and an optional SOCKS5
  `proxyHost` and `proxyPort`. An IMAP connection may name its `folder` (`INBOX` by default).
  The protocol resolves the host and refuses a loopback, private, link-local or CGNAT address
  unless the shell allows private networks, the same rule the guarded HTTP client applies.
- **SMTP.** The provider builds the MIME message and asks the engine for a send on its
  connection; the protocol hands it to the server with MailKit. Anything that fails before the
  server accepted the envelope (DNS, connect, TLS, sign-in, a refused sender or recipient)
  is reported as not sent, so the engine may retry it; a connection lost during the transfer is
  not retried.
- **Twilio.** The connection declares Twilio's message and account operations; "create
  message" is unsafe and accepts Twilio's idempotency token, which the provider sends with
  every message, so the engine may repeat it without sending twice. Workflows and Queries run
  the same connection's other operations (list and fetch messages, fetch the account).
- **Azure Communication Services.** The provider runs the Azure SDK's send inside the engine
  (`IConnectorEngine.RunSdkAsync`) on the guarded HTTP client, with the SDK's own retries off.
  A request the service refused (4xx) or never reached is not sent; a 5xx is left unknown.
- **Microsoft Graph.** The send provider runs the connection's `sendMail` as the tenant's
  grant (Graph takes no idempotency key, so only not-sent failures are retried). The mailbox
  provider lists a folder's messages received at or after the cursor's time with
  `listMessages`, oldest first and then by id, reads each one's MIME with `getMessageMime`,
  and removes with `deleteMessage`. The cursor is the last message's received time and the ids
  already read at that time (`{time}|{id1,id2}`); the next read skips those ids, so messages
  sharing a timestamp are neither lost nor read twice.
- **IMAP and POP3** read a mailbox oldest first after a cursor (`UIDVALIDITY:UID` for IMAP, the
  last UIDL for POP3) without marking messages read, and remove messages by id.
- **The host's pickup delivery.** A development or test host can set the shell configuration
  section `Grove_Email_Smtp` (`DefaultSender`, `DeliveryMethod: SpecifiedPickupDirectory`,
  `PickupDirectoryLocationBase`, `PickupDirectoryLocation`). The SMTP module then registers a
  further e-mail provider, `SmtpPickup`, that writes each message as an `.eml` file under the
  base directory instead of sending it. Nothing leaves the host, so it uses no connection.
- **The channels' log providers** (`Log` for SMS, in development) write messages to the log
  and use no connection either.

### Settings

Each channel is a settings group in the Communications menu section: Email (`email`) and SMS
(`sms`). The channel's own section picks the default provider (and for e-mail the default
receive provider). Each provider adds its own section: whether it is enabled, its sender, and
the connection it uses, picked from the tenant's connections of its protocol. The test pages
(Email Test, SMS Test), in the same section, send through any enabled provider.

## Integrations

An integration module wraps one vendor. Everything it registers goes into a registry that
another module owns:
- a connection, into the connector engine (connectors.md › Integrations);
- an e-mail or SMS provider over that connection, into the channel's registry;
- a login provider, into Access;
- a workflow activity, into Workflows.

It never defines a capability of its own.

Each area an integration plugs into is its own feature, with its own manifest category, so the
features page lists it where it belongs. A tenant can enable one area without the others.

| Integration | Features |
|---|---|
| `Grove.Integrations.Facebook` | Meta: the Graph API connection and the Meta app credentials (Connectors); Meta Login (Access); Meta Pixel and the Conversions API connection (Meta) |
| `Grove.Integrations.GitHub` | GitHub: the connection and the GitHub app credentials (Connectors); GitHub sign-in (Access) |
| `Grove.Integrations.Google` | Google: the connection and the Google app credentials (Connectors); Google sign-in (Access); Analytics and Tag Manager |
| `Grove.Integrations.Microsoft` | Microsoft Graph: the connection and the Microsoft app credentials (Connectors); Microsoft account and Entra ID sign-in (Access); Graph / Exchange mail (e-mail provider); Communication Services e-mail and SMS (providers) |
| `Grove.Integrations.Twilio` | SMS provider |
| `Grove.Integrations.Twitter` | X: the connection, the X app credentials and posting to X (Connectors); Sign in with X (Access) |

## Notifications

Notifications live in `Grove.Communications`. They send to users through the channels, and
never through a provider directly.

- **Notification methods.** `INotificationMethodProvider` is the method registry. Each method
  delivers through one channel and is a feature that requires that channel's feature:
  - e-mail delivers through `IEmailService`;
  - SMS delivers through `ISmsService`;
  - portal delivers to the in-app inbox.

  A channel knows nothing about notifications.
- **The record.** Every notification is stored with its read state. The portal shows it in the
  admin and member shells: an indicator, the list, and marking as read.
- **Preferences.** Each user picks which methods reach them. A notification goes out through
  every method that both the user and the tenant have enabled.
- **Templates.** A notification's subject and body are Liquid templates
  ([architecture.md](architecture.md) › Direction: Blazor or headless, nothing else),
  rendered for each recipient.
- **Workflows.** `Notify users` and `Notify content owner` are the notification activities. A
  workflow that only has an address, not a user, uses the channel's own activity instead
  (`Send email`, `Send SMS`).

Downstream modules notify users through notifications: an approval request, a due invoice, a
shared file. They call a channel directly only for an outside address, such as a customer's
e-mail on an invoice.

## Registries

| Registry | Owner | Contributors |
|---|---|---|
| E-mail providers (send) | `Grove.Communications.Email` | `Grove.Data.Protocols.Smtp`, `Grove.Integrations.Microsoft`, the log provider |
| E-mail providers (receive) | `Grove.Communications.Email` | `Grove.Data.Protocols.Imap`, `Grove.Data.Protocols.Pop`, `Grove.Integrations.Microsoft` |
| SMS providers | `Grove.Communications.Sms` | `Grove.Integrations.Twilio`, `Grove.Integrations.Microsoft`, the log provider |
| Notification methods | `Grove.Communications` | its e-mail, SMS and portal features |
| Communications channels (the `Communications.Channels` page region) | `Grove.Communications` | each channel's card, from the channel's WASM library |
