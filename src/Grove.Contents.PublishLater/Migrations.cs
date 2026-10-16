using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Data.Migration;
using Grove.Contents.PublishLater.Indexes;
using YesSql.Sql;
using Grove.Contents.Types;

namespace Grove.Contents.PublishLater;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterFieldSetDefinitionAsync("PublishLater", builder => builder
            .Attachable()
            .WithDescription("Adds the ability to schedule content items to be published at a given future date and time."));

        await SchemaBuilder.CreateMapIndexTableAsync<PublishLaterIndex>(table => table
            .Column<string>("ContentItemId")
            .Column<DateTime>("ScheduledPublishDateTimeUtc")
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await SchemaBuilder.AlterIndexTableAsync<PublishLaterIndex>(table => table
            .CreateIndex("IDX_PublishLaterIndex_DocumentId",
                "Id",
                "DocumentId",
                "ContentItemId",
                "ScheduledPublishDateTimeUtc",
                "Published",
                "Latest")
        );

        await DefinePublishLaterFieldsAsync();

        return 4;
    }

    // This code can be removed in a later version.
    public async Task<int> UpdateFrom1Async()
    {
        // The 'ScheduledPublishDateTimeUtc' column and related index are kept on existing databases,
        // this because dropping an index and altering a column don't work on all providers.

        await SchemaBuilder.AlterIndexTableAsync<PublishLaterIndex>(table => table
            .AddColumn<DateTime>(nameof(PublishLaterIndex.ScheduledPublishDateTimeUtc))
        );

        await SchemaBuilder.AlterIndexTableAsync<PublishLaterIndex>(table => table
            .CreateIndex($"IDX_PublishLaterIndex_ScheduledPublishDateTimeUtc",
                "DocumentId",
                "ScheduledPublishDateTimeUtc")
        );

        return 2;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<PublishLaterIndex>(table =>
        {
            table.AddColumn<string>("ContentItemId");
            table.AddColumn<bool>("Published");
            table.AddColumn<bool>("Latest");
        });

        await SchemaBuilder.AlterIndexTableAsync<PublishLaterIndex>(table =>
        {
            table.DropIndex("IDX_PublishLaterIndex_ScheduledPublishDateTimeUtc");
        });

        await SchemaBuilder.AlterIndexTableAsync<PublishLaterIndex>(table =>
        {
            table.CreateIndex("IDX_PublishLaterIndex_ScheduledPublishDateTimeUtc",
                "Id",
                "DocumentId",
                "ContentItemId",
                "ScheduledPublishDateTimeUtc",
                "Published",
                "Latest");
        });

        return 3;
    }

    // The field set's values are fields (docs/content-engine.md › Behaviour is workflows); the ones its
    // system flows require are locked by the module.
    public async Task<int> UpdateFrom3Async()
    {
        await DefinePublishLaterFieldsAsync();
        return 4;
    }

    private Task DefinePublishLaterFieldsAsync() =>
        _contentDefinitionManager.AlterFieldSetDefinitionAsync("PublishLater", builder => builder
            .WithField(Models.PublishLaterFieldSet.ScheduledPublishUtcField, field => field
                .OfType("DateTimeField")
                .WithDisplayMode(ContentFieldDisplayModes.None)
                .WithDisplayName("Publish at")
                .WithPosition("0")
                .Locked()));
}
