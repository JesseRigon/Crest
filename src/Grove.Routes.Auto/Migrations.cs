using Grove.Routes.Auto.Indexes;
using Grove.Routes.Auto.Models;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using YesSql.Sql;
using Grove.Contents.Types;

namespace Grove.Routes.Auto;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("Autoroute", builder => builder
            .Attachable()
            .WithDescription("Provides a custom url for your content item."));

        await SchemaBuilder.CreateMapIndexTableAsync<AutorouteIndex>(table => table
            .Column<string>("ContentItemId", c => c.WithLength(26))
            .Column<string>("ContainedContentItemId", c => c.WithLength(26))
            .Column<string>("JsonPath", c => c.Unlimited())
            .Column<string>("Path", col => col.WithLength(AutorouteFieldSet.MaxPathLength))
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await DefineAutorouteFieldsAsync();

        // Shortcut other migration steps on new content definition schemas.
        return 6;
    }

    // Migrate PartSettings. This only needs to run on old content definition schemas.
    // This code can be removed in a later version.
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.MigrateFieldSetSettingsAsync<AutorouteFieldSet, AutorouteFieldSetSettings>();

        return 2;
    }

    // This code can be removed in a later version.
    public static int UpdateFrom2()
    {
        return 3;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AutorouteIndex>(table => table
            .AddColumn<string>("ContainedContentItemId", c => c.WithLength(26))
        );

        await SchemaBuilder.AlterIndexTableAsync<AutorouteIndex>(table => table
            .AddColumn<string>("JsonPath", c => c.Unlimited())
        );

        await SchemaBuilder.AlterIndexTableAsync<AutorouteIndex>(table => table
            .AddColumn<bool>("Latest", c => c.WithDefault(false))
        );

        return 4;
    }

    // This code can be removed in a later version.
    public static int UpdateFrom4()
    {
        return 5;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom5Async()
    {
        await DefineAutorouteFieldsAsync();
        return 6;
    }

    private Task DefineAutorouteFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync("Autoroute", builder => builder
            .WithField(AutorouteFieldSet.PathField, field => field
                .OfType("TextField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Permalink")
                .WithPosition("0")
                .Locked())
            .WithField(AutorouteFieldSet.SetHomepageField, field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Set as home page")
                .WithPosition("1")
                .Locked())
            .WithField(AutorouteFieldSet.DisabledField, field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Disabled")
                .WithPosition("2")
                .Locked())
            .WithField(AutorouteFieldSet.RouteContainedItemsField, field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Route contained items")
                .WithPosition("3")
                .Locked())
            .WithField(AutorouteFieldSet.AbsoluteField, field => field
                .OfType("BooleanField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Absolute")
                .WithPosition("4")
                .Locked()));
}
