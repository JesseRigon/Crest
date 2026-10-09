using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Publish Later",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The Publish Later module adds the ability to schedule content items to be published at a given future date and time.",
    Dependencies = ["Crest.Contents"],
    Category = "Content Management"
)]
