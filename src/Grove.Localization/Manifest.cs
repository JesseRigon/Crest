using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Localization",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Localization",
    Name = "Localization",
    Description = "Provides support for UI localization.",
    Dependencies = ["Grove.Settings"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.Localization.ContentLanguageHeader",
    Name = "Content Language Header",
    Description = "Adds the Content-Language HTTP header, which describes the language(s) intended for the audience.",
    Dependencies = ["Grove.Localization"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.Localization.AdminCulturePicker",
    Name = "Admin Culture Picker",
    Description = "Provides a culture picker shape for the admin area.",
    Dependencies = ["Grove.Localization"],
    Category = "Internationalization"
)]
