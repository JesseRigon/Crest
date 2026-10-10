using Crest.Features;
using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Features",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = FeaturesConstants.FeatureId,
    Name = "Features",
    Description = "The Features module enables the administrator of the site to manage the installed modules as well as activate and de-activate features.",
    Dependencies = ["Crest.Resources"],
    Category = "Infrastructure",
    IsAlwaysEnabled = true
)]
