using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Templates",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Templates",
    Name = "Templates",
    Description = "Templates as content: Liquid that composes display-tree components, bound to shapes or placed by name.",
    Dependencies = ["Crest.Liquid"],
    Category = "Development"
)]
