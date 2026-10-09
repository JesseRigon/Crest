using Crest.Modules.Manifest;

[assembly: Module(
    Name = "Crest Server",
    Author = "Crest",
    Website = "https://crest.local",
    Version = "4.0.0.0.0",
    Description = "Provides Crest tenant APIs and server-side Crest integrations.",
    Category = "Crest"
)]

[assembly: Feature(
    Id = "Crest",
    Name = "Crest Server",
    Description = "Provides Crest tenant APIs for authentication, theme settings, and app configuration.",
    Category = "Crest",
    // Every entry here is constructor-injected or GetRequiredService'd by an
    // always-live controller/service in this assembly - see docs/feature-enablement.md
    // in the host repo for why these belong in the manifest rather than a setup recipe
    // (a recipe entry only fixes tenants provisioned from that recipe).
    //   Crest.Navigation -> INavigationManager (AdminMenus/App/Navigation
    //     controllers). Resolves today only because Crest.Admin happens to call
    //     AddNavigation() without declaring the feature; declared here so Crest does not
    //     depend on that incidental coupling.
    //   Crest.Recipes    -> IRecipeExecutor (RecipesController.ExecuteAsync).
    //   Crest.DataLocalization -> TranslationsManager (AdminMenusController's
    //     promote-rename-to-translation endpoint). This is also the feature that registers
    //     IDataLocalizer, which is how Crest's own Razor admin translates DB-backed admin
    //     menu node captions - without it a rename can be stored per-culture in the Crest
    //     layout but never promoted into the tenant's translation store, so the Razor admin
    //     and Crest would disagree about the caption.
    //   Crest.Autoroute  -> ISite.HomeRoute (SiteController's home-page-lookup
    //     endpoint, consumed by Site's Home.razor) is only ever WRITTEN by
    //     AutoroutePartHandler.PublishedAsync, which only runs while this feature is
    //     enabled - AutorouteOptions itself is a ContentManagement.Abstractions type
    //     (already referenced), but without this feature enabled, HomeRoute would stay
    //     permanently null regardless. Also brings in Crest.HomeRoute (its own
    //     manifest dependency) for free.
    // Crest.LegacyFrame is deliberately absent from Dependencies AND from any
    // Before/After hint, even though this feature needs it enabled. In Crest's
    // ordering model a module can never point at a theme in either way without creating a
    // cycle: ThemeExtensionDependencyStrategy gives every theme an implicit dependency on
    // every non-theme feature, so LegacyFrame -> Crest already exists, and any
    // Crest -> LegacyFrame edge closes the loop. The resulting fallback ordering is not
    // cosmetic - it breaks the theme's own shape/view registration, which is what made
    // legacy-frame requests silently render TheAdmin's full chrome. LegacyFrame is
    // IsAlwaysEnabled instead, which is all this feature actually needs (the theme has to
    // exist and be enabled so LegacyFrameThemeSelector can switch to it by Id at runtime;
    // its load order relative to this feature is irrelevant).
    //   Crest.ContentFields.Indexing.SQL -> TextFieldIndex/NumericFieldIndex/
    //     BooleanFieldIndex rows, which ContentItemOptionSourceProvider joins for
    //     Field:-path sort/filter pushdown. Without the feature the tables are empty
    //     and a field-sorted picker would silently return no rows.
    Dependencies = ["Crest.Admin", "Crest.AdminMenu", "Crest.Autoroute", "Crest.ContentFields", "Crest.ContentFields.Indexing.SQL", "Crest.Contents", "Crest.DataLocalization", "Crest.Indexing", "Crest.Localization", "Crest.Media", "Crest.Menu", "Crest.Navigation", "Crest.Queries", "Crest.Recipes", "Crest.Security", "Crest.Settings", "Crest.Templates", "Crest.Themes", "Crest.Users", "Crest.Icons"],
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Crest.Icons",
    Name = "Crest Crest UI Framework Icons",
    Description = "Provides packaged icon registry, local SVG icon sources, icon search, and icon pack delivery.",
    Category = "Crest",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Crest.Icons.TenantMedia",
    Name = "Crest Crest UI Framework Tenant Media Icons",
    Description = "Allows tenants to upload, index, search, and use their own SVG icons from Crest Media storage.",
    Category = "Crest",
    Dependencies = ["Crest.Icons", "Crest.Media"]
)]

[assembly: Feature(
    Id = "Crest.DesignSystem",
    Name = "Crest Design System",
    Description = "Adds tenant-level design token editing for Crest Crest UI Framework without switching Crest themes.",
    Category = "Crest",
    Dependencies = ["Crest.Settings", "Crest.Themes"]
)]
