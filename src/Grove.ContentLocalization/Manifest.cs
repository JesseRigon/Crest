using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Content Localization",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides a part that allows to localize content items.",
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.ContentLocalization",
    Name = "Content Localization",
    Description = "Provides a part that allows to localize content items.",
    Dependencies = ["Grove.ContentTypes", "Grove.Localization"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.ContentLocalization.ContentCulturePicker",
    Name = "Content Culture Picker",
    Description = "Provides a culture picker shape for the frontend.",
    Dependencies = ["Grove.ContentLocalization", "Grove.Autoroute"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.ContentLocalization.Sitemaps",
    Name = "Localized Content Item Sitemaps",
    Description = "Provides support for localized content item sitemaps.",
    Dependencies = ["Grove.ContentLocalization", "Grove.Sitemaps"],
    Category = "Internationalization"
)]
