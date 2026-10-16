using Grove.Modules.Manifest;
using Grove.Integrations.Twitter;

[assembly: Module(
    Name = "Twitter",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Connectors"
)]

[assembly: Feature(
    Id = TwitterConstants.Features.Twitter,
    Name = "X (Twitter)",
    Category = "Connectors",
    Description = "Declares the X connection (X API v2, OAuth 2.0) and the X app credentials, and a workflow activity that posts on X.",
    Dependencies =
    [
        "Grove.Data.Connectors",
    ]
)]

[assembly: Feature(
    Id = TwitterConstants.Features.Signin,
    Name = "Sign in with X (Twitter)",
    Category = "Access",
    Description = "Authenticates users with their X (Twitter) account, on the X app credentials.",
    Dependencies =
    [
        TwitterConstants.Features.Twitter,
        "Grove.Access.Users.ExternalAuthentication",
    ]
)]
