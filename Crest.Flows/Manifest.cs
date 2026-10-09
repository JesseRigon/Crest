using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Flows",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Flows",
    Name = "Flows",
    Description = "Provides a content part allowing users to edit their content based on Widgets.",
    Dependencies = ["Crest.Widgets"],
    Category = "Content"
)]
