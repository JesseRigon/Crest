using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Shortcodes",
    Author = ManifestConstants.PlatformTeam,
    Website = ManifestConstants.PlatformWebsite,
    Version = ManifestConstants.PlatformVersion
)]

[assembly: Feature(
    Id = "Grove.Shortcodes",
    Name = "Shortcodes",
    Description = "The Shortcodes feature adds shortcode capabilities.",
    Category = "Infrastructure"
)]

[assembly: Feature(
    Id = "Grove.Shortcodes.Templates",
    Name = "Shortcode Templates",
    Description = "The Shortcode Templates feature provides a way to write custom shortcode templates from the admin.",
    Category = "Content",
    Dependencies = ["Grove.Display.Liquid", "Grove.Shortcodes"]
)]
