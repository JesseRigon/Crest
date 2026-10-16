using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Contents.SiteMenu.Models;
using Grove.Infrastructure.Recipes;
using Grove.Infrastructure.Recipes.Services;
using Grove.Contents.Types;

namespace Grove.Contents.SiteMenu;

public sealed class Migrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(
        IRecipeMigrator recipeMigrator,
        IContentDefinitionManager contentDefinitionManager)
    {
        _recipeMigrator = recipeMigrator;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _recipeMigrator.ExecuteAsync($"menu{RecipesConstants.RecipeExtension}", this);

        await DefineLinkMenuItemFieldsAsync();

        await DefineHtmlMenuItemFieldsAsync();

        await DefineContentMenuItemFieldsAsync();

        await DefineMenuItemPermissionFieldsAsync();

        // Shortcut other migration steps on new content definition schemas.
        return 6;
    }

    // Add content menu. This only needs to run on old content definition schemas.
    // This code can be removed in a later version.
    public async Task<int> UpdateFrom1Async()
    {
        await _recipeMigrator.ExecuteAsync($"content-menu-updatefrom1{RecipesConstants.RecipeExtension}", this);

        return 2;
    }

    // Add html menu. This only needs to run on old content definition schemas.
    // This code can be removed in a later version.
    public async Task<int> UpdateFrom2Async()
    {
        await _recipeMigrator.ExecuteAsync($"html-menu-updatefrom2{RecipesConstants.RecipeExtension}", this);

        return 3;
    }

    public Task<int> UpdateFrom3Async() => Task.FromResult(4);

    // A menu's items are its MenuItems embedding (docs/content-engine.md › Embedded content items).
    public async Task<int> UpdateFrom4Async()
    {
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Menu", menu => menu
            .RemoveFieldSet("MenuItemsList")
            .WithEmbedding(MenuItemsEmbedding.Name, items => items
                .WithDisplayName("Menu items")
                .WithPosition("3")
                .Allow("@MenuItem")
                .Nested()
                .Referenceable()));

        return 5;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom5Async()
    {
        await DefineLinkMenuItemFieldsAsync();
        await DefineHtmlMenuItemFieldsAsync();
        await DefineContentMenuItemFieldsAsync();
        await DefineMenuItemPermissionFieldsAsync();
        return 6;
    }

    private Task DefineLinkMenuItemFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<LinkMenuItemFieldSet>(), builder => builder
            .WithField(nameof(LinkMenuItemFieldSet.Url), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Url")
                .WithPosition("0")
                .Locked())
            .WithField(nameof(LinkMenuItemFieldSet.Target), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Target")
                .WithPosition("1")
                .Locked()));

    private Task DefineHtmlMenuItemFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<HtmlMenuItemFieldSet>(), builder => builder
            .WithField(nameof(HtmlMenuItemFieldSet.Url), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Url")
                .WithPosition("0")
                .Locked())
            .WithField(nameof(HtmlMenuItemFieldSet.Target), field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Target")
                .WithPosition("1")
                .Locked())
            .WithField(nameof(HtmlMenuItemFieldSet.Html), field => field
                .OfType("HtmlField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Html")
                .WithPosition("2")
                .Locked()));

    private Task DefineContentMenuItemFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<ContentMenuItemFieldSet>(), builder => builder
            .WithField(nameof(ContentMenuItemFieldSet.CheckContentPermissions), field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Check content permissions")
                .WithPosition("0")
                .Locked()));

    private Task DefineMenuItemPermissionFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<MenuItemPermissionFieldSet>(), builder => builder
            .WithField(nameof(MenuItemPermissionFieldSet.PermissionNames), field => field
                .OfType("MultiTextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Permissions")
                .WithPosition("0")
                .Locked()));
}
