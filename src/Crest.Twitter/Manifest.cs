using Crest.Modules.Manifest;
using Crest.Twitter;

[assembly: Module(
    Name = "Twitter",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "X (Twitter)"
)]

[assembly: Feature(
    Id = TwitterConstants.Features.Twitter,
    Name = "X (Twitter) Integration",
    Category = "X (Twitter)",
    Description = "Provides a TwitterClient and Workflow Activities to integrate with X (Twitter)"
)]

[assembly: Feature(
    Id = TwitterConstants.Features.Signin,
    Name = "Sign in with X (Twitter)",
    Category = "X (Twitter)",
    Description = "Authenticates users with their X (Twitter) Account.",
    Dependencies =
    [
        TwitterConstants.Features.Twitter,
        "Crest.Users.ExternalAuthentication",
    ]
)]
