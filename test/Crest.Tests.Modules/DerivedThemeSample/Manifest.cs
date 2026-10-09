using Crest.DisplayManagement.Manifest;
using Crest.Modules.Manifest;

[assembly: Theme(
    Name = "Derived Theme Sample",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Derived Theme Sample.",
    Category = "Test",
    BaseTheme = "BaseThemeSample"
)]
