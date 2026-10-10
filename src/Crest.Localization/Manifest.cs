using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Localization",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Crest.Localization",
    Name = "Localization",
    Description = "Provides support for UI localization.",
    Dependencies = ["Crest.Settings"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Crest.Localization.ContentLanguageHeader",
    Name = "Content Language Header",
    Description = "Adds the Content-Language HTTP header, which describes the language(s) intended for the audience.",
    Dependencies = ["Crest.Localization"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Crest.Localization.AdminCulturePicker",
    Name = "Admin Culture Picker",
    Description = "Provides a culture picker shape for the admin area.",
    Dependencies = ["Crest.Localization"],
    Category = "Internationalization"
)]
