using Grove.Integrations.GitHub;
using Grove.Modules.Manifest;

[assembly: Module(
    Name = "GitHub",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "GitHub"
)]

[assembly: Feature(
    Id = GitHubConstants.Features.GitHubAuthentication,
    Name = "GitHub Authentication",
    Category = "GitHub",
    Description = "Authenticates users with their GitHub Account.",
    Dependencies =
    [
        "Grove.Access.Users.ExternalAuthentication",
    ]
)]
