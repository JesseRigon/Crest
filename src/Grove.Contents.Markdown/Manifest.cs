using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Markdown",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The markdown module enables content items to have markdown editors.",
    Dependencies = ["Grove.Contents.Types", "Grove.Shortcodes"],
    Category = "Content Management"
)]
