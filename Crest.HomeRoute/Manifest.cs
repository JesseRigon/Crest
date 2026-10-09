using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Home Route",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.HomeRoute",
    Name = "Home Route",
    Description = "Provides a way to set the route corresponding to the homepage of the site",
    Category = "Infrastructure"
)]
