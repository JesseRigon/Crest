using Grove.DisplayManagement.Manifest;
using Grove.Modules.Manifest;

[assembly: Theme(
    Id = "Grove.LegacyFrame",
    Name = "Grove Grove UI Framework Legacy Frame",
    BaseTheme = "TheAdmin",
    Author = "Grove Grove UI Framework",
    Website = "https://github.com/Grove/Orchard-Grove",
    Version = "4.0.0.0.0",
    Description = "A stripped admin theme for rendering standard Grove admin pages inside Grove Grove UI Framework iframes.",
    Tags = new[] { ManifestConstants.AdminTag, "grove", "legacy-frame", "hidden" },
    // Always enabled rather than pulled in via Grove's Dependencies: a module
    // can never hard-depend on a theme without a load-order cycle, because
    // ThemeExtensionDependencyStrategy gives every theme an implicit dependency on every
    // non-theme feature (so Grove -> LegacyFrame -> Grove). This
    // theme is Grove-owned infrastructure (LegacyFrameThemeSelector switches to it by Id
    // at runtime), never meant to be independently toggled, so always-enabled is correct
    // regardless of the cycle.
    IsAlwaysEnabled = true
)]
