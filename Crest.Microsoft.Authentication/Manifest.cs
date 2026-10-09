using Crest.Microsoft.Authentication;
using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Microsoft Authentication",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Microsoft Authentication"
)]

[assembly: Feature(
    Id = MicrosoftAuthenticationConstants.Features.MicrosoftAccount,
    Name = "Microsoft Account Authentication",
    Category = "Microsoft Authentication",
    Description = "Authenticates users with their Microsoft Account.",
    Dependencies =
    [
        "Crest.Users.ExternalAuthentication",
    ]
)]

[assembly: Feature(
    Id = MicrosoftAuthenticationConstants.Features.AAD,
    Name = "Microsoft Entra ID (Azure Active Directory) Authentication",
    Category = "Microsoft Authentication",
    Description = "Authenticates users with their Microsoft Entra ID Account.",
    Dependencies =
    [
        "Crest.Users.ExternalAuthentication",
    ]
)]
