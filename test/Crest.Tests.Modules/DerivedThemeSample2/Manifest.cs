using Crest.DisplayManagement.Manifest;
using Crest.Modules.Manifest;

[assembly: Theme(
    Name = "Derived Theme Sample 2",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Derived Theme Sample 2.",
    Category = "Test",
    BaseTheme = "BaseThemeSample2"
)]
