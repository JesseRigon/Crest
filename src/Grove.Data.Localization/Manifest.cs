using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Localization",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Data.Localization",
    Name = "Localization",
    Description = "Provides support for UI localization.",
    Dependencies = ["Grove.Infrastructure.Settings"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.Data.Localization.ContentLanguageHeader",
    Name = "Content Language Header",
    Description = "Adds the Content-Language HTTP header, which describes the language(s) intended for the audience.",
    Dependencies = ["Grove.Data.Localization"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.Data.Localization.AdminCulturePicker",
    Name = "Admin Culture Picker",
    Description = "Provides a culture picker shape for the admin area.",
    Dependencies = ["Grove.Data.Localization"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.Data.Localization.Translations",
    Name = "Data Localization",
    Description = "Provides support for data localization.",
    Dependencies = ["Grove.Data.Localization"],
    Category = "Internationalization"
)]
