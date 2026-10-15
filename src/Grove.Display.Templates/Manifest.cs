using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Templates",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Display.Templates",
    Name = "Templates",
    Description = "Templates as content: Liquid that composes display-tree components, bound to shapes or placed by name.",
    Dependencies = ["Grove.Display.Liquid"],
    Category = "Development"
)]
