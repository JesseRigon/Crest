using Grove.Integrations.GitHub;
using Grove.Modules.Manifest;

[assembly: Module(
    Name = "GitHub",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Connectors"
)]

[assembly: Feature(
    Id = GitHubConstants.Features.GitHub,
    Name = "GitHub",
    Category = "Connectors",
    Description = "Declares the GitHub connection (REST API, OAuth 2.0) and the GitHub app credentials.",
    Dependencies =
    [
        "Grove.Data.Connectors",
    ]
)]

[assembly: Feature(
    Id = GitHubConstants.Features.GitHubAuthentication,
    Name = "GitHub Authentication",
    Category = "Access",
    Description = "Authenticates users with their GitHub Account, on the GitHub app credentials.",
    Dependencies =
    [
        GitHubConstants.Features.GitHub,
        "Grove.Access.Users.ExternalAuthentication",
    ]
)]
