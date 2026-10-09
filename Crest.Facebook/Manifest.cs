using Crest.Facebook;
using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Meta",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Category = "Meta"
)]

[assembly: Feature(
    Id = FacebookConstants.Features.Core,
    Name = "Meta Core Components",
    Category = "Meta",
    Description = "Registers the core components used by the Meta features.",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Id = FacebookConstants.Features.Login,
    Name = "Meta Login",
    Category = "Meta",
    Description = "Authenticates users from Meta.",
    Dependencies =
    [
        FacebookConstants.Features.Core,
        "Crest.Users.ExternalAuthentication",
    ]
)]

[assembly: Feature(
    Id = FacebookConstants.Features.Widgets,
    Name = "Meta Social Plugins Widgets",
    Category = "Meta",
    Description = "Integrates Meta social plugins as predefined widgets",
    Dependencies =
    [
        FacebookConstants.Features.Core,
        "Crest.Widgets",
        "Crest.Recipes.Core",
    ]
)]

[assembly: Feature(
    Id = FacebookConstants.Features.Pixel,
    Name = "Meta Pixel",
    Category = "Meta",
    Description = "Provides a way to enable Meta Pixel tracking for your site."
)]
