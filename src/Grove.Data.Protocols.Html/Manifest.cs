using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Html",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The Html module enables content items to have Html bodies.",
    Dependencies = ["Grove.Contents.Types", "Grove.Shortcodes"],
    Category = "Content Management"
)]
