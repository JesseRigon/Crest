using Grove.Integrations.Microsoft;
using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Microsoft",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Microsoft Authentication"
)]

[assembly: Feature(
    Id = MicrosoftAuthenticationConstants.Features.Graph,
    Name = "Microsoft Graph",
    Category = "Connectors",
    Description = "Declares the Microsoft Graph connection (OAuth 2.0) and the Microsoft app credentials the sign-in providers share.",
    Dependencies =
    [
        "Grove.Data.Connectors",
    ]
)]

[assembly: Feature(
    Id = MicrosoftAuthenticationConstants.Features.MicrosoftAccount,
    Name = "Microsoft Account Authentication",
    Category = "Access",
    Description = "Authenticates users with their Microsoft Account, on the Microsoft app credentials.",
    Dependencies =
    [
        MicrosoftAuthenticationConstants.Features.Graph,
        "Grove.Access.Users.ExternalAuthentication",
    ]
)]

[assembly: Feature(
    Id = MicrosoftAuthenticationConstants.Features.AAD,
    Name = "Microsoft Entra ID (Azure Active Directory) Authentication",
    Category = "Access",
    Description = "Authenticates users with their Microsoft Entra ID Account, on the Microsoft app credentials.",
    Dependencies =
    [
        MicrosoftAuthenticationConstants.Features.Graph,
        "Grove.Access.Users.ExternalAuthentication",
    ]
)]

[assembly: Feature(
    Id = AzureCommunicationServices.Features.Email,
    Name = "Azure Communication Services Email",
    Category = "Communication",
    Description = "Declares the Azure Communication Services e-mail connection and sends e-mail through it.",
    Dependencies =
    [
        "Grove.Communications.Email",
        "Grove.Data.Connectors",
    ]
)]

[assembly: Feature(
    Id = AzureCommunicationServices.Features.Sms,
    Name = "Azure Communication Services SMS",
    Category = "Communication",
    Description = "Declares the Azure Communication Services SMS connection and sends SMS messages through it.",
    Dependencies =
    [
        "Grove.Communications.Sms",
        "Grove.Data.Connectors",
    ]
)]

[assembly: Feature(
    Id = MicrosoftAuthenticationConstants.Features.GraphMail,
    Name = "Microsoft Graph Email",
    Category = "Communication",
    Description = "Sends e-mail through Microsoft Graph (Exchange Online) on the Microsoft Graph connection.",
    Dependencies =
    [
        MicrosoftAuthenticationConstants.Features.Graph,
        "Grove.Communications.Email",
    ]
)]

[assembly: Feature(
    Id = MicrosoftAuthenticationConstants.Features.GraphMailbox,
    Name = "Microsoft Graph Mailbox",
    Category = "Communication",
    Description = "Receives e-mail from an Exchange Online mailbox through Microsoft Graph, on the Microsoft Graph connection.",
    Dependencies =
    [
        MicrosoftAuthenticationConstants.Features.Graph,
        "Grove.Communications.Email",
    ]
)]
