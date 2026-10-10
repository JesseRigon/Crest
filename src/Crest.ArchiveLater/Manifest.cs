using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Archive Later",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The Archive Later module adds the ability to schedule content items to be archived at a given future date and time.",
    Dependencies = ["Crest.Contents"],
    Category = "Content Management"
)]
