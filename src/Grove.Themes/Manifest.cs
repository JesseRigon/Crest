using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Themes",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "The Themes modules allows you to specify the Front and the Admin theme.",
    Dependencies = ["Grove.Admin"],
    Category = "Theming"
)]
