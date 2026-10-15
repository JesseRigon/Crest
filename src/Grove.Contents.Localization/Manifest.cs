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
    Id = "Grove.Contents.Localization",
    Name = "Content Localization",
    Description = "Provides a part that allows to localize content items.",
    Dependencies = ["Grove.Contents.Types", "Grove.Data.Localization"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.Contents.Localization.ContentCulturePicker",
    Name = "Content Culture Picker",
    Description = "Provides a culture picker shape for the frontend.",
    Dependencies = ["Grove.Contents.Localization", "Grove.Routes.Auto"],
    Category = "Internationalization"
)]

[assembly: Feature(
    Id = "Grove.Contents.Localization.Sitemaps",
    Name = "Localized Content Item Sitemaps",
    Description = "Provides support for localized content item sitemaps.",
    Dependencies = ["Grove.Contents.Localization", "Grove.Seo.Sitemaps"],
    Category = "Internationalization"
)]
