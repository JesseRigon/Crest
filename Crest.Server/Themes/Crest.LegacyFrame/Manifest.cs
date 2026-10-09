using Crest.DisplayManagement.Manifest;
using Crest.Modules.Manifest;

[assembly: Theme(
    Id = "Crest.LegacyFrame",
    Name = "Crest Crest UI Framework Legacy Frame",
    BaseTheme = "TheAdmin",
    Author = "Crest Crest UI Framework",
    Website = "https://github.com/Crest/Orchard-Crest",
    Version = "4.0.0.0.0",
    Description = "A stripped admin theme for rendering standard Crest admin pages inside Crest Crest UI Framework iframes.",
    Tags = new[] { ManifestConstants.AdminTag, "crest", "legacy-frame", "hidden" },
    // Always enabled rather than pulled in via Crest's Dependencies: a module
    // can never hard-depend on a theme without a load-order cycle, because
    // ThemeExtensionDependencyStrategy gives every theme an implicit dependency on every
    // non-theme feature (so Crest -> LegacyFrame -> Crest). This
    // theme is Crest-owned infrastructure (LegacyFrameThemeSelector switches to it by Id
    // at runtime), never meant to be independently toggled, so always-enabled is correct
    // regardless of the cycle.
    IsAlwaysEnabled = true
)]
