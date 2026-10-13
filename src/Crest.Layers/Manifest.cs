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
    Description = "Places components into zones across the site's pages, by rules.",
    Dependencies =
    ["Crest.Access",
        "Crest.Scripting",
        "Crest.Rules"
    ],
    Category = "Content"
)]
