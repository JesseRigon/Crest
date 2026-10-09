using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Content Localization",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion,
    Description = "Provides a part that allows to localize content items.",
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Crest.ContentLocalization",
    Name = "Content Localization",
    Description = "Provides a part that allows to localize content items.",
    Dependencies = ["Crest.ContentTypes", "Crest.Localization"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Crest.ContentLocalization.ContentCulturePicker",
    Name = "Content Culture Picker",
    Description = "Provides a culture picker shape for the frontend.",
    Dependencies = ["Crest.ContentLocalization", "Crest.Autoroute"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Crest.ContentLocalization.Sitemaps",
    Name = "Localized Content Item Sitemaps",
    Description = "Provides support for localized content item sitemaps.",
    Dependencies = ["Crest.ContentLocalization", "Crest.Sitemaps"],
    Category = "Internationalization"
)]
