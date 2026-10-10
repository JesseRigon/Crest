using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Widgets",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Widgets",
    Name = "Widgets",
    Description = "Provides a part allowing content items to render Widgets in theme zones.",
    Dependencies = ["Crest.ContentTypes"],
    Category = "Content"
)]
