using Grove.Integrations.Google;
using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Google",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Google"
)]

[assembly: Feature(
    Id = GoogleConstants.Features.Google,
    Name = "Google",
    Category = "Connectors",
    Description = "Declares the Google connection (Google APIs, OAuth 2.0) and the Google app credentials.",
    Dependencies =
    [
        "Grove.Data.Connectors",
    ]
)]

[assembly: Feature(
    Id = GoogleConstants.Features.GoogleAuthentication,
    Name = "Google Authentication",
    Category = "Access",
    Description = "Authenticates users with their Google Account, on the Google app credentials.",
    Dependencies =
    [
        GoogleConstants.Features.Google,
        "Grove.Access.Users.ExternalAuthentication",
    ]
)]

[assembly: Feature(
    Id = GoogleConstants.Features.GoogleAnalytics,
    Name = "Google Analytics",
    Category = "Google",
    Description = "Integrate Google Analytics (gtag.js)"
)]

[assembly: Feature(
    Id = GoogleConstants.Features.GoogleTagManager,
    Name = "Google Tag Manager",
    Category = "Google",
    Description = "Integrate Google Tag Manager"
)]
