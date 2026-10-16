using Grove.Integrations.Facebook;
using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Meta",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Connectors"
)]

[assembly: Feature(
    Id = FacebookConstants.Features.Core,
    Name = "Meta",
    Category = "Connectors",
    Description = "Declares the Meta app credentials and the Graph API connection, and serves the Meta JavaScript SDK.",
    Dependencies =
    [
        "Grove.Data.Connectors",
    ],
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = FacebookConstants.Features.Login,
    Name = "Meta Login",
    Category = "Access",
    Description = "Authenticates users from Meta, on the Meta app credentials.",
    Dependencies =
    [
        FacebookConstants.Features.Core,
        "Grove.Access.Users.ExternalAuthentication",
    ]
)]

[assembly: Feature(
    Id = FacebookConstants.Features.Pixel,
    Name = "Meta Pixel",
    Category = "Meta",
    Description = "Meta Pixel tracking for your site, and server-side events through the Meta Conversions API connection.",
    Dependencies =
    [
        "Grove.Data.Connectors",
    ]
)]
