using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Markdown",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The markdown module enables content items to have markdown editors.",
    Dependencies = ["Crest.ContentTypes", "Crest.Shortcodes"],
    Category = "Content Management"
)]
