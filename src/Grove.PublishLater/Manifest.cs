using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Publish Later",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The Publish Later module adds the ability to schedule content items to be published at a given future date and time.",
    Dependencies = ["Grove.Contents"],
    Category = "Content Management"
)]
