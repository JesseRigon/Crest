using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Layers",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Layers",
    Name = "Layers",
    Description = "Enables users to render Widgets across pages of the site based on conditions.",
    Dependencies =
    [
        "Crest.Widgets",
        "Crest.Scripting",
        "Crest.Rules"
    ],
    Category = "Content"
)]
