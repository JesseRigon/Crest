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

- **The communications settings and management screens.** These cover the enabled channels,
  each channel's default provider and that provider's settings, test sends, and the delivery
  log. A channel's screens are Blazor components that the channel registers; Communications
  hosts them in one place in the admin.
- **Notifications.** A notification goes to a *user* or a group of users, never to an address,
  and is kept on record. Notifications are covered below.

## Channels

Each channel is a capability module pair. `Grove.Communications.Email` and
`Grove.Communications.Sms` exist today:

| Module | Holds |
|---|---|
| `Grove.Communications.<Channel>.Abstractions` | The message model (`MailMessage`, `SmsMessage`), the send service (`IEmailService`, `ISmsService`), the provider contract (`IEmailProvider`, `ISmsProvider`), the provider registry (`IEmailProviderResolver`, `ISmsProviderResolver`), the channel's settings and permissions |
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
- `Grove.Data.Protocols.Imap` and `Grove.Data.Protocols.Pop` receive it, when receiving is
  needed.

A protocol module knows the channel it serves, but nothing about notifications or about any
vendor.

## Integrations

An integration module wraps one vendor. Everything it registers goes into a registry that
another module owns:
- an e-mail or SMS provider, into that channel's registry;
- a login provider, into Access;
- a workflow activity or connector, into Workflows.

It never defines a capability of its own.

Each area an integration plugs into is its own feature, with its own manifest category, so the
features page lists it where it belongs. A tenant can enable one area without the others.

| Integration | Features |
|---|---|
| `Grove.Integrations.Microsoft` | Microsoft account and Entra ID sign-in (Access); Graph / Exchange mail (e-mail provider); Communication Services e-mail and SMS (providers) |
| `Grove.Integrations.Twilio` | SMS provider |
| `Grove.Integrations.Twitter` | Sign in with X (Access); posting to X (Connectors) |

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
| Communications screens | `Grove.Communications` | each channel's settings and log components |

## Code that still has to change

- The SMTP types in `Grove.Communications.Email.Abstractions` move to
  `Grove.Data.Protocols.Smtp`: `SmtpSettings`, `SmtpOptions`, `DefaultSmtpOptions`,
  `SmtpEncryptionMethod` and `SmtpDeliveryMethod`.
- The Twilio code moves to a new `Grove.Integrations.Twilio`:
  - from `Grove.Communications.Sms`: `TwilioSmsProvider`, `TwilioOptionsConfiguration` and
    `TwilioProviderOptionsConfigurations`;
  - from `Grove.Communications.Sms.Abstractions`: `TwilioOptions`, `TwilioSettings` and
    `TwilioMessageResponse`.
- `SmsNotificationProvider` moves from `Grove.Communications.Sms` to `Grove.Communications`, as
  the SMS notification method feature.
- The Azure Communication Services providers become features of
  `Grove.Integrations.Microsoft`. Today they are two separate modules:
  `Grove.Integrations.Email.Azure` (e-mail) and `Grove.Data.Protocols.Sms.Azure` (SMS).
- Each channel's settings pages move into the communications screens.
- Nothing receives e-mail yet. The receive registry and the IMAP and POP3 providers do not
  exist.
