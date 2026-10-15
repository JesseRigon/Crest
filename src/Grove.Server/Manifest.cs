using Grove.Modules.Manifest;

[assembly: Module(
    Name = "Grove Server",
    Author = "Grove",
    Website = "https://grove.local",
    Version = "4.0.0.0.0",
    Description = "Provides Grove tenant APIs and server-side Grove integrations.",
    Category = "Grove"
)]

[assembly: Feature(
    Id = "Grove",
    Name = "Grove Server",
    Description = "Provides Grove tenant APIs for authentication, theme settings, and app configuration.",
    Category = "Grove",
    // Every entry here is constructor-injected or GetRequiredService'd by an
    // always-live controller/service in this assembly - see docs/feature-enablement.md
    // in the host repo for why these belong in the manifest rather than a setup recipe
    // (a recipe entry only fixes tenants provisioned from that recipe).
    //   Grove.Navigation -> INavigationManager (AdminMenus/App/Navigation
    //     controllers). Resolves today only because Grove.Admin happens to call
    //     AddNavigation() without declaring the feature; declared here so Grove does not
    //     depend on that incidental coupling.
    //   Grove.Recipes    -> IRecipeExecutor (RecipesController.ExecuteAsync).
    //   Grove.DataLocalization -> TranslationsManager (AdminMenusController's
    //     promote-rename-to-translation endpoint). This is also the feature that registers
    //     IDataLocalizer, which is how Grove's own Razor admin translates DB-backed admin
    //     menu node captions - without it a rename can be stored per-culture in the Grove
    //     layout but never promoted into the tenant's translation store, so the Razor admin
    //     and Grove would disagree about the caption.
    //   Grove.Autoroute  -> ISite.HomeRoute (SiteController's home-page-lookup
    //     endpoint, consumed by Site's Home.razor) is only ever WRITTEN by
    //     AutoroutePartHandler.PublishedAsync, which only runs while this feature is
    //     enabled - AutorouteOptions itself is a ContentManagement.Abstractions type
    //     (already referenced), but without this feature enabled, HomeRoute would stay
    //     permanently null regardless. Also brings in Grove.HomeRoute (its own
    //     manifest dependency) for free.
    // Grove.LegacyFrame is deliberately absent from Dependencies AND from any
    // Before/After hint, even though this feature needs it enabled. In Grove's
    // ordering model a module can never point at a theme in either way without creating a
    // cycle: ThemeExtensionDependencyStrategy gives every theme an implicit dependency on
    // every non-theme feature, so LegacyFrame -> Grove already exists, and any
    // Grove -> LegacyFrame edge closes the loop. The resulting fallback ordering is not
    // cosmetic - it breaks the theme's own shape/view registration, which is what made
    // legacy-frame requests silently render TheAdmin's full chrome. LegacyFrame is
    // IsAlwaysEnabled instead, which is all this feature actually needs (the theme has to
    // exist and be enabled so LegacyFrameThemeSelector can switch to it by Id at runtime;
    // its load order relative to this feature is irrelevant).
    //   Grove.ContentFields.Indexing.SQL -> TextFieldIndex/NumericFieldIndex/
    //     BooleanFieldIndex rows, which ContentItemOptionSourceProvider joins for
    //     Field:-path sort/filter pushdown. Without the feature the tables are empty
    //     and a field-sorted picker would silently return no rows.
    Dependencies = ["Grove.Access", "Grove.Admin", "Grove.AdminMenu", "Grove.Autoroute", "Grove.ContentFields", "Grove.ContentFields.Indexing.SQL", "Grove.Contents", "Grove.ContentTypes", "Grove.DataLocalization", "Grove.Indexing", "Grove.Localization", "Grove.Media", "Grove.Menu", "Grove.Navigation", "Grove.Queries", "Grove.Recipes", "Grove.Security", "Grove.Settings", "Grove.Templates", "Grove.Themes", "Grove.Users", "Grove.Icons"],
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Grove.Icons",
    Name = "Grove Grove UI Framework Icons",
    Description = "Provides packaged icon registry, local SVG icon sources, icon search, and icon pack delivery.",
    Category = "Grove",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Grove.Icons.TenantMedia",
    Name = "Grove Grove UI Framework Tenant Media Icons",
    Description = "Allows tenants to upload, index, search, and use their own SVG icons from Grove Media storage.",
    Category = "Grove",
    Dependencies = ["Grove.Icons", "Grove.Media"]
)]

[assembly: Feature(
    Id = "Grove.DesignSystem",
    Name = "Grove Design System",
    Description = "Adds tenant-level design token editing for Grove Grove UI Framework without switching Grove themes.",
    Category = "Grove",
    Dependencies = ["Grove.Settings", "Grove.Themes"]
)]
