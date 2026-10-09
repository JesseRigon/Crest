using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Alias",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The alias module enables content items to have custom logical identifier.",
    Dependencies = ["Crest.ContentTypes"],
    Category = "Content Management"
)]
