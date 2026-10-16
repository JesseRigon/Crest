using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Contents.Hierarchy.Indexes;
using Grove.Contents.Hierarchy.Models;
using YesSql.Sql;
using Grove.Contents.Types;

namespace Grove.Contents.Hierarchy;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("ChildContentItems", builder => builder
            .Attachable()
            .WithDescription("Add a list behavior."));

        await SchemaBuilder.CreateMapIndexTableAsync<ParentContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ListContentItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(ParentContentItemIndex.MaxDisplayTextSize))
            .Column<int>("Order")
            .Column<string>("ListContentType")
            .Column<bool>("Published")
            .Column<bool>("Latest")

        );

        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .CreateIndex("IDX_ParentContentItemIndex_DocumentId",
                "Id",
                "DocumentId",
                "ContentItemId",
                "ListContentItemId",
                "DisplayText",
                "Order",
                "ListContentType",
                "Published",
                "Latest")
        );

        await DefineHierarchyFieldsAsync();

        // Shortcut other migration steps on new content definition schemas.
        return 5;
    }

    // Migrate PartSettings. This only needs to run on old content definition schemas.
    // This code can be removed in a later version.
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.MigrateFieldSetSettingsAsync<ChildContentItemsFieldSet, ChildContentItemsFieldSetSettings>();

        return 2;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .CreateIndex("IDX_ParentContentItemIndex_DocumentId",
            "DocumentId",
            "ListContentItemId",
            "Order")
        );

        return 3;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .AddColumn<string>("ContentItemId", column => column.WithLength(26))
        );

        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .AddColumn<string>("ListContentType")
        );

        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .AddColumn<string>("DisplayText", column => column.WithLength(ParentContentItemIndex.MaxDisplayTextSize))
        );

        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .AddColumn<bool>("Published")
        );

        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .AddColumn<bool>("Latest")
        );
        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .DropIndex("IDX_ParentContentItemIndex_DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<ParentContentItemIndex>(table => table
            .CreateIndex("IDX_ParentContentItemIndex_DocumentId",
                "Id",
                "DocumentId",
                "ContentItemId",
                "ListContentItemId",
                "DisplayText",
                "Order",
                "ListContentType",
                "Published",
                "Latest")
        );

        return 4;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom4Async()
    {
        await DefineHierarchyFieldsAsync();
        return 5;
    }

    private async Task DefineHierarchyFieldsAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync(ContentFieldSetName.Of<ParentContentItemFieldSet>(), builder => builder
                .WithField(ParentContentItemFieldSet.ListContentItemIdField, field => field
                    .OfType("TextField")
                    .WithDisplayMode(ContentFieldDisplayModes.None)
                    .WithDisplayName("List")
                    .WithPosition("0")
                    .WithEditor(ContentFieldEditors.None)
                    .Locked())
                .WithField(ParentContentItemFieldSet.ListContentTypeField, field => field
                    .OfType("TextField")
                    .WithDisplayMode(ContentFieldDisplayModes.None)
                    .WithDisplayName("List type")
                    .WithPosition("1")
                    .WithEditor(ContentFieldEditors.None)
                    .Locked())
                .WithField(ParentContentItemFieldSet.OrderField, field => field
                    .OfType("NumericField")
                    .WithDisplayMode(ContentFieldDisplayModes.None)
                    .WithDisplayName("Order")
                    .WithPosition("2")
                    .WithEditor(ContentFieldEditors.None)
                    .Locked()));

        // A list's feed values; the list's own editor shows them.
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("ChildContentItems", builder => builder
            .WithField("DisableRssFeed", field => field.OfType("BooleanField").WithDisplayMode(ContentFieldDisplayModes.None).WithDisplayName("Disable the RSS feed").WithPosition("0").WithEditor(ContentFieldEditors.None))
            .WithField("FeedProxyUrl", field => field.OfType("TextField").WithDisplayMode(ContentFieldDisplayModes.None).WithDisplayName("Feed proxy URL").WithPosition("1").WithEditor(ContentFieldEditors.None))
            .WithField("FeedItemsCount", field => field.OfType("NumericField").WithDisplayMode(ContentFieldDisplayModes.None).WithDisplayName("Feed items").WithPosition("2").WithEditor(ContentFieldEditors.None)));
    }
}
