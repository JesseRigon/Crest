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
    //   Grove.Display.Navigation -> INavigationManager (AdminMenus/App/Navigation
    //     controllers). Resolves today only because Grove.Infrastructure.Shells.Admin happens to call
    //     AddNavigation() without declaring the feature; declared here so Grove does not
    //     depend on that incidental coupling.
    //   Grove.Infrastructure.Recipes    -> IRecipeExecutor (RecipesController.ExecuteAsync).
    //   Grove.Data.Localization.Translations -> TranslationsManager (AdminMenusHandlers'
    //     promote-rename-to-translation endpoint). This is also the feature that registers
    //     IDataLocalizer, which is how Grove's own Razor admin translates DB-backed admin
    //     menu node captions - without it a rename can be stored per-culture in the Grove
    //     layout but never promoted into the tenant's translation store, so the Razor admin
    //     and Grove would disagree about the caption.
    //   Grove.Routes.Auto  -> ISite.HomeRoute (SiteController's home-page-lookup
    //     endpoint, consumed by Site's Home.razor) is only ever WRITTEN by
    //     AutoroutePartHandler.PublishedAsync, which only runs while this feature is
    //     enabled - AutorouteOptions itself is a ContentManagement.Abstractions type
    //     (already referenced), but without this feature enabled, HomeRoute would stay
    //     permanently null regardless. Also brings in Grove.HomeRoute (its own
    //     manifest dependency) for free.
    //   Grove.Contents.Fields.Indexing.SQL -> TextFieldIndex/NumericFieldIndex/
    //     BooleanFieldIndex rows, which ContentItemOptionSourceProvider joins for
    //     Field:-path sort/filter pushdown. Without the feature the tables are empty
    //     and a field-sorted picker would silently return no rows.
    Dependencies = ["Grove.Access", "Grove.Infrastructure.Shells.Admin", "Grove.Contents.AdminMenu", "Grove.Routes.Auto", "Grove.Contents.Fields", "Grove.Contents.Fields.Indexing.SQL", "Grove.Contents", "Grove.Contents.Types", "Grove.Data.Localization.Translations", "Grove.Data.Indexing", "Grove.Data.Localization", "Grove.Media", "Grove.Contents.SiteMenu", "Grove.Display.Navigation", "Grove.Data.Queries", "Grove.Infrastructure.Recipes", "Grove.Security", "Grove.Infrastructure.Settings", "Grove.Display.Templates", "Grove.Themes", "Grove.Access.Users", "Grove.Display.Icons"],
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Grove.Display.Icons",
    Name = "Grove Grove UI Framework Icons",
    Description = "Provides packaged icon registry, local SVG icon sources, icon search, and icon pack delivery.",
    Category = "Grove",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "Grove.Display.Icons.TenantMedia",
    Name = "Grove Grove UI Framework Tenant Media Icons",
    Description = "Allows tenants to upload, index, search, and use their own SVG icons from Grove Media storage.",
    Category = "Grove",
    Dependencies = ["Grove.Display.Icons", "Grove.Media"]
)]

[assembly: Feature(
    Id = "Grove.DesignSystem",
    Name = "Grove Design System",
    Description = "Adds tenant-level design token editing for Grove Grove UI Framework without switching Grove themes.",
    Category = "Grove",
    Dependencies = ["Grove.Infrastructure.Settings", "Grove.Themes"]
)]
